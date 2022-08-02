#ifndef JPEG_COEFFS_H
#define JPEG_COEFFS_H

#include <stdint.h>
#include "common.h"
#include "io.h"

/* useful for quantized coefficients */
struct int_block {
	int32_t c[64];
};

/* useful for floating-point DCT */
struct flt_block {
	float c[64];
};

int read_block(struct bits *bits, struct context *context, uint8_t Cs, struct int_block *int_block);

int write_block(struct bits *bits, struct context *context, uint8_t Cs, struct int_block *int_block);

int write_block_dry(struct context *context, uint8_t Cs, struct int_block *int_block);

#endif
