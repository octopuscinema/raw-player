#include "LossyJpeg.h"

#include <stddef.h>
#include <stdio.h>
#include <jpeglib.h>
#include <stdint.h>

// Bit hacky, this needs to match the internal header jpegint.h
struct jpeg_decomp_master 
{
	void (*prepare_for_output_pass) (j_decompress_ptr cinfo);
	void (*finish_output_pass) (j_decompress_ptr cinfo);

	/* State variables made visible to other modules */
	boolean is_dummy_pass;        /* True during 1st pass for 2-pass quant */
	boolean lossless;             /* True if decompressing a lossless image */

	/* Partial decompression variables */
	JDIMENSION first_iMCU_col;
	JDIMENSION last_iMCU_col;
	JDIMENSION first_MCU_col[MAX_COMPONENTS];
	JDIMENSION last_MCU_col[MAX_COMPONENTS];
	boolean jinit_upsampler_no_alloc;

	/* Last iMCU row that was successfully decoded */
	JDIMENSION last_good_iMCU_row;

	/* Tail of list of saved markers */
	jpeg_saved_marker_ptr marker_list_end;
};

namespace Octopus::Player::Decoders::Jpeg
{
    extern "C" bool IsLossy(uint8_t* pInCompressed, uint32_t compressedSizeBytes)
	{
		jpeg_decompress_struct context;
		jpeg_error_mgr errorManager;
        
		context.err = jpeg_std_error(&errorManager);
		jpeg_create_decompress(&context);

		jpeg_mem_src(&context, pInCompressed, compressedSizeBytes);

		if ( jpeg_read_header(&context, TRUE) != JPEG_HEADER_OK)
		{
			jpeg_destroy_decompress(&context);
			return false;
		}

		const auto lossy = (context.data_precision == 12 || context.data_precision == 16) && !context.master->lossless;

		jpeg_destroy_decompress(&context);
		return lossy;
	}

	extern "C" Core::eError DecodeLossy(uint8_t* pOut16Bit, uint8_t* pInCompressed, uint32_t compressedSizeBytes, uint32_t width, uint32_t height,
        uint32_t bitDepth)
	{
		jpeg_decompress_struct context;
		jpeg_error_mgr errorManager;

		context.err = jpeg_std_error(&errorManager);
		jpeg_create_decompress(&context);

		jpeg_mem_src(&context, pInCompressed, compressedSizeBytes);

		if ( jpeg_read_header(&context, TRUE) != JPEG_HEADER_OK )
		{
			jpeg_destroy_decompress(&context);
			return Core::eError::BadImageData;
		}

		jpeg_start_decompress(&context);

		const auto stride = context.output_width * context.output_components * sizeof(short);

		switch (context.data_precision)
		{
		case 12:
			while (context.output_scanline < context.output_height)
			{
				uint8_t* scanlines[4];
				scanlines[0] = pOut16Bit + (context.output_scanline * stride);
				scanlines[1] = scanlines[0] + stride;
				scanlines[2] = scanlines[1] + stride;
				scanlines[3] = scanlines[2] + stride;

				if (jpeg12_read_scanlines(&context, J12SAMPARRAY(scanlines), 4) == 0)
				{
					jpeg_abort_decompress(&context);
					jpeg_destroy_decompress(&context);
					return Core::eError::BadImageData;
				}
			}

			// Work around for weird behaviour from RAW Converter creating 16-bit dngs with 12-bit jpeg data
			// This could be moved to the GPU pipeline...
			if (bitDepth > context.data_precision)
			{
				const auto shift = bitDepth - context.data_precision;
				const auto pData = (uint16_t*)pOut16Bit;
				const auto pixelCount = width * height;
				for (int i = 0; i < pixelCount; i++)
					pData[i] = pData[i] << shift;
			}
			break;
		case 16:
			while (context.output_scanline < context.output_height)
			{
				uint8_t* scanlines[4];
				scanlines[0] = pOut16Bit + (context.output_scanline * stride);
				scanlines[1] = scanlines[0] + stride;
				scanlines[2] = scanlines[1] + stride;
				scanlines[3] = scanlines[2] + stride;

				if (jpeg16_read_scanlines(&context, J16SAMPARRAY(scanlines), 4) == 0)
				{
					jpeg_abort_decompress(&context);
					jpeg_destroy_decompress(&context);
					return Core::eError::BadImageData;
				}
			}
			break;
		default:
			jpeg_finish_decompress(&context);
			jpeg_destroy_decompress(&context);
			return Core::eError::BadMetadata;
		}

		jpeg_finish_decompress(&context);
		jpeg_destroy_decompress(&context);
		return Core::eError::None;
	}
}
