#include <stddef.h>
#include <stdlib.h>
#include <assert.h>
#include <string.h>
#include "common.h"
#include "mjpeg.h"
#include "huffman.h"
#include "coeffs.h"

int init_qtable(struct qtable *qtable)
{
	assert(qtable != NULL);

	qtable->Pq = 0;

	for (int i = 0; i < 64; ++i) {
		qtable->Q[i] = 1;
	}

	return RET_SUCCESS;
}

int init_component(struct component *component)
{
	assert(component != NULL);

	component->H = 0;
	component->V = 0;

	component->Tq = 0;

	component->Td = 0;
	component->Ta = 0;

	component->b_x = 0;
	component->b_y = 0;

	component->int_buffer = NULL;
	component->flt_buffer = NULL;

	component->frame_buffer = NULL;

	return RET_SUCCESS;
}

int init_htable(struct htable *htable)
{
	assert(htable != NULL);

	for (int i = 0; i < 16; ++i) {
		htable->L[i] = 0;
	}

	for (int i = 0; i < 16; ++i) {
		for (int j = 0; j < 255; ++j) {
			htable->V[i][j] = 0;
		}
	}

	return RET_SUCCESS;
}

int init_context(struct context *context)
{
	assert(context != NULL);

	for (int i = 0; i < 4; ++i) {
		init_qtable(&context->qtable[i]);
	}

	context->P = 0;

	context->Y = 0;
	context->X = 0;

	context->Nf = 0;

	for (int i = 0; i < 256; ++i) {
		init_component(&context->component[i]);
	}

	for (int j = 0; j < 2; ++j) {
		for (int i = 0; i < 4; ++i) {
			init_htable(&context->htable[j][i]);

			init_huffenc(&context->huffenc[j][i]);
		}
	}

	/* implicit MJPEG tables */
	context->htable[0][0] = mjpg_htable_0_0;
	context->htable[0][1] = mjpg_htable_0_1;
	context->htable[1][0] = mjpg_htable_1_0;
	context->htable[1][1] = mjpg_htable_1_1;

	conv_htable_to_hcode(&context->htable[0][0], &context->hcode[0][0]);
	conv_htable_to_hcode(&context->htable[0][1], &context->hcode[0][1]);
	conv_htable_to_hcode(&context->htable[1][0], &context->hcode[1][0]);
	conv_htable_to_hcode(&context->htable[1][1], &context->hcode[1][1]);

	context->Ri = 0;

	context->m_x = 0;
	context->m_y = 0;

	context->mblocks = 0;

	return RET_SUCCESS;
}

size_t ceil_div(size_t n, size_t d)
{
	return (n + (d - 1)) / d;
}

int alloc_buffers(struct component *component, size_t size)
{
	// redefine component (multiple definitions of the same component inside SOF marker)
	free(component->int_buffer);
	free(component->flt_buffer);
	free(component->frame_buffer);

	component->int_buffer = malloc(sizeof(struct int_block) * size);

	if (component->int_buffer == NULL) {
		return RET_FAILURE_MEMORY_ALLOCATION;
	}

	memset(component->int_buffer, 0, sizeof(struct int_block) * size);

	component->flt_buffer = malloc(sizeof(struct flt_block) * size);

	if (component->flt_buffer == NULL) {
		return RET_FAILURE_MEMORY_ALLOCATION;
	}

	component->frame_buffer = malloc(sizeof(float) * 64 * size);

	if (component->frame_buffer == NULL) {
		return RET_FAILURE_MEMORY_ALLOCATION;
	}

	return RET_SUCCESS;
}

void free_buffers(struct context *context)
{
	for (int i = 0; i < 256; ++i) {
		free(context->component[i].int_buffer);
		free(context->component[i].flt_buffer);

		free(context->component[i].frame_buffer);
	}
}

int compute_no_blocks_and_alloc_buffers(struct context *context)
{
	assert(context != NULL);

	int err;

	uint16_t Y, X;
	uint8_t max_H, max_V;

	Y = context->Y;
	X = context->X;

	max_H = context->max_H;
	max_V = context->max_V;

	context->m_x = ceil_div(X, 8 * max_H);
	context->m_y = ceil_div(Y, 8 * max_V);

	printf("Expecting %zu macroblocks\n", context->m_x * context->m_y);

	for (int i = 0; i < 256; ++i) {
		uint8_t H, V;
		H = context->component[i].H;
		V = context->component[i].V;
		if (H != 0) {
			size_t b_x = ceil_div(X, 8 * max_H) * H;
			size_t b_y = ceil_div(Y, 8 * max_V) * V;

			context->component[i].b_x = b_x;
			context->component[i].b_y = b_y;

			printf("C = %i: %zu blocks (x=%zu y=%zu)\n", i, b_x * b_y, b_x, b_y);

			err = alloc_buffers(&context->component[i], b_x * b_y);
			RETURN_IF(err);
		}
	}

	return RET_SUCCESS;
}

int clamp(int min, int val, int max)
{
	if (val < min) {
		return min;
	}

	if (val > max) {
		return max;
	}

	return val;
}

/* Before starting the procedure, the values of FREQ are collected for V = 0 to 255 and the FREQ value for V = 256 is set to 1 to reserve one code point.  */
void init_huffenc(struct huffenc *huffenc)
{
	assert(huffenc != NULL);

	for (int i = 0; i < 256; ++i) {
		huffenc->freq[i] = 0;
	}

	huffenc->freq[256] = 1;

	for (int i = 0; i < 257; ++i) {
		huffenc->codesize[i] = 0;
		huffenc->others[i] = -1;
	}
}
