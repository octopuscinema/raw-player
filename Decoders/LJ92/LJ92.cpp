#include "LJ92.h"

#ifdef _MSC_VER 

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

BOOL APIENTRY DllMain(HMODULE hModule,
    DWORD  ul_reason_for_call,
    LPVOID lpReserved
)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

#endif

namespace Player::Decoders::LJ92
{
    extern "C" int TestMethod(int param)
    {
        return param + 1;
    }

    extern "C" int Decode(uint8_t * pCompressedIn, int compressedSizeBytes, uint16_t * pOut, int width, int height, int bitDepth)
    {
        return 0;
    }
}
