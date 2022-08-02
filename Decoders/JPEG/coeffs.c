#include <assert.h>
#include "coeffs.h"
#include "huffman.h"

struct coeff_dc {
	int32_t c;
};

struct coeff_ac {
	int eob; // is end-of-block?
	uint8_t zrl; // zero run
	int32_t c; // the coefficient
};

uint8_t value_to_category(uint8_t value)
{
	return value & 15;
}

uint8_t value_to_zerorun(uint8_t value)
{
	return value >> 4;
}

uint8_t cat_zrl_to_value(uint8_t cat, uint8_t zrl)
{
	return ((zrl & 15) << 4) | (cat & 15);
}

/*
 * Figure F.12 – Extending the sign bit of a decoded value in V
 */
int32_t decode_coeff(uint8_t cat, uint16_t extra)
{
	switch (cat) {
		int sign;
		case 0:
			return 0;
		default:
			// 0 negative, 1 positive
			sign = extra >> (cat - 1);
			if (sign == 0) {
				return -(INT32_C(1) << cat) + extra + 1;
			} else {
				return extra;
			}
	}
}

/*
 * uint8_t cat = encode_cat(c);
 * uint16_t extra = encode_extra(c, cat);
 *
 * c = decode_coeff(cat, extra);
 */
uint8_t encode_cat(int32_t c)
{
	if (c == 0) {
		return 0;
	}

	if (c < 0) {
		c = -c;
	}

	uint8_t r = 0;

	do {
		c >>= 1;
		r++;
	} while (c != 0);

	return r;
}

uint16_t encode_extra(int32_t c, uint8_t cat)
{
	if (c < 0) {
		c--;
	}

	return (uint16_t)(c & ((UINT32_C(1) << cat) - 1));
}

// read_code + read_extra_bits + compose the coefficient value
int read_dc(struct bits *bits, struct hcode *hcode_dc, struct coeff_dc *coeff_dc)
{
	int err;

	/* cat. code */
	uint8_t cat;

	/* read DC coefficient */
	err = read_code(bits, hcode_dc, &cat);
	RETURN_IF(err);

	/* read extra bits */
	uint16_t extra;
	err = read_extra_bits(bits, cat, &extra);
	RETURN_IF(err);

	assert(coeff_dc != NULL);

	coeff_dc->c = decode_coeff(cat, extra);

	return RET_SUCCESS;
}

// encode_cat(), encode_extra(), write_code(), write_extra_bits()
int write_dc(struct bits *bits, struct hcode *hcode_dc, struct coeff_dc *coeff_dc)
{
	int err;

	assert(coeff_dc != NULL);

	uint8_t cat = encode_cat(coeff_dc->c);
	uint16_t extra = encode_extra(coeff_dc->c, cat);

	err = write_code(bits, hcode_dc, cat);
	RETURN_IF(err);

	err = write_extra_bits(bits, cat, extra);
	RETURN_IF(err);

	return RET_SUCCESS;
}

int read_ac(struct bits *bits, struct hcode *hcode_ac, struct coeff_ac *coeff_ac)
{
	int err;

	// RS = binary ’RRRRSSSS’
	uint8_t rs;

	err = read_code(bits, hcode_ac, &rs);
	RETURN_IF(err);

	uint8_t cat = value_to_category(rs);

	// read extra bits
	uint16_t extra;
	err = read_extra_bits(bits, cat, &extra);
	RETURN_IF(err);

	uint8_t zrl = value_to_zerorun(rs);

	coeff_ac->zrl = zrl;

	assert(coeff_ac != NULL);

	// EOB
	if (rs == 0) {
		coeff_ac->eob = 1;
	} else {
		coeff_ac->eob = 0;
	}

	coeff_ac->c = decode_coeff(cat, extra);

	return RET_SUCCESS;
}

// encode_cat, encode_extra, compose rs from zrl and cat, write_code(), write_extra_bits()
int write_ac(struct bits *bits, struct hcode *hcode_ac, struct coeff_ac *coeff_ac)
{
	int err;

	assert(coeff_ac != NULL);

	// due to valgrind
	if (coeff_ac->eob) {
		coeff_ac->c = 0;
	}

	uint8_t cat = encode_cat(coeff_ac->c);
	uint16_t extra = encode_extra(coeff_ac->c, cat);

	uint8_t rs = cat_zrl_to_value(cat, coeff_ac->zrl);

	if (coeff_ac->eob) {
		cat = 0; // no extra bits
		rs = 0;
	}

	// write Huff(rs), extra

	err = write_code(bits, hcode_ac, rs);
	RETURN_IF(err);

	err = write_extra_bits(bits, cat, extra);
	RETURN_IF(err);

	return RET_SUCCESS;
}

int read_block(struct bits *bits, struct context *context, uint8_t Cs, struct int_block *int_block)
{
	int err;
	uint8_t Td = context->component[Cs].Td;
	uint8_t Ta = context->component[Cs].Ta;

	struct hcode *hcode_dc = &context->hcode[0][Td];
	struct hcode *hcode_ac = &context->hcode[1][Ta];

	struct coeff_dc coeff_dc;

	/* read DC coefficient */
	err = read_dc(bits, hcode_dc, &coeff_dc);
	RETURN_IF(err);

	/*
	 * special case: when reading past the end of data, the caller set
	 * the int_block to NULL... treat this as if there was no more data
	 */
	if (int_block == NULL) {
		printf("*** corrupted JPEG file ***\n");
		return RET_FAILURE_NO_MORE_DATA;
	}

	assert(int_block != NULL);

	int_block->c[zigzag[0]] = coeff_dc.c;

	// reset all remaining 63 coefficients to zero
	for (int i = 1; i < 64; ++i) {
		int_block->c[zigzag[i]] = 0;
	}

	int i = 1; // AC coefficient pointer
	/* read 63 AC coefficients */
	/* F.1.2.2 Huffman encoding of AC coefficients */
	int rem = 63; // remaining
	do {
		struct coeff_ac coeff_ac;

		/* read AC coefficient */
		err = read_ac(bits, hcode_ac, &coeff_ac);
		RETURN_IF(err);

		// EOB
		if (coeff_ac.eob) {
			break;
		}

		// zero run + one AC coeff.
		i += coeff_ac.zrl;
		int_block->c[zigzag[i]] = coeff_ac.c;
		i++;

		rem -= coeff_ac.zrl + 1;
	} while (rem > 0);

	return RET_SUCCESS;
}

int write_block(struct bits *bits, struct context *context, uint8_t Cs, struct int_block *int_block)
{
	int err;
	uint8_t Td = context->component[Cs].Td;
	uint8_t Ta = context->component[Cs].Ta;

	struct hcode *hcode_dc = &context->hcode[0][Td];
	struct hcode *hcode_ac = &context->hcode[1][Ta];

	assert(int_block != NULL);

	struct coeff_dc coeff_dc;

	coeff_dc.c = int_block->c[zigzag[0]];

	// write_dc()
	err = write_dc(bits, hcode_dc, &coeff_dc);
	RETURN_IF(err);

	/* Figure F.2 – Procedure for sequential encoding of AC coefficients with Huffman coding */
	for (int r = 0, i = 1; i < 64; ++i) {
		struct coeff_ac coeff_ac;
		coeff_ac.eob = 0;

		if (int_block->c[zigzag[i]] == 0) {
			/* zero coefficient */
			if (i == 63) {
				coeff_ac.eob = 1;
				err = write_ac(bits, hcode_ac, &coeff_ac);
				RETURN_IF(err);
			} else {
				r++;
			}
		} else {
			/* non-zero coefficient */
			while (r > 15) {
				/* produce ZRL */
				coeff_ac.c = 0;
				coeff_ac.zrl = 15;
				err = write_ac(bits, hcode_ac, &coeff_ac);
				RETURN_IF(err);
				r -= 16;
			}
			/* encode coefficient */
			coeff_ac.c = int_block->c[zigzag[i]];
			coeff_ac.zrl = r;
			err = write_ac(bits, hcode_ac, &coeff_ac);
			RETURN_IF(err);
			r = 0;
		}
	}

	return RET_SUCCESS;
}

/* don't actually compress the block, just collect the coded values to later optimize Huffman table */
int write_block_dry(struct context *context, uint8_t Cs, struct int_block *int_block)
{
	uint8_t Td = context->component[Cs].Td;
	uint8_t Ta = context->component[Cs].Ta;

	struct huffenc *huffenc_dc = &context->huffenc[0][Td];
	struct huffenc *huffenc_ac = &context->huffenc[1][Ta];

	assert(int_block != NULL);

	struct coeff_dc coeff_dc;

	coeff_dc.c = int_block->c[zigzag[0]];

	// write_dc()
	huffenc_dc->freq[encode_cat(coeff_dc.c)]++;

	/* Figure F.2 – Procedure for sequential encoding of AC coefficients with Huffman coding */
	for (int r = 0, i = 1; i < 64; ++i) {
		struct coeff_ac coeff_ac;
		coeff_ac.eob = 0;

		if (int_block->c[zigzag[i]] == 0) {
			/* zero coefficient */
			if (i == 63) {
				coeff_ac.eob = 1;
				// write_ac()
				huffenc_ac->freq[0]++;
			} else {
				r++;
			}
		} else {
			/* non-zero coefficient */
			while (r > 15) {
				/* produce ZRL */
				coeff_ac.c = 0;
				coeff_ac.zrl = 15;
				// write_ac()
				huffenc_ac->freq[cat_zrl_to_value(encode_cat(coeff_ac.c), coeff_ac.zrl)]++;
				r -= 16;
			}
			/* encode coefficient */
			coeff_ac.c = int_block->c[zigzag[i]];
			coeff_ac.zrl = r;
			// write_ac()
			huffenc_ac->freq[cat_zrl_to_value(encode_cat(coeff_ac.c), coeff_ac.zrl)]++;
			r = 0;
		}
	}

	return RET_SUCCESS;
}
