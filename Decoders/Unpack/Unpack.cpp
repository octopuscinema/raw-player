#include "Unpack.h"

namespace Octopus::Player::Decoders::Unpack
{
    extern "C" int TestUnpackMethod(int param)
    {
        return param +1;
    }

	extern "C" void Unpack12to16Bit(uint8_t* pOut, std::size_t outOffsetBytes, const uint8_t* p12BitPacked, std::size_t sizeBytes)
	{
		const uint8_t *pEnd = p12BitPacked + sizeBytes;
		uint8_t b0, b1, b2;
		uint16_t* p16BitOut = (uint16_t*)(pOut + outOffsetBytes);

		while (p12BitPacked != pEnd)
		{
			// Read 2 pixel block of packed 12-bit (3 bytes)
			// Reverse order for DNG big endian to little endian conversion
			b2 = *p12BitPacked++;
			b1 = *p12BitPacked++;
			b0 = *p12BitPacked++;

			// Reverse order for DNG big endian to little endian conversion
			*p16BitOut++ = (b2 << 4) | (b1 >> 4);
			*p16BitOut++ = (0x0fff & (b1 << 8)) | b0;
		}
	}

	extern "C" void Unpack14to16Bit(uint8_t* pOut, std::size_t outOffsetBytes, const uint8_t* p14BitPacked, std::size_t sizeBytes)
	{
		const uint8_t* pEnd = p14BitPacked + sizeBytes;
		uint8_t b0, b1, b2, b3, b4, b5, b6;
		uint16_t* p16BitOut = (uint16_t*)(pOut + outOffsetBytes);

		while (p14BitPacked != pEnd)
		{
			// Read 4 pixel block of packed 14-bit (7 bytes)
			// Reverse order for DNG big endian to little endian conversion
			b6 = *p14BitPacked++;
			b5 = *p14BitPacked++;
			b4 = *p14BitPacked++;
			b3 = *p14BitPacked++;
			b2 = *p14BitPacked++;
			b1 = *p14BitPacked++;
			b0 = *p14BitPacked++;

			// Pixel4: Byte5 bits0-5 | Byte6 bits 6-13
			*p16BitOut++ = (b5 >> 2) | (b6 << 6);

			// Pixel3: Byte3 bits0-3 | Byte4 bits 4-11 | Byte5 bits12-13
			*p16BitOut++ = (b3 >> 4) | (b4 << 4) | (0x3fff & (b5 << 12));

			// Pixel2: Byte1 bits0-1 | Byte2 bits2-9 | Byte3 bits10-13
			*p16BitOut++ = (b1 >> 6) | (b2 << 2) | (0x3fff & (b3 << 10));

			// Pixel1: Byte0 bits0-7 | Byte1 bits8-13
			*p16BitOut++ = (0x3fff & (b1 << 8)) | b0;
		}
	}

	extern "C" void TestByteArray(uint8_t* pOut, const uint8_t* pIn, std::size_t sizeBytes)
	{
		for (int i = 0; i < sizeBytes; i++)
		{
			pOut[i] = pIn[i] * 2;
		}
	}
}
