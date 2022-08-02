#include <stddef.h>
#include <stdio.h>
#include <stdlib.h>
#include <inttypes.h>
#include <assert.h>
#include <unistd.h>
#include "common.h"
#include "frame.h"
#include "coeffs.h"
#include "imgproc.h"
#include "huffman.h"

/* K.1 Quantization tables for luminance and chrominance components */
static const unsigned int std_luminance_quant_tbl[64] = {
  16,  11,  10,  16,  24,  40,  51,  61,
  12,  12,  14,  19,  26,  58,  60,  55,
  14,  13,  16,  24,  40,  57,  69,  56,
  14,  17,  22,  29,  51,  87,  80,  62,
  18,  22,  37,  56,  68, 109, 103,  77,
  24,  35,  55,  64,  81, 104, 113,  92,
  49,  64,  78,  87, 103, 121, 120, 101,
  72,  92,  95,  98, 112, 100, 103,  99
};

static const unsigned int std_chrominance_quant_tbl[64] = {
  17,  18,  24,  47,  99,  99,  99,  99,
  18,  21,  26,  66,  99,  99,  99,  99,
  24,  26,  56,  99,  99,  99,  99,  99,
  47,  66,  99,  99,  99,  99,  99,  99,
  99,  99,  99,  99,  99,  99,  99,  99,
  99,  99,  99,  99,  99,  99,  99,  99,
  99,  99,  99,  99,  99,  99,  99,  99,
  99,  99,  99,  99,  99,  99,  99,  99
};

/* 0..100 to scaling_factor */
/* according to https://github.com/libjpeg-turbo/ijg/blob/master/jcparam.c */
int quality_to_sf(int q)
{
	if (q < 1) {
		q = 1;
	}
	if (q > 100) {
		q = 100;
	}

	int sf;

	if (q < 50) {
		sf = 5000 / q;
	} else {
		sf = 200 - q * 2;
	}

	return sf;
}

void set_qtable(struct qtable *qtable, const unsigned int Q_ref[64], int q)
{
	int sf = quality_to_sf(q);

	for (int i = 0; i < 64; ++i) {
		qtable->Q[i] = clamp(1, (Q_ref[i] * sf + 50) / 100, 255);
	}
}

/* command line parameters */
struct params {
	/* luma subsampling */
	uint8_t H, V;

	/* quality 1..100 */
	int q;

	int optimize;
};

void init_params(struct params *params)
{
	assert(params != NULL);

	params->H = 2;
	params->V = 1;

	params->q = 75;

	params->optimize = 1;
}

int read_image(struct context *context, FILE *stream, struct params *params)
{
	int err;

	struct frame frame;

	assert(context != NULL);

	// load PPM/PGM header, detect X, Y, number of components, bpp
	err = read_frame_header(&frame, stream);
	RETURN_IF(err);

	printf("read PPM/PGM header: Nf=%" PRIu8 " Y=%" PRIu16 " X=%" PRIu16 " P=%" PRIu8 "\n", frame.components, frame.Y, frame.X, frame.precision);

	context->Nf = frame.components;
	context->Y = frame.Y;
	context->X = frame.X;
	context->P = frame.precision;

	switch (frame.components) {
		case 1:
			context->component[1].H = 1;
			context->component[1].V = 1;

			context->component[1].Tq = 0;

			context->component[1].Td = 0;
			context->component[1].Ta = 0;

			context->max_H = 1;
			context->max_V = 1;
			break;
		case 3:
			assert(params->H >= 1 && params->H <= 2);
			assert(params->V >= 1 && params->V <= 2);

			context->component[1].H = params->H;
			context->component[1].V = params->V;
			context->component[2].H = 1;
			context->component[2].V = 1;
			context->component[3].H = 1;
			context->component[3].V = 1;

			context->component[1].Tq = 0;
			context->component[2].Tq = 1;
			context->component[3].Tq = 1;

			context->component[1].Td = 0;
			context->component[1].Ta = 0;
			context->component[2].Td = 1;
			context->component[2].Ta = 1;
			context->component[3].Td = 1;
			context->component[3].Ta = 1;

			context->max_H = params->H;
			context->max_V = params->V;
			break;
		default:
			return RET_FAILURE_FILE_UNSUPPORTED;
	}

	set_qtable(&context->qtable[0], std_luminance_quant_tbl, params->q);
	set_qtable(&context->qtable[1], std_chrominance_quant_tbl, params->q);

	err = frame_create_empty(context, &frame);
	RETURN_IF(err);

	// load frame body
	err = read_frame_body(&frame, stream);
	RETURN_IF(err);

	err = compute_no_blocks_and_alloc_buffers(context);
	RETURN_IF(err);

	err = frame_to_ycc(&frame);
	RETURN_IF(err);

	// copy frame->data[] into context->component[]->frame_buffer[]
	transform_frame_to_components(context, &frame);

	frame_destroy(&frame);

	return RET_SUCCESS;
}

/* read_image(), conv_frame_to_blocks(), forward_dct(), quantize() */
int prologue(struct context *context, FILE *i_stream, struct params *params)
{
	int err;

	err = read_image(context, i_stream, params);
	RETURN_IF(err);

	err = conv_frame_to_blocks(context);
	RETURN_IF(err);

	err = forward_dct(context);
	RETURN_IF(err);

	err = quantize(context);
	RETURN_IF(err);

	return RET_SUCCESS;
}

int produce_SOI(FILE *stream)
{
	int err;

	err = write_marker(stream, 0xffd8);
	RETURN_IF(err);

	return RET_SUCCESS;
}

int produce_DQT(struct context *context, uint8_t Tq, FILE *stream)
{
	int err;

	assert(context != NULL);

	err = write_marker(stream, 0xffdb);
	RETURN_IF(err);

	// length = 2 (len) + 1 (Pq, Tq) + 64 (Q[]) = 67
	err = write_length(stream, 67);
	RETURN_IF(err);

	uint8_t Pq;
	Pq = 0;

	err = write_nibbles(stream, Pq, Tq);
	RETURN_IF(err);

	struct qtable *qtable = &context->qtable[Tq];

	for (int i = 0; i < 64; ++i) {
		uint8_t byte = (uint8_t)qtable->Q[zigzag[i]];

		err = write_byte(stream, byte);
		RETURN_IF(err);
	}

	return RET_SUCCESS;
}

int produce_SOF0(struct context *context, FILE *stream)
{
	int err;

	assert(context != NULL);

	err = write_marker(stream, 0xffc0);
	RETURN_IF(err);

	uint8_t Nf = context->Nf;

	// length = 2 (len) + 1 (P) + 2 (Y) + 2 (X) + 1 (Nf) + Nf * ( 1 (C) + 1 (H, V) + 1 (Tq) ) = 8 + 3 * Nf
	err = write_length(stream, 8 + 3 * Nf);
	RETURN_IF(err);

	err = write_byte(stream, context->P);
	RETURN_IF(err);
	err = write_word(stream, context->Y);
	RETURN_IF(err);
	err = write_word(stream, context->X);
	RETURN_IF(err);
	err = write_byte(stream, context->Nf);
	RETURN_IF(err);

	for (int i = 0; i < 256; ++i) {
		if (context->component[i].H != 0) {
			err = write_byte(stream, (uint8_t)i);
			RETURN_IF(err);

			err = write_nibbles(stream, context->component[i].H, context->component[i].V);
			RETURN_IF(err);

			err = write_byte(stream, context->component[i].Tq);
			RETURN_IF(err);
		}
	}

	return RET_SUCCESS;
}

int produce_DHT(struct context *context, uint8_t Tc, uint8_t Th, FILE *stream)
{
	int err;

	assert(context != NULL);

	err = write_marker(stream, 0xffc4);
	RETURN_IF(err);

	struct htable *htable = &context->htable[Tc][Th];

	// compute "mt (V)"
	uint16_t mt = 0;
	for (int i = 0; i < 16; ++i) {
		uint8_t L = htable->L[i];
		mt += L;
	}

	// length = 2 (len) + 1 (Tc, Th) + 16 * 1 (L) + mt (V) = 2 + 17 + mt (V)
	err = write_length(stream, 2 + 17 + mt);
	RETURN_IF(err);

	err = write_nibbles(stream, Tc, Th);
	RETURN_IF(err);

	for (int i = 0; i < 16; ++i) {
		err = write_byte(stream, htable->L[i]);
		RETURN_IF(err);
	}

	for (int i = 0; i < 16; ++i) {
		uint8_t L = htable->L[i];

		for (int l = 0; l < L; ++l) {
			err = write_byte(stream, htable->V[i][l]);
			RETURN_IF(err);
		}
	}

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

int fill_scan(struct context *context, struct scan *scan)
{
	assert(context != NULL);
	assert(scan != NULL);

	scan->Ns = context->Nf;

	for (int j = 0, i = 0; i < 256; ++i) {
		if (context->component[i].H != 0) {
			scan->Cs[j++] = i;
		}
	}

	return RET_SUCCESS;
}

int produce_SOS(struct context *context, FILE *stream, struct scan *scan)
{
	int err;

	assert(context != NULL);
	assert(scan != NULL);

	err = write_marker(stream, 0xffda);
	RETURN_IF(err);

	uint8_t Ns = context->Nf;

	/* Number of image components in scan = Number of image components in frame */
	scan->Ns = Ns;

	// length = 2 (len) + 1 (Ns) + Ns * (1 (Cs) + 1 (Td, Ta)) + 1 (Ss) + 1 (Se) + 1 (Ah, Al) = 6 + 2 * Ns
	err = write_length(stream, 6 + 2 * Ns);
	RETURN_IF(err);

	for (int j = 0, i = 0; i < 256; ++i) {
		if (context->component[i].H != 0) {
			scan->Cs[j++] = i;
		}
	}

	err = write_byte(stream, Ns);
	RETURN_IF(err);

	for (int j = 0; j < Ns; ++j) {
		uint8_t Cs;
		uint8_t Td, Ta;

		Cs = scan->Cs[j];
		Td = context->component[Cs].Td;
		Ta = context->component[Cs].Ta;

		err = write_byte(stream, scan->Cs[j]);
		RETURN_IF(err);

		err = write_nibbles(stream, Td, Ta);
		RETURN_IF(err);
	}

	uint8_t Ss = 0;
	uint8_t Se = 63;
	uint8_t Ah = 0, Al = 0;

	err = write_byte(stream, Ss);
	RETURN_IF(err);
	err = write_byte(stream, Se);
	RETURN_IF(err);
	err = write_nibbles(stream, Ah, Al);
	RETURN_IF(err);

	return RET_SUCCESS;
}

int produce_EOI(FILE *stream)
{
	int err;

	err = write_marker(stream, 0xffd9);
	RETURN_IF(err);

	return RET_SUCCESS;
}

int write_macroblock(struct bits *bits, struct context *context, struct scan *scan)
{
	int err;

	assert(scan != NULL);
	assert(context != NULL);

	size_t seq_no = context->mblocks;

	size_t x = seq_no % context->m_x;
	size_t y = seq_no / context->m_x;

	/* for each component */
	for (int j = 0; j < scan->Ns; ++j) {
		uint8_t Cs = scan->Cs[j];
		uint8_t H = context->component[Cs].H;
		uint8_t V = context->component[Cs].V;

		/* for each 8x8 block */
		for (int v = 0; v < V; ++v) {
			for (int h = 0; h < H; ++h) {
				size_t block_x = x * H + h;
				size_t block_y = y * V + v;

				assert(block_x < context->component[Cs].b_x);

				size_t block_seq = block_y * context->component[Cs].b_x + block_x;

				struct int_block *int_block = &context->component[Cs].int_buffer[block_seq];

				/* differential DC coding */
				if (scan->last_block[Cs] != NULL) {
					int_block->c[0] -= scan->last_block[Cs]->c[0];
				}

				assert(int_block->c[0] >= -2047 && int_block->c[0] <= +2047);

				/* write block */
				err = write_block(bits, context, Cs, int_block);
				RETURN_IF(err);

				// revert back
				if (scan->last_block[Cs] != NULL) {
					int_block->c[0] += scan->last_block[Cs]->c[0];
				}

				scan->last_block[Cs] = int_block;
			}
		}
	}

	return RET_SUCCESS;
}

int write_macroblock_dry(struct context *context, struct scan *scan)
{
	int err;

	assert(scan != NULL);
	assert(context != NULL);

	size_t seq_no = context->mblocks;

	size_t x = seq_no % context->m_x;
	size_t y = seq_no / context->m_x;

	/* for each component */
	for (int j = 0; j < scan->Ns; ++j) {
		uint8_t Cs = scan->Cs[j];
		uint8_t H = context->component[Cs].H;
		uint8_t V = context->component[Cs].V;

		/* for each 8x8 block */
		for (int v = 0; v < V; ++v) {
			for (int h = 0; h < H; ++h) {
				size_t block_x = x * H + h;
				size_t block_y = y * V + v;

				assert(block_x < context->component[Cs].b_x);

				size_t block_seq = block_y * context->component[Cs].b_x + block_x;

				struct int_block *int_block = &context->component[Cs].int_buffer[block_seq];

				/* differential DC coding */
				if (scan->last_block[Cs] != NULL) {
					int_block->c[0] -= scan->last_block[Cs]->c[0];
				}

				assert(int_block->c[0] >= -2047 && int_block->c[0] <= +2047);

				/* write block */
				err = write_block_dry(context, Cs, int_block);
				RETURN_IF(err);

				// revert back
				if (scan->last_block[Cs] != NULL) {
					int_block->c[0] += scan->last_block[Cs]->c[0];
				}

				scan->last_block[Cs] = int_block;
			}
		}
	}

	return RET_SUCCESS;
}

const char *Tc_to_str[] = {
	[0] = "DC",
	[1] = "AC"
};

int write_ecs_dry(struct context *context, struct scan *scan)
{
	int err;

	size_t mblocks_total = context->m_x * context->m_y;

	/* reset the counter */
	context->mblocks = 0;

	for (int i = 0; i < 256; ++i) {
		scan->last_block[i] = NULL;
	}

	/* loop over macroblocks (dry run) */
	for (; context->mblocks < mblocks_total; context->mblocks++) {
		err = write_macroblock_dry(context, scan);
		RETURN_IF(err);
	}

	/* adapt codes */
	for (int j = 0; j < 2; ++j) {
		for (int i = 0; i < (context->Nf > 1 ? 2 : 1); ++i) {
			printf("Adapting Huffman table [%s][%i]...\n", Tc_to_str[j], i);

			err = adapt_huffman_table(&context->htable[j][i], &context->huffenc[j][i]);
			RETURN_IF(err);

			int err = conv_htable_to_hcode(&context->htable[j][i], &context->hcode[j][i]);
			RETURN_IF(err);
		}
	}

	return RET_SUCCESS;
}

int write_ecs(FILE *stream, struct context *context, struct scan *scan)
{
	int err;
	struct bits bits;

	init_bits(&bits, stream);

	size_t mblocks_total = context->m_x * context->m_y;

	/* reset the counter */
	context->mblocks = 0;

	for (int i = 0; i < 256; ++i) {
		scan->last_block[i] = NULL;
	}

	/* loop over macroblocks */
	for (; context->mblocks < mblocks_total; context->mblocks++) {
		err = write_macroblock(&bits, context, scan);
		RETURN_IF(err);
	}

	flush_bits(&bits);

	printf("Processed: %zu macroblocks\n", context->mblocks);

	return RET_SUCCESS;
}

int produce_codestream(struct context *context, FILE *stream, struct params *params)
{
	int err;

	/* SOI */
	err = produce_SOI(stream);
	RETURN_IF(err);

	/* DQT */
	err = produce_DQT(context, 0, stream); // Y
	RETURN_IF(err);
	if (context->Nf > 1) {
		err = produce_DQT(context, 1, stream); // Cb/Cr
		RETURN_IF(err);
	}

	/* SOF0 */
	err = produce_SOF0(context, stream);
	RETURN_IF(err);

	struct scan scan;

	err = fill_scan(context, &scan);
	RETURN_IF(err);

	// enable this by command line option
	if (params->optimize) {
		err = write_ecs_dry(context, &scan);
		RETURN_IF(err);
	}

	/* DHT */
	err = produce_DHT(context, 0, 0, stream); // DC Y
	RETURN_IF(err);
	err = produce_DHT(context, 1, 0, stream); // AC Y
	RETURN_IF(err);
	if (context->Nf > 1) {
		err = produce_DHT(context, 0, 1, stream); // DC Cb/Cr
		RETURN_IF(err);
		err = produce_DHT(context, 1, 1, stream); // AC Cb/Cr
		RETURN_IF(err);
	}

	/* SOS */
	err = produce_SOS(context, stream, &scan);
	RETURN_IF(err);

	/* loop over macroblocks */
	err = write_ecs(stream, context, &scan);
	RETURN_IF(err);

	/* EOI */
	err = produce_EOI(stream);
	RETURN_IF(err);

	return RET_SUCCESS;
}

int process_stream(FILE *i_stream, FILE *o_stream, struct params *params)
{
	int err;

	struct context *context = malloc(sizeof(struct context));

	err = init_context(context);
	RETURN_IF(err);

	err = prologue(context, i_stream, params);
	RETURN_IF(err);

	err = produce_codestream(context, o_stream, params);
	RETURN_IF(err);

	free_buffers(context);

	free(context);

	return RET_SUCCESS;
}

int main(int argc, char *argv[])
{
	struct params params;

	init_params(&params);

	int opt;

	while ((opt = getopt(argc, argv, "h:v:q:o:")) != -1) {
		switch (opt) {
			case 'h':
				params.H = atoi(optarg);
				break;
			case 'v':
				params.V = atoi(optarg);
				break;
			case 'q':
				params.q = atoi(optarg);
				break;
			case 'o':
				params.optimize = atoi(optarg);
				break;
			default:
				fprintf(stderr, "Usage: %s [-h factor] [-v factor] [-q quality] [-o value] input.{ppm|pgm} output.jpg\n",
					argv[0]);
				return 1;
		}
	}

	const char *i_path = optind + 0 < argc ? argv[optind + 0] : "Lenna.ppm";
	const char *o_path = optind + 1 < argc ? argv[optind + 1] : "output.jpg";

	FILE *i_stream = fopen(i_path, "r");
	FILE *o_stream = fopen(o_path, "w");

	if (i_stream == NULL) {
		fprintf(stderr, "fopen failure\n");
		return 1;
	}

	if (o_stream == NULL) {
		fprintf(stderr, "fopen failure\n");
		return 1;
	}

	int err = process_stream(i_stream, o_stream, &params);

	if (err) {
		fprintf(stderr, "Failure.\n");
	}

	fclose(o_stream);
	fclose(i_stream);

	return 0;
}
