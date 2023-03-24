#pragma once

#include "../Api.h"

#include <stdint.h>
#include <cstddef>

namespace Octopus::Player::Decoders::Unpack
{
DECODER_EXPORT_BEGIN
    DECODER_EXPORT void Unpack10to16Bit(uint8_t* pOut, const uint8_t* p10BitPacked, uint32_t sizeBytes);
    DECODER_EXPORT uint32_t Unpack12InputOffsetBytes();
    DECODER_EXPORT void Unpack12to16Bit(uint8_t* pOut, const uint8_t* p12BitPacked, uint32_t sizeBytes);
    DECODER_EXPORT void Unpack14to16Bit(uint8_t* pOut, const uint8_t* p14BitPacked, uint32_t sizeBytes);
DECODER_EXPORT_END
}
