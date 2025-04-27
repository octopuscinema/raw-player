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

    constexpr uint32_t FourCC(char const p[4])
    {
        return (p[0] << 24) | (p[1] << 16) | (p[2] << 8) | p[3];
    }

    static inline uint32_t CodecFourCC(eCodec codec)
    {
        switch (codec)
        {
        case eCodec::Dng:
            return 'cDNG';
        case eCodec::ProResRAW:
            return 'aprn';
        case eCodec::ProResRAWHQ:
            return 'aprh';
        case eCodec::TicoRAW:
            return 'nraw';
        default:
            throw std::runtime_error("Unknown fourcc for codec: " + std::to_string(static_cast<int>(codec)));
        }
    }

    static inline eCodec CodecFromFourCC(uint32_t fourCC)
    {
        switch (fourCC)
        {
        case 'cDNG':
            return eCodec::Dng;
        case 'aprn':
            return eCodec::ProResRAW;
        case 'aprh':
            return eCodec::ProResRAWHQ;
        case 'nraw':
            return eCodec::TicoRAW;
        default:
            return eCodec::Unknown;
        }
    }

    static inline eCodec CodecFromFourCC(const std::string& fourCC)
    {
        if (fourCC.length() < 4)
            return eCodec::Invalid;

        return CodecFromFourCC(FourCC(fourCC.c_str()));
    }
}