#ifndef JPEG_FRAME_H
#define JPEG_FRAME_H

#include <stddef.h>
#include <stdint.h>
#include "common.h"

struct frame {
	uint8_t components;
	uint16_t Y, X;
	size_t size_x, size_y;
	uint8_t precision;

	float *data;
};

int frame_create(struct context *context, struct frame *frame);

void frame_destroy(struct frame *frame);

int frame_to_rgb(struct frame *frame);

int write_frame(struct frame *frame, const char *path);

int read_frame_header(struct frame *frame, FILE *stream);

int frame_create_empty(struct context *context, struct frame *frame);

int read_frame_body(struct frame *frame, FILE *stream);

void transform_frame_to_components(struct context *context, struct frame *frame);

int frame_to_ycc(struct frame *frame);

#endif
