#ifndef JPEG_IMGPROC_H
#define JPEG_IMGPROC_H

#include "common.h"
#include "coeffs.h"

/* for each component: remove quantization */
int dequantize(struct context *context);

int quantize(struct context *context);

void dequantize_block(struct int_block *int_block, struct flt_block *flt_block, struct qtable *qtable);

int inverse_dct(struct context *context);

int forward_dct(struct context *context);

int conv_blocks_to_frame(struct context *context);

int conv_frame_to_blocks(struct context *context);

#endif
