#pragma once

#include <stdint.h>

namespace Player::Decoders::LJ92
{
#ifdef __GNUC__
#pragma GCC visibility push(default)
	extern "C" int TestMethod(int param);
#pragma GCC visibility pop
#endif

#ifdef _MSC_VER 
	extern "C" __declspec(dllexport) int TestMethod(int param);

	extern "C" __declspec(dllexport) int Decode(uint8_t* pCompressedIn, int compressedSizeBytes, uint16_t* pOut, int width, int height, int bitDepth);
#endif
}