#include "Unpack.h"

#ifdef __AVX2__
#include <immintrin.h>
#endif

namespace Octopus::Player::Decoders::Unpack
{
	namespace NonSIMD
	{
		static inline void Unpack12to16Bit(uint8_t* pOut, std::size_t outOffsetBytes, const uint8_t* p12BitPacked, uint32_t inOffsetBytes, std::size_t sizeBytes)
		{
			p12BitPacked += inOffsetBytes;

			const uint8_t* pEnd = p12BitPacked + sizeBytes;
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
	}

#ifdef __AVX2__
	namespace SIMD
	{
		// Unpacks 16 packed 12-bit Little Endian values using AVX 256bit instructions in one pass
		// Warning, the input pointer is read from 4 bytes less.
		// Ensure there are at least 4 valid allocated bytes before the address of pInput
		static inline __m256i Unpack12to16Bit16(uint8_t* pInput)
		{
			// v= [ x H G F E | D C B A x ]   where each letter is a 3-byte pair of two 12-bit fields, and x is 4 bytes of garbage we load but ignore
			__m256i v = _mm256_loadu_si256((const __m256i*)(pInput - 4));

			// each 16-bit chunk has the bits it needs, but not in the right position
			const __m256i bytegrouping =
				_mm256_setr_epi8(4, 5, 5, 6, 7, 8, 8, 9, 10, 11, 11, 12, 13, 14, 14, 15, // low half uses last 12B
					0, 1, 1, 2, 3, 4, 4, 5, 6, 7, 7, 8, 9, 10, 10, 11); // high half uses first 12B
			v = _mm256_shuffle_epi8(v, bytegrouping);

			// in each chunk of 8 nibbles (4 bytes): [ f e d c | d c b a ]
			__m256i hi = _mm256_srli_epi16(v, 4);                              // [ 0 f e d | xxxx ]
			__m256i lo = _mm256_and_si256(v, _mm256_set1_epi32(0x00000FFF));  // [ 0000 | 0 c b a ]

			// nibbles in each pair of epi16: [ 0 f e d | 0 c b a ] 
			return _mm256_blend_epi16(lo, hi, 0b10101010);
		}

		// Helper function does the above but copies to output in 64bit chunks
		static inline void Unpack12to16Bit16(uint8_t* pInput, uint64_t* pOutput)
		{
			const auto& Out16 = Unpack12BitLE16(pInput);
			pOutput[0] = ((uint64*)&Out16)[0];
			pOutput[1] = ((uint64*)&Out16)[1];
			pOutput[2] = ((uint64*)&Out16)[2];
			pOutput[3] = ((uint64*)&Out16)[3];
		}

		// Helper function does the above but with an arbitrary length input
		static inline void Unpack12to16Bit(uint8_t* pOut, std::size_t outOffsetBytes, const uint8_t* p12BitPacked, std::size_t sizeBytes)
		{
			static const u32 Stride12Bit16 = (16 * 12) / 8;
			const auto NumElements = (InputSizeBytes * 8) / 12;

			uint32_t i = 0;
			for (i = 0; i < NumElements; i += 16) {
				const auto& Out16 = Unpack12BitLE16(pInput);
				*pOutput++ = ((uint64*)&Out16)[0];
				*pOutput++ = ((uint64*)&Out16)[1];
				*pOutput++ = ((uint64*)&Out16)[2];
				*pOutput++ = ((uint64*)&Out16)[3];
				pInput += Stride12Bit16;
			}

			// Do the remaining (less than 16 elements) without simd
			if (i > NumElements) {
				const auto RemainingElements = i - NumElements;
				const auto RemainingBytes = (RemainingElements * 12) / 8;
				NonSIMD::Unpack12BitLE((uint16_t*)pInput, (uint16_t*)pOutput, RemainingBytes);
			}
		}
	}
#endif

	extern "C" void Unpack10to16Bit(uint8_t * pOut, std::size_t outOffsetBytes, const uint8_t * p12BitPacked, std::size_t sizeBytes)
	{
		// TODO
	}

	extern "C" uint32_t Unpack12InputOffsetBytes()
	{
#ifdef __AVX2__
		return 4;
#else
		return 0;
#endif
	}

	extern "C" void Unpack12to16Bit(uint8_t* pOut, std::size_t outOffsetBytes, const uint8_t* p12BitPacked, uint32_t inOffsetBytes, std::size_t sizeBytes)
	{
#ifdef __AVX2__
		SIMD::Unpack12to16Bit();
#else
		NonSIMD::Unpack12to16Bit(pOut, outOffsetBytes, p12BitPacked, inOffsetBytes, sizeBytes);
#endif
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
}
