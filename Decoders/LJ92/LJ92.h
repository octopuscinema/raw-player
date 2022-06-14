#pragma once

#include "../Api.h"

#include <stdint.h>

namespace Player::Decoders::LJ92
{
DECODER_EXPORT_BEGIN
	DECODER_EXPORT int TestMethod(int param);
	DECODER_EXPORT int Decode(uint8_t* pCompressedIn, int compressedSizeBytes, uint16_t* pOut, int width, int height, int bitDepth);
DECODER_EXPORT_END
}
