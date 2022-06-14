#pragma once

#ifdef __GNUC__
#define DECODER_EXPORT extern "C"
#define DECODER_EXPORT_BEGIN _Pragma("GCC visibility push(default)")
#define DECODER_EXPORT_END _Pragma("GCC visibility pop")
#endif
#ifdef _MSC_VER
#define DECODER_EXPORT extern "C" __declspec(dllexport)
#define DECODER_EXPORT_BEGIN
#define DECODER_EXPORT_END
#endif
