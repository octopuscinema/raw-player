#pragma once

#ifdef __GNUC__

#pragma GCC visibility push(default)
extern "C" int TestMethod(int param);
#pragma GCC visibility pop

#else

extern "C" int TestMethod(int param);

#endif
