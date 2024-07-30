#pragma once

#include "../Api.h"

#include <stdint.h>

namespace Octopus::Player::Decoders::Jpeg
{
DECODER_EXPORT_BEGIN
    DECODER_EXPORT bool IsLossy(uint8_t* pInCompressed, uint32_t compressedSizeBytes);
	DECODER_EXPORT Core::eError DecodeLossy(uint8_t* pOut16Bit, uint8_t* pInCompressed, uint32_t compressedSizeBytes, uint32_t width,
        uint32_t height, uint32_t bitDepth);
DECODER_EXPORT_END
}
