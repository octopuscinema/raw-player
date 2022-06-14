#pragma once

#include "../Api.h"

#include <stdint.h>
#include <cstddef>

namespace Player::Decoders::Unpack
{
DECODER_EXPORT_BEGIN
	DECODER_EXPORT int TestUnpackMethod(int param);
    DECODER_EXPORT void Unpack12to16Bit(uint16_t* p16BitOut, const uint8_t* p12BitPacked, std::size_t sizeBytes);
    DECODER_EXPORT void Unpack14to16Bit(uint16_t* p16BitOut, const uint8_t* p14BitPacked, std::size_t sizeBytes);
DECODER_EXPORT_END
}
