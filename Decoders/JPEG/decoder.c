#include <stddef.h>
#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <inttypes.h>
#include <assert.h>
#include "common.h"
#include "io.h"
#include "huffman.h"
#include "coeffs.h"
#include "imgproc.h"
#include "frame.h"

const char *Pq_to_str[] = {
	[0] = "8-bit",
	[1] = "16-bit"
};

/* B.2.4.1 Quantization table-specification syntax */
int parse_qtable(FILE *stream, struct context *context)
{
	int err;
	uint8_t Pq, Tq;
	struct qtable *qtable;

	assert(context != NULL);

	err = read_nibbles(stream, &Pq, &Tq);
	RETURN_IF(err);

	if (Tq >= 4) {
		/* invalid value */
		return RET_FAILURE_FILE_UNSUPPORTED;
	}

	assert(Tq < 4);
	assert(Pq < 2);

	printf("Pq = %" PRIu8 " (%s), Tq = %" PRIu8 " (QT identifier)\n", Pq, Pq_to_str[Pq], Tq);

	qtable = &context->qtable[Tq];

	/* precision */
	qtable->Pq = Pq;

	for (int i = 0; i < 64; ++i) {
		if (Pq == 0) {
			uint8_t byte;
			err = read_byte(stream, &byte);
			RETURN_IF(err);
			qtable->Q[zigzag[i]] = (uint16_t)byte;
		} else {
			uint16_t word;
			err = read_word(stream, &word);
			RETURN_IF(err);
			qtable->Q[zigzag[i]] = word;
		}
	}

	for (int y = 0; y < 8; ++y) {
		for (int x = 0; x < 8; ++x) {
			printf("%3" PRIu16 " ", qtable->Q[y * 8 + x]);
		}
		printf("\n");
	}

	return RET_SUCCESS;
}

int parse_frame_header(FILE *stream, struct context *context)
{
	int err;
	/* Sample precision */
	uint8_t P;
	/* Number of lines, Number of samples per line */
	uint16_t Y, X;
	/* Number of image components in frame */
	uint8_t Nf;

	assert(context != NULL);

	err = read_byte(stream, &P);
	RETURN_IF(err);
	err = read_word(stream, &Y);
	RETURN_IF(err);
	err = read_word(stream, &X);
	RETURN_IF(err);
	err = read_byte(stream, &Nf);
	RETURN_IF(err);

	assert(X > 0);
	assert(Nf > 0);

	printf("P = %" PRIu8 " (Sample precision), Y = %" PRIu16 ", X = %" PRIu16 ", Nf = %" PRIu8 " (Number of image components)\n", P, Y, X, Nf);

	/* precision */
	context->P = P;

	context->Y = Y;
	context->X = X;

	/* components */
	context->Nf = Nf;

	uint8_t max_H = 0, max_V = 0;

	for (int i = 0; i < Nf; ++i) {
		uint8_t C;
		uint8_t H, V;
		uint8_t Tq;

		err = read_byte(stream, &C);
		RETURN_IF(err);
		err = read_nibbles(stream, &H, &V);
		RETURN_IF(err);
		err = read_byte(stream, &Tq);
		RETURN_IF(err);

		printf("C = %" PRIu8 " (Component identifier), H = %" PRIu8 ", V = %" PRIu8 ", Tq = %" PRIu8 " (QT identifier)\n", C, H, V, Tq);

		context->component[C].H = H;
		context->component[C].V = V;
		context->component[C].Tq = Tq;

		max_H = (H > max_H) ? H : max_H;
		max_V = (V > max_V) ? V : max_V;
	}

	context->max_H = max_H;
	context->max_V = max_V;

	err = compute_no_blocks_and_alloc_buffers(context);
	RETURN_IF(err);

	return RET_SUCCESS;
}

const char *Tc_to_str[] = {
	[0] = "DC",
	[1] = "AC"
};

int parse_huffman_tables(FILE *stream, struct context *context)
{
	int err;
	uint8_t Tc, Th;

	assert(context != NULL);

	err = read_nibbles(stream, &Tc, &Th);
	RETURN_IF(err);

	if (Tc >= 2) {
		return RET_FAILURE_FILE_UNSUPPORTED;
	}

	assert(Tc < 2);

	printf("Tc = %" PRIu8 " (%s table) Th = %" PRIu8 " (HT identifier)\n", Tc, Tc_to_str[Tc], Th);

	struct htable *htable = &context->htable[Tc][Th];

	for (int i = 0; i < 16; ++i) {
		err = read_byte(stream, &htable->L[i]);
		RETURN_IF(err);
	}

	for (int i = 0; i < 16; ++i) {
		uint8_t L = htable->L[i];

		for (int l = 0; l < L; ++l) {
			err = read_byte(stream, &htable->V[i][l]);
			RETURN_IF(err);
		}
	}

	/* Annex C */
	struct hcode *hcode = &context->hcode[Tc][Th];

	err = conv_htable_to_hcode(htable, hcode);
	RETURN_IF(err);

	return RET_SUCCESS;
}

struct scan {
	uint8_t Ns;
	uint8_t Cs[256];

	/* useful to remove differential DC coding
	 *
	 * At the beginning of the scan and at the beginning of each restart interval, the prediction for the DC coefficient prediction
	 * is initialized to 0. */
	struct int_block *last_block[256];
};

int parse_scan_header(FILE *stream, struct context *context, struct scan *scan)
{
	int err;
	/* Number of image components in scan */
	uint8_t Ns;

	err = read_byte(stream, &Ns);
	RETURN_IF(err);

	printf("Ns = %" PRIu8 " (Number of image components in scan)\n", Ns);

	assert(scan != NULL);

	scan->Ns = Ns;

	for (int j = 0; j < Ns; ++j) {
		uint8_t Cs;
		uint8_t Td, Ta;

		err = read_byte(stream, &Cs);
		RETURN_IF(err);
		err = read_nibbles(stream, &Td, &Ta);
		RETURN_IF(err);

		printf("Cs%i = %" PRIu8 " (Component identifier), Td%i = %" PRIu8 " (DC HT identifier), Ta%i = %" PRIu8 " (AC HT identifier)\n", j, Cs, j, Td, j, Ta);

		scan->Cs[j] = Cs;

		context->component[Cs].Td = Td;
		context->component[Cs].Ta = Ta;
	}

	uint8_t Ss;
	uint8_t Se;
	uint8_t Ah, Al;

	err = read_byte(stream, &Ss);
	RETURN_IF(err);
	err = read_byte(stream, &Se);
	RETURN_IF(err);
	err = read_nibbles(stream, &Ah, &Al);
	RETURN_IF(err);

	if (Ss != 0 || Se != 63) {
		return RET_FAILURE_FILE_UNSUPPORTED;
	}

	assert(Ss == 0);
	assert(Se == 63);
	printf("Ss = %" PRIu8 " (the first DCT coefficient), Se = %" PRIu8 " (the last DCT coefficient)\n", Ss, Se);

	if (Ah != 0 || Al != 0) {
		return RET_FAILURE_FILE_UNSUPPORTED;
	}

	assert(Ah == 0);
	assert(Al == 0);
	printf("Ah = %" PRIu8 " (bit position high), Al = %" PRIu8 " (bit position low)\n", Ah, Al);

	context->mblocks = 0;

	return RET_SUCCESS;
}

/* read MCU */
int read_macroblock(struct bits *bits, struct context *context, struct scan *scan)
{
	int err;

	assert(scan != NULL);
	assert(context != NULL);

	size_t seq_no = context->mblocks;

	if (scan->Ns == 0) {
		/* nothing to do */
		return RET_FAILURE_NO_MORE_DATA;
	} else if (scan->Ns == 1) {
		/* A.2.2 Non-interleaved order (Ns = 1) */
		assert(scan->Ns == 1);

		uint8_t Cs = scan->Cs[0];

		uint8_t H = context->component[Cs].H;
		uint8_t V = context->component[Cs].V;

		size_t blocks_in_mb = H * V;

		for (size_t w = 0; w < blocks_in_mb; ++w) {
			size_t block_x = (blocks_in_mb * seq_no + w) % context->component[Cs].b_x;
			size_t block_y = (blocks_in_mb * seq_no + w) / context->component[Cs].b_x;

			size_t block_seq = block_y * context->component[Cs].b_x + block_x;

			struct int_block *int_block = &context->component[Cs].int_buffer[block_seq];

			/* read block */
			err = read_block(bits, context, Cs, int_block);
			RETURN_IF(err);

			if (scan->last_block[Cs] != NULL) {
				int_block->c[0] += scan->last_block[Cs]->c[0];
			}

			scan->last_block[Cs] = int_block;
		}
	} else {
		assert(scan->Ns > 1);

		if (context->m_x == 0) {
			/* missing SOF before SOS? */
			return RET_FAILURE_FILE_UNSUPPORTED;
		}

		assert(context->m_x != 0);

		size_t x = seq_no % context->m_x;
		size_t y = seq_no / context->m_x;

// 		printf("[DEBUG] reading macroblock... x=%zu y=%zu\n", x, y);

		/* for each component */
		for (int j = 0; j < scan->Ns; ++j) {
			uint8_t Cs = scan->Cs[j];
			uint8_t H = context->component[Cs].H;
			uint8_t V = context->component[Cs].V;

// 			printf("[DEBUG] reading component %" PRIu8 " blocks @ x=%zu y=%zu\n", Cs, x * H, y * V);

			/* for each 8x8 block */
			for (int v = 0; v < V; ++v) {
				for (int h = 0; h < H; ++h) {
					size_t block_x = x * H + h;
					size_t block_y = y * V + v;

					assert(block_x < context->component[Cs].b_x);

					size_t block_seq = block_y * context->component[Cs].b_x + block_x;

// 					printf("[DEBUG] reading component %" PRIu8 " blocks @ x=%zu y=%zu out of X=%zu Y=%zu\n", Cs, x * H + h, y * V + v, context->component[Cs].b_x, context->component[Cs].b_y);
// 					printf("[DEBUG] reading component %" PRIu8 " block# %zu out of %zu\n", Cs, block_seq, context->component[Cs].b_x * context->component[Cs].b_y);

					struct int_block *int_block = &context->component[Cs].int_buffer[block_seq];

					/* past the end of data? */
					if (block_seq >= context->component[Cs].b_x * context->component[Cs].b_y) {
						int_block = NULL;
					}

					/* read block */
					err = read_block(bits, context, Cs, int_block);
					RETURN_IF(err);

					/* remove differential DC coding */
					if (scan->last_block[Cs] != NULL) {
						int_block->c[0] += scan->last_block[Cs]->c[0];
					}

					scan->last_block[Cs] = int_block;
				}
			}
		}
	}

	return RET_SUCCESS;
}

int read_ecs(FILE *stream, struct context *context, struct scan *scan)
{
	int err;
	struct bits bits;

	init_bits(&bits, stream);

	for (int i = 0; i < 256; ++i) {
		scan->last_block[i] = NULL;
	}

	/* loop over macroblocks */
	do {
		err = read_macroblock(&bits, context, scan);
		if (err == RET_FAILURE_NO_MORE_DATA)
			goto end;
		RETURN_IF(err);
		context->mblocks++;
	} while (1);

end:
	printf("Processed: %zu macroblocks\n", context->mblocks);

	return RET_SUCCESS;
}

int parse_restart_interval(FILE *stream, struct context *context)
{
	int err;
	uint16_t Ri;

	err = read_word(stream, &Ri);
	RETURN_IF(err);

	context->Ri = Ri;

	return RET_SUCCESS;
}

int parse_comment(FILE *stream, uint16_t len)
{
	if (len < 2) {
		return RET_FAILURE_FILE_UNSUPPORTED;
	}

	assert(len >= 2);

	size_t l = len - 2;

	char *buf = malloc(l + 1);

	if (buf == NULL) {
		return RET_FAILURE_MEMORY_ALLOCATION;
	}

	if (fread(buf, sizeof(char), l, stream) != l) {
		free(buf);
		return RET_FAILURE_FILE_IO;
	}

	buf[l] = 0;

	printf("%s\n", buf);

	free(buf);

	return RET_SUCCESS;
}

int write_image(struct context *context, const char *path)
{
	int err;

	struct frame frame;

	err = frame_create(context, &frame);
	RETURN_IF(err);

	err = frame_to_rgb(&frame);

	if (err) {
		goto end;
	}

	err = write_frame(&frame, path);

end:
	frame_destroy(&frame);

	return err;
}

int epilogue(struct context *context, const char *path)
{
	int err;

	err = dequantize(context);
	RETURN_IF(err);
	err = inverse_dct(context);
	RETURN_IF(err);
	err = conv_blocks_to_frame(context);
	RETURN_IF(err);
	err = write_image(context, path);
	RETURN_IF(err);

	return RET_SUCCESS;
}

int parse_format(FILE *stream, struct context *context, const char *path)
{
	int err;

	struct scan scan;

	// init
	scan.Ns = 0;

	while (1) {
		uint16_t marker;

		err = read_marker(stream, &marker);
		RETURN_IF(err);

		/* An asterisk (*) indicates a marker which stands alone,
		 * that is, which is not the start of a marker segment. */
		switch (marker) {
			uint16_t len;
			long pos;

			/* SOI* Start of image */
			case 0xffd8:
				printf("SOI\n");
				break;
			/* APPn */
			case 0xffe0:
			case 0xffe1:
			case 0xffe2:
			case 0xffe3:
			case 0xffe4:
			case 0xffe5:
			case 0xffe6:
			case 0xffe7:
			case 0xffe8:
			case 0xffeb:
			case 0xffec:
			case 0xffed:
			case 0xffee:
				printf("APP%i\n", marker & 0xf);
				err = read_length(stream, &len);
				RETURN_IF(err);
				err = skip_segment(stream, len);
				RETURN_IF(err);
				break;
			/* DQT Define quantization table(s) */
			case 0xffdb:
				printf("DQT\n");
				pos = ftell(stream);
				err = read_length(stream, &len);
				RETURN_IF(err);
				do {
					err = parse_qtable(stream, context);
					RETURN_IF(err);
				} while (ftell(stream) < pos + len);
				break;
			/* SOF0 Baseline DCT */
			case 0xffc0:
				printf("SOF0\n");
				err = read_length(stream, &len);
				RETURN_IF(err);
				err = parse_frame_header(stream, context);
				RETURN_IF(err);
				break;
			/* SOF1 Extended sequential DCT */
			case 0xffc1:
				printf("SOF1\n");
				err = read_length(stream, &len);
				RETURN_IF(err);
				err = parse_frame_header(stream, context);
				RETURN_IF(err);
				break;
			/* SOF2 Progressive DCT */
			case 0xffc2:
				printf("SOF2\n");
				err = read_length(stream, &len);
				RETURN_IF(err);
				err = parse_frame_header(stream, context);
				RETURN_IF(err);
				fprintf(stderr, "Progressive DCT not supported!\n");
				return RET_FAILURE_FILE_UNSUPPORTED;
			/* SOF3 Lossless (sequential) */
			case 0xffc3:
				printf("SOF3\n");
				err = read_length(stream, &len);
				RETURN_IF(err);
				err = parse_frame_header(stream, context);
				RETURN_IF(err);
				fprintf(stderr, "Lossless JPEG not supported!\n");
				return RET_FAILURE_FILE_UNSUPPORTED;
			/* SOF9 Extended sequential DCT (arithmetic coding) */
			case 0xffc9:
				printf("SOF9\n");
				err = read_length(stream, &len);
				RETURN_IF(err);
				err = parse_frame_header(stream, context);
				RETURN_IF(err);
				fprintf(stderr, "Arithmetic coding not supported!\n");
				return RET_FAILURE_FILE_UNSUPPORTED;
			/* SOF10 Progressive DCT (arithmetic coding) */
			case 0xffca:
				printf("SOF10\n");
				err = read_length(stream, &len);
				RETURN_IF(err);
				err = parse_frame_header(stream, context);
				RETURN_IF(err);
				fprintf(stderr, "Arithmetic coding not supported!\n");
				return RET_FAILURE_FILE_UNSUPPORTED;
			/* DHT Define Huffman table(s) */
			case 0xffc4:
				printf("DHT\n");
				pos = ftell(stream);
				err = read_length(stream, &len);
				RETURN_IF(err);
				/* parse multiple tables in single DHT */
				do {
					err = parse_huffman_tables(stream, context);
					RETURN_IF(err);
				} while (ftell(stream) < pos + len);
				break;
			/* SOS Start of scan */
			case 0xffda:
				printf("SOS\n");
				err = read_length(stream, &len);
				RETURN_IF(err);
				err = parse_scan_header(stream, context, &scan);
				RETURN_IF(err);
				err = read_ecs(stream, context, &scan);
				RETURN_IF(err);
				break;
			/* EOI* End of image */
			case 0xffd9:
				printf("EOI\n");
				pos = ftell(stream);
				fseek(stream, 0, SEEK_END);
				if (ftell(stream) - pos > 0) {
					printf("*** %li bytes of garbage ***\n", ftell(stream) - pos);
				}
				err = epilogue(context, path);
				RETURN_IF(err);
				return RET_SUCCESS;
			/* DRI Define restart interval */
			case 0xffdd:
				printf("DRI\n");
				err = read_length(stream, &len);
				RETURN_IF(err);
				err = parse_restart_interval(stream, context);
				RETURN_IF(err);
				break;
			/* RSTm* Restart with modulo 8 count “m” */
			case 0xffd0:
			case 0xffd1:
			case 0xffd2:
			case 0xffd3:
			case 0xffd4:
			case 0xffd5:
			case 0xffd6:
			case 0xffd7:
				printf("RST%i\n", marker & 0xf);
				err = read_ecs(stream, context, &scan);
				RETURN_IF(err);
				break;
			/* COM Comment */
			case 0xfffe:
				printf("COM\n");
				err = read_length(stream, &len);
				RETURN_IF(err);
				err = parse_comment(stream, len);
				RETURN_IF(err);
				break;
			/* TEM* For temporary private use in arithmetic coding */
			case 0xff01:
				printf("TEM\n");
				break;
			/* DAC Define arithmetic coding conditioning(s) */
			case 0xffcc:
				printf("DAC\n");
				err = read_length(stream, &len);
				RETURN_IF(err);
				err = skip_segment(stream, len);
				RETURN_IF(err);
				break;
			default:
				fprintf(stderr, "unhandled marker 0x%" PRIx16 "\n", marker);
				return RET_FAILURE_FILE_UNSUPPORTED;
		}
	}
}

int process_jpeg_stream(FILE *stream, const char *path)
{
	int err;

	struct context *context = malloc(sizeof(struct context));

	if (context == NULL) {
		fprintf(stderr, "malloc failure\n");
		return RET_FAILURE_MEMORY_ALLOCATION;
	}

	err = init_context(context);

	if (err) {
		goto end;
	}

	err = parse_format(stream, context, path);
end:
	free_buffers(context);

	free(context);

	return err;
}

int process_jpeg_file(const char *i_path, const char *o_path)
{
	FILE *stream = fopen(i_path, "r");

	if (stream == NULL) {
		fprintf(stderr, "fopen failure\n");
		return RET_FAILURE_FILE_OPEN;
	}

	int err = process_jpeg_stream(stream, o_path);

	fclose(stream);

	return err;
}

int main(int argc, char *argv[])
{
	const char *i_path = argc > 1 ? argv[1] : "Lenna.jpg";
	const char *o_path = argc > 2 ? argv[2] : NULL;

	int err = process_jpeg_file(i_path, o_path);

	if (err) {
		printf("Failure.\n");
		return 1;
	}

	printf("Success.\n");

	return 0;
}
