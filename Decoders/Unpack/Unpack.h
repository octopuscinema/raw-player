#pragma once

#include "../Api.h"

#include <stdint.h>
#include <cstddef>

namespace Octopus::Player::Decoders::Unpack
{
DECODER_EXPORT_BEGIN
	DECODER_EXPORT int TestUnpackMethod(int param);
    DECODER_EXPORT void Unpack12to16Bit(uint8_t* pOut, std::size_t outOffsetBytes, const uint8_t* p12BitPacked, std::size_t sizeBytes);
    DECODER_EXPORT void Unpack14to16Bit(uint8_t* pOut, std::size_t outOffsetBytes, const uint8_t* p14BitPacked, std::size_t sizeBytes);
    DECODER_EXPORT void TestByteArray(uint8_t* pOut, const uint8_t* pIn, std::size_t sizeBytes);
DECODER_EXPORT_END
}
