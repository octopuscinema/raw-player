#ifndef JPEG_HUFFMAN_H
#define JPEG_HUFFMAN_H

#include <stddef.h>
#include <stdint.h>
#include "common.h"
#include "io.h"

/* represents Huffman code
 */
struct vlc {
	uint16_t code;
	size_t size;
};

int init_vlc(struct vlc *vlc);

int vlc_add_bit(struct vlc *vlc, uint16_t bit);

int vlc_remove_bit(struct vlc *vlc, uint16_t *bit);

int generate_size_table(struct htable *htable, struct hcode *hcode);

int generate_code_table(struct htable *htable, struct hcode *hcode);

int order_codes(struct htable *htable, struct hcode *hcode);

int conv_htable_to_hcode(struct htable *htable, struct hcode *hcode);

/*
 * query if the code is present in htable/hcode, and return its value
 */
int query_code(struct vlc *vlc, struct hcode *hcode, uint8_t *value);

int read_code(struct bits *bits, struct hcode *hcode, uint8_t *value);

int write_code(struct bits *bits, struct hcode *hcode, uint8_t value);

int read_extra_bits(struct bits *bits, uint8_t count, uint16_t *value);

int write_extra_bits(struct bits *bits, uint8_t count, uint16_t value);

/*
 * adaptive Huffman
 */
int adapt_huffman_table(struct htable *htable, struct huffenc *huffenc);

#endif
