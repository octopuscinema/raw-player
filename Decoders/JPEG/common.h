#ifndef JPEG_COMMON_H
#define JPEG_COMMON_H

#include <stddef.h>
#include <stdint.h>

/**
 * \brief Error codes
 *
 * The standard (C89, 6.1.3.3 Enumeration constants) states that
 * an identifier declared as an enumeration constant has type \c int.
 * Therefore, it is fine if the function returning these constants
 * has return type \c int.
 */
enum {
	/* 0x0000 successful completion */
	RET_SUCCESS                   = 0x0000, /**< success */
	/* 0x1xxx input/output errors */
	RET_FAILURE_FILE_IO           = 0x1000, /**< I/O error */
	RET_FAILURE_FILE_UNSUPPORTED  = 0x1001, /**< unsupported feature or file type */
	RET_FAILURE_FILE_OPEN         = 0x1002, /**< file open failure */
	RET_FAILURE_FILE_SEEK         = 0x1003,
	/* 0x2xxx memory errors */
	RET_FAILURE_MEMORY_ALLOCATION = 0x2000, /**< unable to allocate dynamic memory */
	/* 0x3xxx general exceptions */
	RET_FAILURE_LOGIC_ERROR       = 0x3000, /**< faulty logic within the program */
	RET_FAILURE_OVERFLOW_ERROR    = 0x3001, /**< result is too large for the destination type */
	/* 0x4xxx other */
	RET_FAILURE_NO_MORE_DATA      = 0x4000,
	RET_LAST
};

/* zig-zag scan to raster scan */
static const uint8_t zigzag[64] = {
	 0,  1,  8, 16,  9,  2,  3, 10,
	17, 24, 32, 25, 18, 11,  4,  5,
	12, 19, 26, 33, 40, 48, 41, 34,
	27, 20, 13,  6,  7, 14, 21, 28,
	35, 42, 49, 56, 57, 50, 43, 36,
	29, 22, 15, 23, 30, 37, 44, 51,
	58, 59, 52, 45, 38, 31, 39, 46,
	53, 60, 61, 54, 47, 55, 62, 63
};

#define RETURN_IF(err) \
	do { \
		if (err) { \
			return (err); \
		} \
	} while (0)

struct qtable {
	/* precision: Value 0 indicates 8-bit Qk values; value 1 indicates 16-bit Qk values. */
	uint8_t Pq;
	/* elements: in raster scan order */
	uint16_t Q[64];
};

struct component {
	/* Horizontal sampling factor, Vertical sampling factor */
	uint8_t H, V;
	/* Quantization table destination selector */
	uint8_t Tq;

	/* DC entropy coding table destination selector
	 * AC entropy coding table destination selector */
	uint8_t Td, Ta;

	/* blocks horizontally and vertically */
	size_t b_x, b_y;

	/* blocks of 64 integers */
	struct int_block *int_buffer;

	/* blocks of 64 floats */
	struct flt_block *flt_buffer;

	/* raster image */
	float *frame_buffer;
};

/*
 * This reflects DHT segment (B.2.4.2)
 */
struct htable {
	/* Number of Huffman codes of length i */
	uint8_t L[16];

	/*  Value associated with each Huffman code */
	uint8_t V[16][255];
};

/*
 * This reflects Annex C
 */
struct hcode {
	/* unrolled htable.V[] */
	uint8_t huff_val[16 * 255];

	/* contains a list of code lengths */
	size_t huff_size[256];
	/*  contains the Huffman codes corresponding to those lengths */
	uint16_t huff_code[256];

	/* the index of the last entry in the table */
	size_t last_k;

	/* EHUFCO and EHUFSI, are created by reordering the codes specified by
	 * HUFFCODE and HUFFSIZE according to the symbol values assigned to each code
	 */
	uint16_t e_huf_co[256];
	size_t e_huf_si[256];
};

/* K.2 A procedure for generating the lists which specify a Huffman code table */
struct huffenc {
	size_t freq[257];
	size_t codesize[257];
	int others[257];
	size_t bits[33]; // 0..32, corresponds to htable.L[]
	uint8_t huff_val[16 * 255]; // to hcode.huff_val[] => htable.V[]
};

struct context {
	/* Specifies one of four possible destinations at the decoder into
	 * which the quantization table shall be installed */
	struct qtable qtable[4];

	/*  Sample precision */
	uint8_t P;

	/* Number of lines, Number of samples per line */
	uint16_t Y, X;

	/* Number of image components in frame */
	uint8_t Nf;

	struct component component[256];

	/* there are two types of tables, DC and AC; the identifiers are not unique accross these types */
	/* indices: [0=DC/1=AC][identifier] */
	struct htable htable[2][4];
	struct hcode hcode[2][4];
	struct huffenc huffenc[2][4];

	/* Restart interval */
	uint16_t Ri;

	/* macroblocks horizontally and vertically */
	size_t m_x, m_y;

	/* seq. number */
	size_t mblocks;

	uint8_t max_H, max_V;
};

void init_huffenc(struct huffenc *huffenc);

int init_qtable(struct qtable *qtable);

int init_component(struct component *component);

int init_htable(struct htable *htable);

int init_context(struct context *context);

int alloc_buffers(struct component *component, size_t size);

void free_buffers(struct context *context);

size_t ceil_div(size_t n, size_t d);

int compute_no_blocks_and_alloc_buffers(struct context *context);

int clamp(int min, int val, int max);

#endif
