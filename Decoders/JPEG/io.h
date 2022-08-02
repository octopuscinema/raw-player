#ifndef JPEG_IO_H
#define JPEG_IO_H

#include <stdio.h>
#include <stdint.h>

struct bits {
	uint8_t byte;
	size_t count;
	FILE *stream;
};

int init_bits(struct bits *bits, FILE *stream);

/* F.2.2.5 The NEXTBIT procedure */
int next_bit(struct bits *bits, uint8_t *bit);

int put_bit(struct bits *bits, uint8_t bit);

/* align to byte boundary */
int flush_bits(struct bits *bits);

int read_nibbles(FILE *stream, uint8_t *first, uint8_t *second);

int write_nibbles(FILE *stream, uint8_t first, uint8_t second);

int read_byte(FILE *stream, uint8_t *byte);

int write_byte(FILE *stream, uint8_t byte);

int read_word(FILE *stream, uint16_t *word);

int write_word(FILE *stream, uint16_t word);

int read_length(FILE *stream, uint16_t *len);

int write_length(FILE *stream, uint16_t len);

int skip_segment(FILE *stream, uint16_t len);

int read_marker(FILE *stream, uint16_t *marker);

int write_marker(FILE *stream, uint16_t marker);

/* read entropy-coded segment byte */
int read_ecs_byte(FILE *stream, uint8_t *byte);

int write_ecs_byte(FILE *stream, uint8_t byte);

#endif
