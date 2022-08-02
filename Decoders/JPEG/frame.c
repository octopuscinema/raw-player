#include <assert.h>
#include <inttypes.h>
#include <stdlib.h>
#include <stdio.h>
#include <math.h>
#include <arpa/inet.h>
#include <ctype.h>
#include "frame.h"
#include "common.h"

void frame_destroy(struct frame *frame)
{
	free(frame->data);
}

int frame_create_empty(struct context *context, struct frame *frame)
{
	assert(context != NULL);
	assert(frame != NULL);

	size_t size_x = ceil_div(frame->X, 8 * context->max_H) * 8 * context->max_H;
	size_t size_y = ceil_div(frame->Y, 8 * context->max_V) * 8 * context->max_V;

	frame->size_x = size_x;
	frame->size_y = size_y;

	// alloc frame->data[]
	frame->data = malloc(sizeof(float) * frame->components * size_x * size_y);

	if (frame->data == NULL) {
		return RET_FAILURE_MEMORY_ALLOCATION;
	}

	return RET_SUCCESS;
}

// context->component[].frame_buffer[] => frame->data[]
void transform_components_to_frame(struct context *context, struct frame *frame)
{
	assert(context != NULL);
	assert(frame != NULL);

	size_t size_x = frame->size_x;
	size_t size_y = frame->size_y;

	// component id
	int compno = 0;

	for (int i = 0; i < 256; ++i) {
		if (context->component[i].frame_buffer != NULL) {
			size_t b_x = context->component[i].b_x;
			size_t b_y = context->component[i].b_y;

			size_t c_x = b_x * 8;
			size_t c_y = b_y * 8;

			size_t step_x = size_x / c_x;
			size_t step_y = size_y / c_y;

			float *buffer = context->component[i].frame_buffer;

			// iterate over component raster (smaller than frame raster)
			for (size_t y = 0; y < c_y; ++y) {
				for (size_t x = 0; x < c_x; ++x) {
					// (i,y,x) to index component
					// (compno,step*y,step*x) to index frame

					float px = buffer[y * c_x + x];

					// copy patch
					for (size_t yy = 0; yy < step_y; ++yy) {
						for (size_t xx = 0; xx < step_x; ++xx) {
							frame->data[(step_y * y + yy) * size_x * frame->components + frame->components * (step_x * x + xx) + compno] = px;
						}
					}
				}
			}

			compno++;
		}
	}
}

void transform_frame_to_components(struct context *context, struct frame *frame)
{
	assert(context != NULL);
	assert(frame != NULL);

	size_t size_x = frame->size_x;
	size_t size_y = frame->size_y;

	// component id
	int compno = 0;

	for (int i = 0; i < 256; ++i) {
		if (context->component[i].frame_buffer != NULL) {
			size_t b_x = context->component[i].b_x;
			size_t b_y = context->component[i].b_y;

			size_t c_x = b_x * 8;
			size_t c_y = b_y * 8;

			size_t step_x = size_x / c_x;
			size_t step_y = size_y / c_y;

			float *buffer = context->component[i].frame_buffer;

			// iterate over component raster (smaller than frame raster)
			for (size_t y = 0; y < c_y; ++y) {
				for (size_t x = 0; x < c_x; ++x) {
					// (i,y,x) to index component
					// (compno,step*y,step*x) to index frame

					float px = 0.f;

					// copy patch
					for (size_t yy = 0; yy < step_y; ++yy) {
						for (size_t xx = 0; xx < step_x; ++xx) {
							px += frame->data[(step_y * y + yy) * size_x * frame->components + frame->components * (step_x * x + xx) + compno];
						}
					}

					px /= step_y * step_x;

					buffer[y * c_x + x] = px;
				}
			}

			compno++;
		}
	}
}

int frame_create(struct context *context, struct frame *frame)
{
	assert(context != NULL);
	assert(frame != NULL);

	int err;

	frame->components = context->Nf;
	frame->Y = context->Y;
	frame->X = context->X;
	frame->precision = context->P;

	err = frame_create_empty(context, frame);
	RETURN_IF(err);

	transform_components_to_frame(context, frame);

	return RET_SUCCESS;
}

int frame_to_ycc(struct frame *frame)
{
	assert(frame != NULL);

	int shift = 1 << (frame->precision - 1);

	switch (frame->components) {
		case 3:
			for (size_t y = 0; y < frame->Y; ++y) {
				for (size_t x = 0; x < frame->X; ++x) {
					float R = frame->data[y * frame->size_x * 3 + x * 3 + 0];
					float G = frame->data[y * frame->size_x * 3 + x * 3 + 1];
					float B = frame->data[y * frame->size_x * 3 + x * 3 + 2];

					float Y  = 0.299 * R + 0.587 * G + 0.114 * B;
					float Cb = - 0.1687 * R - 0.3313 * G + 0.5 * B + shift;
					float Cr = 0.5 * R - 0.4187 * G - 0.0813 * B + shift;

					frame->data[y * frame->size_x * 3 + x * 3 + 0] = Y;
					frame->data[y * frame->size_x * 3 + x * 3 + 1] = Cb;
					frame->data[y * frame->size_x * 3 + x * 3 + 2] = Cr;
				}
			}
			break;
		case 1:
			/* nothing to do */
			break;
		default:
			abort();
	}

	return RET_SUCCESS;
}

int frame_to_rgb(struct frame *frame)
{
	assert(frame != NULL);

	int shift = 1 << (frame->precision - 1);
	int denom = 1 << frame->precision;

	switch (frame->components) {
		case 4:
			for (size_t y = 0; y < frame->Y; ++y) {
				for (size_t x = 0; x < frame->X; ++x) {
					float Y_ = frame->data[y * frame->size_x * 4 + x * 4 + 0];
					float Cb = frame->data[y * frame->size_x * 4 + x * 4 + 1];
					float Cr = frame->data[y * frame->size_x * 4 + x * 4 + 2];
					float K  = frame->data[y * frame->size_x * 4 + x * 4 + 3];

					float C = Y_ + 1.402 * (Cr - shift);
					float M = Y_ - 0.34414 * (Cb - shift) - 0.71414 * (Cr - shift);
					float Y = Y_ + 1.772 * (Cb - shift);

					float R = K - (C * K) / denom;
					float G = K - (M * K) / denom;
					float B = K - (Y * K) / denom;

					frame->data[y * frame->size_x * 4 + x * 4 + 0] = R;
					frame->data[y * frame->size_x * 4 + x * 4 + 1] = G;
					frame->data[y * frame->size_x * 4 + x * 4 + 2] = B;
					frame->data[y * frame->size_x * 4 + x * 4 + 3] = 0xff;
				}
			}
			break;
		case 3:
			for (size_t y = 0; y < frame->Y; ++y) {
				for (size_t x = 0; x < frame->X; ++x) {
					float Y  = frame->data[y * frame->size_x * 3 + x * 3 + 0];
					float Cb = frame->data[y * frame->size_x * 3 + x * 3 + 1];
					float Cr = frame->data[y * frame->size_x * 3 + x * 3 + 2];

					float R = Y + 1.402 * (Cr - shift);
					float G = Y - 0.34414 * (Cb - shift) - 0.71414 * (Cr - shift);
					float B = Y + 1.772 * (Cb - shift);

					frame->data[y * frame->size_x * 3 + x * 3 + 0] = R;
					frame->data[y * frame->size_x * 3 + x * 3 + 1] = G;
					frame->data[y * frame->size_x * 3 + x * 3 + 2] = B;
				}
			}
			break;
		case 1:
			/* nothing to do */
			break;
		default:
			abort();
	}

	return RET_SUCCESS;
}

size_t convert_maxval_to_sample_size(int maxval)
{
	assert(maxval > 0);

	if (maxval <= UINT8_MAX)
		return sizeof(uint8_t);
	if (maxval <= UINT16_MAX)
		return sizeof(uint16_t);

	/* not supported */
	return 0;
}

uint8_t floor_log2(unsigned n)
{
	unsigned r = 0;

	while (n >>= 1) {
		r++;
	}

	return r;
}

uint8_t convert_maxval_to_precision(int maxval)
{
	assert(maxval > 0);

	return floor_log2((unsigned)maxval) + 1;
}

int read_frame_body(struct frame *frame, FILE *stream)
{
	assert(frame != NULL);

	uint8_t Nf = frame->components;
	int maxval = (1 << frame->precision) - 1;
	size_t sample_size = convert_maxval_to_sample_size(maxval);
	int components = frame->components;
	size_t width = (size_t)frame->X;
	size_t height = (size_t)frame->Y;
	size_t line_size = sample_size * components * width;

	void *line = malloc(line_size);

	if (line == NULL) {
		return RET_FAILURE_MEMORY_ALLOCATION;
	}

	for (size_t y = 0; y < height; ++y) {
		if (fread(line, 1, line_size, stream) < line_size) {
			free(line);
			return RET_FAILURE_FILE_IO;
		}
		switch (sample_size) {
			case sizeof(uint8_t): {
				uint8_t *line_ = line;
				for (size_t x = 0; x < width; ++x) {
					for (int c = 0; c < components; ++c) {
						frame->data[y * frame->size_x * Nf + x * Nf + c] = (float)*line_++;
					}
				}
				/* padding */
				for (size_t x = width; x < frame->size_x; ++x) {
					for (int c = 0; c < components; ++c) {
						frame->data[y * frame->size_x * Nf + x * Nf + c] =
							frame->data[y * frame->size_x * Nf + (width - 1) * Nf + c];
					}
				}
				break;
			}
			case sizeof(uint16_t): {
				uint16_t *line_ = line;
				for (size_t x = 0; x < width; ++x) {
					for (int c = 0; c < components; ++c) {
						frame->data[y * frame->size_x * Nf + x * Nf + c] = (float)ntohs(*line_++);
					}
				}
				/* padding */
				for (size_t x = width; x < frame->size_x; ++x) {
					for (int c = 0; c < components; ++c) {
						frame->data[y * frame->size_x * Nf + x * Nf + c] =
							frame->data[y * frame->size_x * Nf + (width - 1) * Nf + c];
					}
				}
				break;
			}
			default:
				return RET_FAILURE_LOGIC_ERROR;
		}
	}
	/* padding */
	for (size_t y = height; y < frame->size_y; ++y) {
		for (size_t x = 0; x < frame->size_x; ++x) {
			for (int c = 0; c < components; ++c) {
				frame->data[y * frame->size_x * Nf + x * Nf + c] =
					frame->data[(height - 1) * frame->size_x * Nf + x * Nf + c];
			}
		}
	}

	free(line);

	return RET_SUCCESS;
}

int write_frame_body(struct frame *frame, int components, FILE *stream)
{
	assert(frame != NULL);

	uint8_t Nf = frame->components;
	int maxval = (1 << frame->precision) - 1;
	size_t sample_size = convert_maxval_to_sample_size(maxval);
	size_t width = (size_t)frame->X;
	size_t height = (size_t)frame->Y;
	size_t line_size = sample_size * components * width;

	void *line = malloc(line_size);

	if (line == NULL) {
		return RET_FAILURE_MEMORY_ALLOCATION;
	}

	for (size_t y = 0; y < height; ++y) {
		switch (sample_size) {
			case sizeof(uint8_t): {
				uint8_t *line_ = line;
				for (size_t x = 0; x < width; ++x) {
					for (int c = 0; c < components; ++c) {
						float sample = roundf(frame->data[y * frame->size_x * Nf + x * Nf + c]);
						*line_++ = (uint8_t)clamp(0, (int)sample, maxval);
					}
				}
				break;
			}
			case sizeof(uint16_t): {
				uint16_t *line_ = line;
				for (size_t x = 0; x < width; ++x) {
					for (int c = 0; c < components; ++c) {
						float sample = roundf(frame->data[y * frame->size_x * Nf + x * Nf + c]);
						*line_++ = htons((uint16_t)clamp(0, (int)sample, maxval));
					}
				}
				break;
			}
			default:
				free(line);
				return RET_FAILURE_LOGIC_ERROR;
		}
		/* write line */
		if (fwrite(line, 1, line_size, stream) < line_size) {
			free(line);
			return RET_FAILURE_FILE_IO;
		}
	}

	free(line);

	return RET_SUCCESS;
}

int write_frame_header(struct frame *frame, int components, FILE *stream)
{
	assert(frame != NULL);

	int maxval = (1 << frame->precision) - 1;

	switch (components) {
		case 3:
			if (fprintf(stream, "P6\n%" PRIu16 " %" PRIu16 "\n%i\n", frame->X, frame->Y, maxval) < 0) {
				return RET_FAILURE_FILE_IO;
			}
			break;
		case 1:
			if (fprintf(stream, "P5\n%" PRIu16 " %" PRIu16 "\n%i\n", frame->X, frame->Y, maxval) < 0) {
				return RET_FAILURE_FILE_IO;
			}
			break;
		default:
			return RET_FAILURE_FILE_UNSUPPORTED;
	}

	return RET_SUCCESS;
}

int stream_skip_comment(FILE *stream)
{
	int c;

	/* look ahead for a comment, ungetc */
	while ((c = getc(stream)) == '#') {
		char com[4096];
		if (NULL == fgets(com, 4096, stream))
			return RET_FAILURE_FILE_IO;
	}

	if (EOF == ungetc(c, stream))
		return RET_FAILURE_FILE_IO;

	return RET_SUCCESS;
}

int read_frame_header(struct frame *frame, FILE *stream)
{
	char magic[2];
	int maxval;
	uint16_t height, width;
	uint8_t precision;
	uint8_t components;

	if (fscanf(stream, "%c%c ", magic, magic + 1) != 2) {
		return RET_FAILURE_FILE_IO;
	}

	if (magic[0] != 'P') {
		return RET_FAILURE_FILE_UNSUPPORTED;
	}

	switch (magic[1]) {
		case '5':
			components = 1;
			break;
		case '6':
			components = 3;
			break;
		default:
			return RET_FAILURE_FILE_UNSUPPORTED;
	}

	if (stream_skip_comment(stream)) {
		return RET_FAILURE_FILE_IO;
	}

	if (fscanf(stream, " %" SCNu16 " ", &width) != 1) {
		return RET_FAILURE_FILE_IO;
	}

	if (stream_skip_comment(stream)) {
		return RET_FAILURE_FILE_IO;
	}

	if (fscanf(stream, " %" SCNu16 " ", &height) != 1) {
		return RET_FAILURE_FILE_IO;
	}

	if (stream_skip_comment(stream)) {
		return RET_FAILURE_FILE_IO;
	}

	if (fscanf(stream, " %i", &maxval) != 1) {
		return RET_FAILURE_FILE_IO;
	}

	precision = convert_maxval_to_precision(maxval);

	if (precision > 16) {
		return RET_FAILURE_FILE_UNSUPPORTED;
	}

	if (stream_skip_comment(stream)) {
		return RET_FAILURE_FILE_IO;
	}

	if (!isspace(fgetc(stream))) {
		return RET_FAILURE_FILE_UNSUPPORTED;
	}

	/* fill the struct */
	assert(frame != NULL);

	frame->components = components;
	frame->Y = height;
	frame->X = width;
	frame->precision = precision;

	return RET_SUCCESS;
}

int write_frame_components(struct frame *frame, int components, const char *path)
{
	int err;

	FILE *stream = fopen(path, "w");

	if (stream == NULL) {
		return RET_FAILURE_FILE_OPEN;
	}

	err = write_frame_header(frame, components, stream);

	if (err) {
		goto end;
	}

	err = write_frame_body(frame, components, stream);

end:
	fclose(stream);

	return err;
}

int write_frame(struct frame *frame, const char *path)
{
	assert(frame != NULL);

	int err;

	switch (frame->components) {
		case 4:
		case 3:
			err = write_frame_components(frame, 3, path != NULL ? path : "output.ppm");
			break;
		case 1:
			err = write_frame_components(frame, 1, path != NULL ? path : "output.pgm");
			break;
		default:
			abort();
	}

	return err;
}
