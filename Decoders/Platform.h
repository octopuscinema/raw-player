#pragma once

#ifdef _WIN32
#define NOMINMAX // Disables windows.h min max macros (interferes with std::min/std::max)
#define OCTOPUS_PLATFORM_WINDOWS
#if defined(_M_ARM) || defined(_M_ARM64) || defined(_M_ARM64EC)
#define OCTOPUS_PLATFORM_WINDOWS_ARM
#endif
#if defined(_M_ARM64) || defined(_M_ARM64EC)
#define OCTOPUS_PLATFORM_WINDOWS_ARM64
#endif
#ifdef _M_ARM64EC
#define OCTOPUS_PLATFORM_WINDOWS_ARM64EC
#endif
#ifdef _WIN64
#define OCTOPUS_PLATFORM_WINDOWS_64
#endif
#endif

#ifdef __ANDROID__
#define OCTOPUS_PLATFORM_ANDROID

#ifdef __arm__
#define OCTOPUS_PLATFORM_ANDROID_ARM

#ifdef __aarch64__
#define OCTOPUS_PLATFORM_ANDROID_ARM64
#endif
#else
#ifdef __i386__
#define OCTOPUS_PLATFORM_ANDROID_X86
#endif
#endif
#endif

#if defined(__linux__) && !defined(__ANDROID__)
#define OCTOPUS_PLATFORM_LINUX

#ifdef __arm__
#define OCTOPUS_PLATFORM_LINUX_ARM
#endif

#ifdef __aarch64__
#define OCTOPUS_PLATFORM_LINUX_ARM
#define OCTOPUS_PLATFORM_LINUX_ARM64
#endif

#ifdef __x86_64__
#define OCTOPUS_PLATFORM_LINUX_AMD64
#endif

#endif

#ifdef __APPLE__
    #include "TargetConditionals.h"
    #if TARGET_OS_IPHONE
        #define OCTOPUS_PLATFORM_IOS
        #if TARGET_IPHONE_SIMULATOR
            #define OCTOPUS_PLATFORM_IOS_SIMULATOR
        #endif
    #endif
    #if TARGET_OS_OSX
        #define OCTOPUS_PLATFORM_MACOS
        #if TARGET_CPU_ARM64
            #define OCTOPUS_PLATFORM_MACOS_ARM64
        #endif
        #if TARGET_CPU_X86_64
            #define OCTOPUS_PLATFORM_MACOS_X86_64
        #endif
    #endif
#endif
