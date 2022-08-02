#include <stddef.h>
#include <assert.h>
#include <stdio.h>
#include <stdint.h>
#include <inttypes.h>
#include "huffman.h"
#include "io.h"
#include "common.h"

int init_vlc(struct vlc *vlc)
{
	assert(vlc != NULL);

	vlc->code = 0;
	vlc->size = 0;

	return RET_SUCCESS;
}

int vlc_add_bit(struct vlc *vlc, uint16_t bit)
{
	assert(vlc != NULL);

	vlc->code <<= 1;
	vlc->code |= bit & 1;
	vlc->size++;

	return RET_SUCCESS;
}

int vlc_remove_bit(struct vlc *vlc, uint16_t *bit)
{
	assert(vlc != NULL);
	assert(bit != NULL);
	assert(vlc->size > 0);

	*bit = (vlc->code >> (vlc->size - 1)) & 1;

	vlc->size--;

	return RET_SUCCESS;
}

/* Figure C.1 – Generation of table of Huffman code sizes */
int generate_size_table(struct htable *htable, struct hcode *hcode)
{
	assert(htable != NULL);
	assert(hcode != NULL);

#define BITS(I)     (htable->L[(I) - 1])
#define HUFFSIZE(K) (hcode->huff_size[(K)])
#define LASTK       (hcode->last_k)

	size_t K = 0;
	size_t I = 1;
	size_t J = 1;

	do {
		while (J <= BITS(I)) {
			assert(K < 256);
			HUFFSIZE(K) = I;
			K++;
			J++;
		}
		I++;
		J = 1;
	} while (I <= 16);
	assert(K < 256);
	HUFFSIZE(K) = 0;
	LASTK = K;

#undef BITS
#undef HUFFSIZE
#undef LASTK

	return RET_SUCCESS;
}

/* Figure C.2 – Generation of table of Huffman codes */
int generate_code_table(struct htable *htable, struct hcode *hcode)
{
	assert(htable != NULL);
	assert(hcode != NULL);

#define HUFFSIZE(K) (hcode->huff_size[(K)])
#define HUFFCODE(K) (hcode->huff_code[(K)])

	size_t K = 0;
	uint16_t CODE = 0;
	size_t SI = HUFFSIZE(0);

	do {
		do {
			assert(K < 256);
			HUFFCODE(K) = CODE;
			CODE++;
			K++;
			assert(K < 256);
		} while (HUFFSIZE(K) == SI);

		assert(K < 256);
		if (HUFFSIZE(K) == 0) {
			return RET_SUCCESS;
		}

		do {
			CODE <<= 1;
			SI++;
			assert(K < 256);
		} while (HUFFSIZE(K) != SI);
	} while (1);

#undef HUFFSIZE
#undef HUFFCODE
}

/* Figure C.3 – Ordering procedure for encoding procedure code tables */
int order_codes(struct htable *htable, struct hcode *hcode)
{
	assert(htable != NULL);
	assert(hcode != NULL);

#define HUFFVAL(K)  (hcode->huff_val[(K)])
#define EHUFCO(I)   (hcode->e_huf_co[(I)])
#define EHUFSI(I)   (hcode->e_huf_si[(I)])
#define LASTK       (hcode->last_k)
#define HUFFSIZE(K) (hcode->huff_size[(K)])
#define HUFFCODE(K) (hcode->huff_code[(K)])

	size_t K = 0;

	do {
		uint8_t I = HUFFVAL(K);
		EHUFCO(I) = HUFFCODE(K);
		EHUFSI(I) = HUFFSIZE(K);
// 		printf("[DEBUG] value=%i cat=%i size=%zu code=%" PRIu16 "\n", I, I & 15, EHUFSI(I), EHUFCO(I));
		K++;
	} while (K < LASTK);

#undef HUFFVAL
#undef EHUFCO
#undef EHUFSI
#undef LASTK
#undef HUFFSIZE
#undef HUFFCODE

	return RET_SUCCESS;
}

int conv_htable_to_hcode(struct htable *htable, struct hcode *hcode)
{
	int err;

	assert(htable != NULL);
	assert(hcode != NULL);

	uint8_t *v = hcode->huff_val;

	for (int i = 0; i < 16; ++i) {
		uint8_t L = htable->L[i];

		for (int l = 0; l < L; ++l) {
			*v = htable->V[i][l];
			v++;
		}
	}

	err = generate_size_table(htable, hcode);
	RETURN_IF(err);

	err = generate_code_table(htable, hcode);
	RETURN_IF(err);

	err = order_codes(htable, hcode);
	RETURN_IF(err);

	return RET_SUCCESS;
}

/*
 * query if the code is present in htable/hcode, and return its value
 *
 * Usage:
 *
 * do {
 *     next_bit(&bits, &bit); // read next bit
 *     vlc_add_bit(vlc, bit); // add this bit to VLC
 * } while (query_code(vlc, htable, hcode, value) == -1); // query Huffman table
 *
 * // value ... category code
 * // read extra bits
 */
int query_code(struct vlc *vlc, struct hcode *hcode, uint8_t *value)
{
	assert(vlc != NULL);
	assert(hcode != NULL);
	assert(value != NULL);

#define HUFFVAL(K)  (hcode->huff_val[(K)])
#define LASTK       (hcode->last_k)
#define HUFFSIZE(K) (hcode->huff_size[(K)])
#define HUFFCODE(K) (hcode->huff_code[(K)])

	size_t K = 0;

	do {
		uint16_t code = HUFFCODE(K);
		size_t size = HUFFSIZE(K);

		if (vlc->size == size && vlc->code == code) {
			uint8_t I = HUFFVAL(K);
			*value = I;
			return RET_SUCCESS;
		}

		K++;
	} while (K < LASTK);

#undef HUFFVAL
#undef LASTK
#undef HUFFSIZE
#undef HUFFCODE

	return -1; /* not found */
}

/* transform value to (code, size), inverse of query_code() */
int value_to_vlc(struct vlc *vlc, struct hcode *hcode, uint8_t value)
{
	assert(vlc != NULL);
	assert(hcode != NULL);

#define HUFFVAL(K)  (hcode->huff_val[(K)])
#define LASTK       (hcode->last_k)
#define HUFFSIZE(K) (hcode->huff_size[(K)])
#define HUFFCODE(K) (hcode->huff_code[(K)])

	for (size_t K = 0; K < LASTK; ++K) {
		if (value == HUFFVAL(K)) {
			vlc->size = HUFFSIZE(K);
			vlc->code = HUFFCODE(K);

			return RET_SUCCESS;
		}
	}

#undef HUFFVAL
#undef LASTK
#undef HUFFSIZE
#undef HUFFCODE

	return -1; /* not found */
}

int read_code(struct bits *bits, struct hcode *hcode, uint8_t *value)
{
	int err;
	struct vlc vlc;

	init_vlc(&vlc);

	do {
		uint8_t bit;
		err = next_bit(bits, &bit);
		RETURN_IF(err);
		err = vlc_add_bit(&vlc, (uint16_t)bit); // add this bit to VLC
		RETURN_IF(err);
	} while (query_code(&vlc, hcode, value) == -1); // query Huffman table

	return RET_SUCCESS;
}

/* inverse of read_code() */
int write_code(struct bits *bits, struct hcode *hcode, uint8_t value)
{
	int err;
	struct vlc vlc;

	err = value_to_vlc(&vlc, hcode, value);
	RETURN_IF(err);

	/* send bits */
	while (vlc.size != 0) {
		uint16_t bit;
		err = vlc_remove_bit(&vlc, &bit);
		RETURN_IF(err);
		err = put_bit(bits, (uint8_t)bit);
		RETURN_IF(err);
	}

	return RET_SUCCESS;
}

int read_extra_bits(struct bits *bits, uint8_t count, uint16_t *value)
{
	int err;
	uint16_t v = 0;

	for (int i = 0; i < count; ++i) {
		uint8_t bit;
		err = next_bit(bits, &bit);
		RETURN_IF(err);
		v <<= 1;
		v |= bit & 1;
	}

	assert(value != NULL);

	*value = v;

	return RET_SUCCESS;
}

int write_extra_bits(struct bits *bits, uint8_t count, uint16_t value)
{
	int err;

	for (int s = count - 1; s >= 0; --s) {
		uint8_t bit = (value >> s) & 1;
		err = put_bit(bits, bit);
		RETURN_IF(err);
	}

	return RET_SUCCESS;
}

/* The procedure “Find V1 for least value of FREQ(V1) > 0” always selects
 * the value with the largest value of V1 when more than one V1 with the same
 * frequency occurs. */
int find_for_least_value_of_freq(struct huffenc *huffenc)
{
	assert(huffenc != NULL);

	// find least value of freq[] > 0
	size_t min_freq = (size_t)-1;
	int v1 = -1;
	for (int i = 0; i < 257; ++i) {
		if (huffenc->freq[i] > 0 && huffenc->freq[i] <= min_freq) {
			min_freq = huffenc->freq[i];
			v1 = i;
		}
	}

	assert(v1 != -1);

	return v1;
}

int find_for_next_least_value_of_freq(struct huffenc *huffenc, int v1)
{
	assert(huffenc != NULL);

	// find least value of freq[] > 0
	size_t min_freq = (size_t)-1;
	int v2 = -1;
	for (int i = 0; i < 257; ++i) {
		if (huffenc->freq[i] > 0 && huffenc->freq[i] <= min_freq && i != v1) {
			min_freq = huffenc->freq[i];
			v2 = i;
		}
	}

	return v2;
}

void code_size(struct huffenc *huffenc)
{
	assert(huffenc != NULL);

#define FREQ(V)     (huffenc->freq[(V)])
#define CODESIZE(V) (huffenc->codesize[(V)])
#define OTHERS(V)   (huffenc->others[(V)])

	do {
		int V1 = find_for_least_value_of_freq(huffenc);
		int V2 = find_for_next_least_value_of_freq(huffenc, V1);

		if (V2 == -1) {
			break;
		}

		FREQ(V1) += FREQ(V2);
		FREQ(V2) = 0;

		while (1) {
			CODESIZE(V1)++;

			if (OTHERS(V1) == -1) {
				break;
			} else {
				V1 = OTHERS(V1);
			}
		}

		OTHERS(V1) = V2;

		while (1) {
			CODESIZE(V2)++;

			if (OTHERS(V2) == -1) {
				break;
			} else {
				V2 = OTHERS(V2);
			}
		}
	} while (1);

#undef FREQ
#undef CODESIZE
#undef OTHERS
}

void adjust_bits(struct huffenc *huffenc)
{
	assert(huffenc != NULL);

#define BITS(I) (huffenc->bits[(I)])

	int i = 32;

loop:
	if (BITS(i) > 0) {
		int j = i - 1;
		do {
			j--;
		} while (BITS(j) <= 0);
		BITS(i) -= 2;
		BITS(i - 1) += 1;
		BITS(j + 1) += 2;
		BITS(j) -= 1;
		goto loop;
	} else {
		i--;
		if (i != 16) {
			goto loop;
		}
		while (BITS(i) == 0) {
			i--;
		}
		BITS(i) -= 1;
	}

#undef BITS
}

void count_bits(struct huffenc *huffenc)
{
	assert(huffenc != NULL);

#define BITS(I)     (huffenc->bits[(I)])
#define CODESIZE(V) (huffenc->codesize[(V)])

	/* The counts in BITS are zero at the start of the procedure */
	for (int k = 0; k < 33; ++k) {
		BITS(k) = 0;
	}

	int i = 0;

	do {
		assert(CODESIZE(i) < 33);

		if (CODESIZE(i) != 0) {
			BITS(CODESIZE(i))++;
		}

		i++;
	} while (i != 257);

	adjust_bits(huffenc);

#undef BITS
#undef CODESIZE
}

void sort_input(struct huffenc *huffenc)
{
	assert(huffenc != NULL);

#define CODESIZE(V) (huffenc->codesize[(V)])
#define HUFFVAL(K)  (huffenc->huff_val[(K)])

	int i = 1;
	int k = 0;

	do {
		int j = 0;

		do {
			assert(j < 257);

			if (CODESIZE(j) == (size_t)i) {
				assert(k < 16 * 255);

				HUFFVAL(k) = j;
				k++;
			}
			j++;
		} while (j <= 255);

		i++;
	} while (i <= 32);

#undef CODESIZE
#undef HUFFVAL
}

int adapt_huffman_table(struct htable *htable, struct huffenc *huffenc)
{
	assert(htable != NULL);
	assert(huffenc != NULL);

	code_size(huffenc);

	count_bits(huffenc);

	sort_input(huffenc);

	// fill htable.L[]
	for (int i = 0; i < 16; ++i) {
		htable->L[i] = huffenc->bits[i + 1];
	}

	// fill htable.V[]
	uint8_t *v = huffenc->huff_val;

	for (int i = 0; i < 16; ++i) {
		uint8_t L = htable->L[i];

		for (int l = 0; l < L; ++l) {
			htable->V[i][l] = *v;
			v++;
		}
	}

	return RET_SUCCESS;
}
