#include "../Api.h"

#include <stddef.h>
#include <stdio.h>
#include <jpeglib.h>
#include <stdint.h>

namespace Octopus::Player::Decoders::Jpeg
{
    extern "C" bool IsLossy(uint8_t* pInCompressed, uint32_t compressedSizeBytes, uint32_t bitDepth)
	{
		return false;
	}

	extern "C" Core::eError DecodeLossy(uint8_t* pOut16Bit, uint8_t* pInCompressed, uint32_t compressedSizeBytes, uint32_t width, uint32_t height,
        uint32_t bitDepth)
	{
		return Core::eError::NotImplmeneted;
	}
}
