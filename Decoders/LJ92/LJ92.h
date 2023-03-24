#pragma once

#include "../Api.h"

#include <stdint.h>

namespace Octopus::Player::Decoders::LJ92
{
DECODER_EXPORT_BEGIN
	DECODER_EXPORT Core::eError Decode(uint8_t* pOut16Bit, uint8_t* pInCompressed, uint32_t compressedSizeBytes, uint32_t width, uint32_t height, uint32_t bitDepth);
DECODER_EXPORT_END
}
