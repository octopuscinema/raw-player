#include <arpa/inet.h>
#include <assert.h>
#include "io.h"
#include "common.h"

int init_bits(struct bits *bits, FILE *stream)
{
	assert(bits != NULL);

	bits->count = 0;
	bits->stream = stream;

	return RET_SUCCESS;
}

/* F.2.2.5 The NEXTBIT procedure
 * Figure F.18 – Procedure for fetching the next bit of compressed data */
int next_bit(struct bits *bits, uint8_t *bit)
{
	int err;

	assert(bits != NULL);

	if (bits->count == 0) {
		/* refill bits->byte */
		err = read_ecs_byte(bits->stream, &bits->byte);
		RETURN_IF(err); /* incl. RET_FAILURE_NO_MORE_DATA */

		bits->count = 8;
	}

	assert(bit != NULL);

	/* output MSB */
	*bit = bits->byte >> 7;

	bits->byte <<= 1;
	bits->count--;

	return RET_SUCCESS;
}

int put_bit(struct bits *bits, uint8_t bit)
{
	assert(bits != NULL);
	assert(bits->count < 8);

	bits->byte <<= 1;
	bits->byte |= bit & 1;

	bits->count++;

	if (bits->count == 8) {
		int err;

		err = write_ecs_byte(bits->stream, bits->byte);
		RETURN_IF(err);

		bits->count = 0;
	}

	return RET_SUCCESS;
}

int flush_bits(struct bits *bits)
{
	int err;

	assert(bits != NULL);

	if (bits->count == 0) {
		return RET_SUCCESS;
	}

	while (bits->count < 8) {
		bits->byte <<= 1;
		bits->byte |= 1;
		bits->count++;
	}

	err = write_ecs_byte(bits->stream, bits->byte);
	RETURN_IF(err);

	bits->count = 0;

	return RET_SUCCESS;
}

int read_byte(FILE *stream, uint8_t *byte)
{
	if (fread(byte, sizeof(uint8_t), 1, stream) != 1) {
		return RET_FAILURE_FILE_IO;
	}

	return RET_SUCCESS;
}

int write_byte(FILE *stream, uint8_t byte)
{
	if (fwrite(&byte, sizeof(uint8_t), 1, stream) != 1) {
		return RET_FAILURE_FILE_IO;
	}

	return RET_SUCCESS;
}

int read_word(FILE *stream, uint16_t *word)
{
	uint16_t w;

	if (fread(&w, sizeof(uint16_t), 1, stream) != 1) {
		return RET_FAILURE_FILE_IO;
	}

	w = ntohs(w);

	assert(word != NULL);

	*word = w;

	return RET_SUCCESS;
}

int write_word(FILE *stream, uint16_t word)
{
	word = htons(word);

	if (fwrite(&word, sizeof(uint16_t), 1, stream) != 1) {
		return RET_FAILURE_FILE_IO;
	}

	return RET_SUCCESS;
}

int read_length(FILE *stream, uint16_t *len)
{
	int err;

	err = read_word(stream, len);
	RETURN_IF(err);

	return RET_SUCCESS;
}

int write_length(FILE *stream, uint16_t len)
{
	int err;

	err = write_word(stream, len);
	RETURN_IF(err);

	return RET_SUCCESS;
}

int read_nibbles(FILE *stream, uint8_t *first, uint8_t *second)
{
	int err;
	uint8_t byte;

	assert(first != NULL);
	assert(second != NULL);

	err = read_byte(stream, &byte);
	RETURN_IF(err);

	/* The first 4-bit parameter of the pair shall occupy the most significant 4 bits of the byte.  */
	*first = (byte >> 4) & 15;
	*second = (byte >> 0) & 15;

	return RET_SUCCESS;
}

int write_nibbles(FILE *stream, uint8_t first, uint8_t second)
{
	int err;
	uint8_t byte = (first << 4) | (second & 15);

	err = write_byte(stream, byte);
	RETURN_IF(err);

	return RET_SUCCESS;
}

/* B.1.1.2 Markers
 * All markers are assigned two-byte codes */
int read_marker(FILE *stream, uint16_t *marker)
{
	int err;
	uint8_t byte;

	/* Any marker may optionally be preceded by any
	 * number of fill bytes, which are bytes assigned code X’FF’. */

	long start = ftell(stream), end;

	seek: do {
		err = read_byte(stream, &byte);
		RETURN_IF(err);
	} while (byte != 0xff);

	do {
		err = read_byte(stream, &byte);
		RETURN_IF(err);

		switch (byte) {
			case 0xff:
				continue;
			/* not a marker */
			case 0x00:
				goto seek;
			default:
				end = ftell(stream);
				if (end - start != 2) {
					printf("*** %li bytes skipped ***\n", end - start - 2);
				}
				*marker = UINT16_C(0xff00) | byte;
				return RET_SUCCESS;
		}
	} while (1);
}

int write_marker(FILE *stream, uint16_t marker)
{
	int err;

	assert((marker >> 8) == 0xff);

	err = write_byte(stream, 0xff);
	RETURN_IF(err);

	err = write_byte(stream, (uint8_t)marker);
	RETURN_IF(err);

	return RET_SUCCESS;
}

int skip_segment(FILE *stream, uint16_t len)
{
	if (fseek(stream, (long)len - 2, SEEK_CUR) != 0) {
		return RET_FAILURE_FILE_SEEK;
	}

	return RET_SUCCESS;
}

/* F.1.2.3 Byte stuffing */
int read_ecs_byte(FILE *stream, uint8_t *byte)
{
	int err;
	uint8_t b;

	assert(byte != NULL);

	err = read_byte(stream, &b);
	RETURN_IF(err);

	if (b == 0xff) {
		err = read_byte(stream, &b);
		RETURN_IF(err);

		if (b == 0x00) {
			*byte = 0xff;
			return RET_SUCCESS;
		} else {
			if (fseek(stream, -2, SEEK_CUR) != 0) {
				return RET_FAILURE_FILE_SEEK;
			}
			return RET_FAILURE_NO_MORE_DATA;
		}
	} else {
		*byte = b;
		return RET_SUCCESS;
	}
}

/* B.1.1.5 Entropy-coded data segments */
int write_ecs_byte(FILE *stream, uint8_t byte)
{
	int err;

	err = write_byte(stream, byte);
	RETURN_IF(err);

	if (byte == 0xff) {
		err = write_byte(stream, 0x00);
		RETURN_IF(err);
	}

	return RET_SUCCESS;
}
