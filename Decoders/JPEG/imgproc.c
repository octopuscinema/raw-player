#include <stddef.h>
#include <assert.h>
#include <stdint.h>
#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include "imgproc.h"
#include "coeffs.h"

void dequantize_block(struct int_block *int_block, struct flt_block *flt_block, struct qtable *qtable)
{
	assert(int_block != NULL);
	assert(flt_block != NULL);
	assert(qtable != NULL);

	for (int j = 0; j < 64; ++j) {
		flt_block->c[j] = (float)(int_block->c[j] * (int32_t)qtable->Q[j]);
	}
}

void quantize_block(struct int_block *int_block, struct flt_block *flt_block, struct qtable *qtable)
{
	assert(int_block != NULL);
	assert(flt_block != NULL);
	assert(qtable != NULL);

	for (int j = 0; j < 64; ++j) {
		int_block->c[j] = (int32_t)roundf(flt_block->c[j] / (float)qtable->Q[j]);
	}
}

int dequantize(struct context *context)
{
	assert(context != NULL);

	for (int i = 0; i < 256; ++i) {
		if (context->component[i].int_buffer != NULL) {
			printf("Dequantizing component %i...\n", i);

			size_t blocks = context->component[i].b_x * context->component[i].b_y;

			uint8_t Tq = context->component[i].Tq;
			struct qtable *qtable = &context->qtable[Tq];

			// for each block, for each coefficient, c[] *= Q[]
			for (size_t b = 0; b < blocks; ++b) {
				struct int_block *int_block = &context->component[i].int_buffer[b];
				struct flt_block *flt_block = &context->component[i].flt_buffer[b];

				dequantize_block(int_block, flt_block, qtable);
			}
		}
	}

	return RET_SUCCESS;
}

int quantize(struct context *context)
{
	assert(context != NULL);

	for (int i = 0; i < 256; ++i) {
		if (context->component[i].int_buffer != NULL) {
			printf("Quantizing component %i...\n", i);

			size_t blocks = context->component[i].b_x * context->component[i].b_y;

			uint8_t Tq = context->component[i].Tq;
			struct qtable *qtable = &context->qtable[Tq];

			// for each block, for each coefficient, c[] *= Q[]
			for (size_t b = 0; b < blocks; ++b) {
				struct int_block *int_block = &context->component[i].int_buffer[b];
				struct flt_block *flt_block = &context->component[i].flt_buffer[b];

				quantize_block(int_block, flt_block, qtable);
			}
		}
	}

	return RET_SUCCESS;
}

static float C(int u)
{
	if (u == 0) {
		return 1.f / sqrtf(2.f);
	}

	return 1.f;
}

float lut[8][8];

void init_lut()
{
	for (int x = 0; x < 8; ++x) {
		for (int u = 0; u < 8; ++u) {
			lut[x][u] = 0.5f * C(u) * cosf((2 * x + 1) * u * M_PI / 16);
		}
	}
}

void idct1(const float in[8], float out[8], size_t stride)
{
	for (int x = 0; x < 8; ++x) {
		float s = 0.f;

		for (int u = 0; u < 8; ++u) {
			s += in[u * stride] * lut[x][u];
		}

		out[x * stride] = s;
	}
}

void fdct1(const float in[8], float out[8], size_t stride)
{
	for (int u = 0; u < 8; ++u) {
		float s = 0.f;

		for (int x = 0; x < 8; ++x) {
			s += in[x * stride] * lut[x][u];
		}

		out[u * stride] = s;
	}
}

void idct(struct flt_block *flt_block)
{
	static int init = 0;

	// init look-up table
	if (init == 0) {
		init_lut();
		init = 1;
	}

	struct flt_block b;

	for (int y = 0; y < 8; ++y) {
		idct1(&flt_block->c[y * 8], &b.c[y * 8], 1);
	}

	for (int x = 0; x < 8; ++x) {
		idct1(&b.c[x], &flt_block->c[x], 8);
	}
}

void fdct(struct flt_block *flt_block)
{
	static int init = 0;

	// init look-up table
	if (init == 0) {
		init_lut();
		init = 1;
	}

	struct flt_block b;

	for (int y = 0; y < 8; ++y) {
		fdct1(&flt_block->c[y * 8], &b.c[y * 8], 1);
	}

	for (int x = 0; x < 8; ++x) {
		fdct1(&b.c[x], &flt_block->c[x], 8);
	}
}

int inverse_dct(struct context *context)
{
	assert(context != NULL);

	/* precision */
	uint8_t P = context->P;
	int shift = 1 << (P - 1);

	for (int i = 0; i < 256; ++i) {
		if (context->component[i].int_buffer != NULL) {
			printf("IDCT on component %i...\n", i);

			size_t blocks = context->component[i].b_x * context->component[i].b_y;

			for (size_t b = 0; b < blocks; ++b) {
				struct flt_block *flt_block = &context->component[i].flt_buffer[b];

				idct(flt_block);

				// level shift
				for (int j = 0; j < 64; ++j) {
					flt_block->c[j] += shift;
				}
			}
		}
	}

	return RET_SUCCESS;
}

int forward_dct(struct context *context)
{
	assert(context != NULL);

	/* precision */
	uint8_t P = context->P;
	int shift = 1 << (P - 1);

	for (int i = 0; i < 256; ++i) {
		if (context->component[i].int_buffer != NULL) {
			printf("FDCT on component %i...\n", i);

			size_t blocks = context->component[i].b_x * context->component[i].b_y;

			for (size_t b = 0; b < blocks; ++b) {
				struct flt_block *flt_block = &context->component[i].flt_buffer[b];

				// level shift
				for (int j = 0; j < 64; ++j) {
					flt_block->c[j] -= shift;
				}

				fdct(flt_block);
			}
		}
	}

	return RET_SUCCESS;
}

/* convert floating-point blocks to frame buffers (for each component) */
int conv_blocks_to_frame(struct context *context)
{
	assert(context != NULL);

	for (int i = 0; i < 256; ++i) {
		if (context->component[i].frame_buffer != NULL) {
			printf("converting component %i...\n", i);

			float *buffer = context->component[i].frame_buffer;

			size_t b_x = context->component[i].b_x;
			size_t b_y = context->component[i].b_y;

			for (size_t y = 0; y < b_y; ++y) {
				for (size_t x = 0; x < b_x; ++x) {
					/* copy from... */
					struct flt_block *flt_block = &context->component[i].flt_buffer[y * b_x + x];

					for (int v = 0; v < 8; ++v) {
						for (int u = 0; u < 8; ++u) {
							buffer[y * b_x * 8 * 8 + v * b_x * 8 + x * 8 + u] = flt_block->c[v * 8 + u];
						}
					}
				}
			}
		}
	}

	return RET_SUCCESS;
}

int conv_frame_to_blocks(struct context *context)
{
	assert(context != NULL);

	for (int i = 0; i < 256; ++i) {
		if (context->component[i].frame_buffer != NULL) {
			printf("converting component %i...\n", i);

			float *buffer = context->component[i].frame_buffer;

			size_t b_x = context->component[i].b_x;
			size_t b_y = context->component[i].b_y;

			for (size_t y = 0; y < b_y; ++y) {
				for (size_t x = 0; x < b_x; ++x) {
					/* copy to... */
					struct flt_block *flt_block = &context->component[i].flt_buffer[y * b_x + x];

					for (int v = 0; v < 8; ++v) {
						for (int u = 0; u < 8; ++u) {
							flt_block->c[v * 8 + u] = buffer[y * b_x * 8 * 8 + v * b_x * 8 + x * 8 + u];
						}
					}
				}
			}
		}
	}

	return RET_SUCCESS;
}
