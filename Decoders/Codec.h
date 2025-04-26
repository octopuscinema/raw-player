#pragma once

#include <stdexcept>
#include <string>

namespace Octopus::Player::Decoders
{
    enum class eCodec
    {
        Invalid = -3,
        Unknown = -2,
        Unset = -1,
        Dng,
        ProResRAW,
        ProResRAWHQ,
        TicoRAW
    };
}