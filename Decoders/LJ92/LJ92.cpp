/*
LJ92.cpp (original)
(c) Andrew Baldwin 2014

LJ92.cpp (modifications/optimisations)
(c) OCTOPUS CINEMA 2022
*/

#include "LJ92.h"

#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifdef _MSC_VER
#include <intrin.h>

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
	
namespace Octopus::Player::Decoders::LJ92
{
    typedef uint8_t u8;
	typedef uint16_t u16;
	typedef uint32_t u32;

#ifdef _MSC_VER
	// MSVC implementation of clz, from
	// https://stackoverflow.com/questions/355967/how-to-use-msvc-intrinsics-to-get-the-equivalent-of-this-gcc-code
	uint32_t __inline __builtin_clz(uint32_t value)
	{
		unsigned long leading_zero = 0;

		if (_BitScanReverse(&leading_zero, value))
		{
			return 31 - leading_zero;
		}
		else
		{
			// This is undefined, I better choose 32 than 0
			return 32;
		}
	}
#endif

//#define DEBUG
//#define PROFILE_DURATION

    //#define SLOW_HUFF

	struct ljp {
		u8* data;
		u8* dataend;
		int datalen;
		int scanstart;
		int ix;
		int x; // Width
		int y; // Height
		int bits; // Bit depth
		int writelen; // Write rows this long
		int skiplen; // Skip this many values after each row
		u16* linearize; // Linearization table
		int linlen;
		int sssshist[16];

		// Huffman table - only one supported, and probably needed
#ifdef SLOW_HUFF
		int* maxcode;
		int* mincode;
		int* valptr;
		u8* huffval;
		int* huffsize;
		int* huffcode;
#else
		u16* hufflut;
		int huffbits;
#endif
		// Parse state
		int cnt;
		u32 b;
		u16* image;
		u16* rowcache;
		u16* outrow[2];
	};

	enum LJ92_ERRORS {
		LJ92_ERROR_NONE = 0,
		LJ92_ERROR_CORRUPT = -1,
		LJ92_ERROR_NO_MEMORY = -2,
		LJ92_ERROR_BAD_HANDLE = -3,
		LJ92_ERROR_TOO_WIDE = -4,
	};

	typedef struct ljp lj92;

#ifdef PROFILE_DURATION
	CJ3Timer g_Timer;
#endif

	static int find(ljp* self) {
		int ix = self->ix;
		u8* data = self->data;
		while (data[ix] != 0xFF && ix < (self->datalen - 1)) {
			ix += 1;
		}
		ix += 2;
		if (ix >= self->datalen) return -1;
		self->ix = ix;
		return data[ix - 1];
	}

#define BEH(ptr) ((((int)(*&ptr))<<8)|(*(&ptr+1)))

	static LJ92_ERRORS parseHuff(ljp* self)
	{
		LJ92_ERRORS ret = LJ92_ERROR_CORRUPT;
		u8* huffhead = &self->data[self->ix]; // xstruct.unpack('>HB16B',self.data[self.ix:self.ix+19])
		u8* bits = &huffhead[2];
		bits[0] = 0; // Because table starts from 1
		int hufflen = BEH(huffhead[0]);
		if ((self->ix + hufflen) >= self->datalen) return ret;
#ifdef SLOW_HUFF
		u8* huffval = calloc(hufflen - 19, sizeof(u8));
		if (huffval == NULL) return LJ92_ERROR_NO_MEMORY;
		self->huffval = huffval;
		for (int hix = 0; hix < (hufflen - 19); hix++) {
			huffval[hix] = self->data[self->ix + 19 + hix];
#ifdef DEBUG
			printf("huffval[%d]=%d\n", hix, huffval[hix]);
#endif
		}
		self->ix += hufflen;
		// Generate huffman table
		int k = 0;
		int i = 1;
		int j = 1;
		int huffsize_needed = 1;
		// First calculate how long huffsize needs to be
		while (i <= 16) {
			while (j <= bits[i]) {
				huffsize_needed++;
				k = k + 1;
				j = j + 1;
			}
			i = i + 1;
			j = 1;
		}
		// Now allocate and do it
		int* huffsize = calloc(huffsize_needed, sizeof(int));
		if (huffsize == NULL) return LJ92_ERROR_NO_MEMORY;
		self->huffsize = huffsize;
		k = 0;
		i = 1;
		j = 1;
		// First calculate how long huffsize needs to be
		int hsix = 0;
		while (i <= 16) {
			while (j <= bits[i]) {
				huffsize[hsix++] = i;
				k = k + 1;
				j = j + 1;
			}
			i = i + 1;
			j = 1;
		}
		huffsize[hsix++] = 0;

		// Calculate the size of huffcode array
		int huffcode_needed = 0;
		k = 0;
		int code = 0;
		int si = huffsize[0];
		while (1) {
			while (huffsize[k] == si) {
				huffcode_needed++;
				code = code + 1;
				k = k + 1;
			}
			if (huffsize[k] == 0)
				break;
			while (huffsize[k] != si) {
				code = code << 1;
				si = si + 1;
			}
		}
		// Now fill it
		int* huffcode = calloc(huffcode_needed, sizeof(int));
		if (huffcode == NULL) return LJ92_ERROR_NO_MEMORY;
		self->huffcode = huffcode;
		int hcix = 0;
		k = 0;
		code = 0;
		si = huffsize[0];
		while (1) {
			while (huffsize[k] == si) {
				huffcode[hcix++] = code;
				code = code + 1;
				k = k + 1;
			}
			if (huffsize[k] == 0)
				break;
			while (huffsize[k] != si) {
				code = code << 1;
				si = si + 1;
			}
		}

		i = 0;
		j = 0;

		int* maxcode = calloc(17, sizeof(int));
		if (maxcode == NULL) return LJ92_ERROR_NO_MEMORY;
		self->maxcode = maxcode;
		int* mincode = calloc(17, sizeof(int));
		if (mincode == NULL) return LJ92_ERROR_NO_MEMORY;
		self->mincode = mincode;
		int* valptr = calloc(17, sizeof(int));
		if (valptr == NULL) return LJ92_ERROR_NO_MEMORY;
		self->valptr = valptr;

		while (1) {
			while (1) {
				i++;
				if (i > 16)
					break;
				if (bits[i] != 0)
					break;
				maxcode[i] = -1;
			}
			if (i > 16)
				break;
			valptr[i] = j;
			mincode[i] = huffcode[j];
			j = j + bits[i] - 1;
			maxcode[i] = huffcode[j];
			j++;
		}
		free(huffsize);
		self->huffsize = NULL;
		free(huffcode);
		self->huffcode = NULL;
		ret = LJ92_ERROR_NONE;
#else
		/* Calculate huffman direct lut */
		// How many bits in the table - find highest entry
		u8* huffvals = &self->data[self->ix + 19];
		int maxbits = 16;
		while (maxbits > 0) {
			if (bits[maxbits]) break;
			maxbits--;
		}
		self->huffbits = maxbits;
		/* Now fill the lut */
		u16* hufflut = (u16*)malloc(((size_t)1 << maxbits) * sizeof(u16));
		if (hufflut == NULL) return LJ92_ERROR_NO_MEMORY;
		self->hufflut = hufflut;
		int i = 0;
		int hv = 0;
		int rv = 0;
		int vl = 0; // i
		int hcode;
		int bitsused = 1;
#ifdef DEBUG
		printf("%04x:%x:%d:%x\n", i, huffvals[hv], bitsused, 1 << (maxbits - bitsused));
#endif
		while (i < 1 << maxbits) {
			if (bitsused > maxbits) {
				break; // Done. Should never get here!
			}
			if (vl >= bits[bitsused]) {
				bitsused++;
				vl = 0;
				continue;
			}
			if (rv == 1 << (maxbits - bitsused)) {
				rv = 0;
				vl++;
				hv++;
#ifdef DEBUG
				printf("%04x:%x:%d:%x\n", i, huffvals[hv], bitsused, 1 << (maxbits - bitsused));
#endif
				continue;
			}
			hcode = huffvals[hv];
			hufflut[i] = hcode << 8 | bitsused;
			//printf("%d %d %d\n",i,bitsused,hcode);
			i++;
			rv++;
		}
		ret = LJ92_ERROR_NONE;
#endif
		return ret;
	}

	static LJ92_ERRORS parseSof3(ljp* self) {
		if (self->ix + 6 >= self->datalen) return LJ92_ERROR_CORRUPT;
		self->y = BEH(self->data[self->ix + 3]);
		self->x = BEH(self->data[self->ix + 5]);
		self->bits = self->data[self->ix + 2];
		self->ix += BEH(self->data[self->ix]);
		return LJ92_ERROR_NONE;
	}

	static LJ92_ERRORS parseBlock(ljp* self, int marker) {
		self->ix += BEH(self->data[self->ix]);
		if (self->ix >= self->datalen) return LJ92_ERROR_CORRUPT;
		return LJ92_ERROR_NONE;
	}

#ifdef SLOW_HUFF
	static int nextbit(ljp* self) {
		u32 b = self->b;
		if (self->cnt == 0) {
			u8* data = &self->data[self->ix];
			u32 next = *data++;
			b = next;
			if (next == 0xff) {
				data++;
				self->ix++;
			}
			self->ix++;
			self->cnt = 8;
		}
		int bit = b >> 7;
		self->cnt--;
		self->b = (b << 1) & 0xFF;
		return bit;
	}

	static int decode(ljp* self) {
		int i = 1;
		int code = nextbit(self);
		while (code > self->maxcode[i]) {
			i++;
			code = (code << 1) + nextbit(self);
		}
		int j = self->valptr[i];
		j = j + code - self->mincode[i];
		int value = self->huffval[j];
		return value;
	}

	static int receive(ljp* self, int ssss) {
		int i = 0;
		int v = 0;
		while (i != ssss) {
			i++;
			v = (v << 1) + nextbit(self);
		}
		return v;
	}

	static int extend(ljp* self, int v, int t) {
		int vt = 1 << (t - 1);
		if (v < vt) {
			vt = (-1 << t) + 1;
			v = v + vt;
		}
		return v;
	}
#endif

	inline static int nextdiff(ljp* self, int Px) {
#ifdef SLOW_HUFF
		int t = decode(self);
		int diff = receive(self, t);
		diff = extend(self, diff, t);
		//printf("%d %d %d %x\n",Px+diff,Px,diff,t);//,index,usedbits);
#else
		u32 b = self->b;
		int cnt = self->cnt;
		int huffbits = self->huffbits;
		int ix = self->ix;
		int next;
		while (cnt < huffbits) {
			next = *(u16*)&self->data[ix];
			int one = next & 0xFF;
			int two = next >> 8;
			b = (b << 16) | (one << 8) | two;
			cnt += 16;
			ix += 2;
			if (one == 0xFF) {
				//printf("%x %x %x %x %d\n",one,two,b,b>>8,cnt);
				b >>= 8;
				cnt -= 8;
			}
			else if (two == 0xFF)
				ix++;
		}
		int index = b >> (cnt - huffbits);
		u16 ssssused = self->hufflut[index];
		int usedbits = ssssused & 0xFF;
		int t = ssssused >> 8;
		self->sssshist[t]++;
		cnt -= usedbits;
		int keepbitsmask = (1 << cnt) - 1;
		b &= keepbitsmask;
		while (cnt < t) {
			next = *(u16*)&self->data[ix];
			int one = next & 0xFF;
			int two = next >> 8;
			b = (b << 16) | (one << 8) | two;
			cnt += 16;
			ix += 2;
			if (one == 0xFF) {
				b >>= 8;
				cnt -= 8;
			}
			else if (two == 0xFF)
				ix++;
		}
		cnt -= t;
		int diff = b >> cnt;
		int vt = 1 << (t - 1);
		if (diff < vt) {
			vt = (-1 << t) + 1;
			diff += vt;
		}
		keepbitsmask = (1 << cnt) - 1;
		self->b = b & keepbitsmask;
		self->cnt = cnt;
		self->ix = ix;
		//printf("%d %d\n",t,diff);
		//printf("%d %d %d %x %x %d\n",Px+diff,Px,diff,t,index,usedbits);
#ifdef DEBUG
#endif
#endif
		return diff;
	}

	static LJ92_ERRORS parsePred1NoSkipNoLinearize(ljp* self)
	{
		LJ92_ERRORS ret = LJ92_ERROR_CORRUPT;
		self->ix += BEH(self->data[self->ix]);
		self->cnt = 0;
		self->b = 0;

		// Now need to decode huffman coded values
		int c = 0;
		int pixels = self->y * self->x;
		u16* out = self->image;
		u16* thisrow = self->outrow[0];
		u16* lastrow = self->outrow[1];

		// First pixel predicted from base value
		int diff;
		int Px = 1 << (self->bits - 1); // First pixel uses middle grey value
		int col = 0;
		int left = 0;
		while (c < pixels) {
			
			diff = nextdiff(self, Px);
			left = Px + diff;
			Px = left; // Default use left pixel
			//printf("%d %d %d\n",c,diff,left);
			
			thisrow[col] = left;
			out[c++] = left;
			if (++col == self->x) {
				col = 0;
				u16* temprow = lastrow;
				lastrow = thisrow;
				thisrow = temprow;
				Px = lastrow[col]; // Use value above for first pixel in row
			}
			
			if (self->ix >= self->datalen + 2) break;
		}
		if (c >= pixels) ret = LJ92_ERROR_NONE;
		return ret;
	}

	static LJ92_ERRORS parsePred2NoSkipNoLinearize(ljp* self)
	{
		LJ92_ERRORS ret = LJ92_ERROR_CORRUPT;
		self->ix += BEH(self->data[self->ix]);
		self->cnt = 0;
		self->b = 0;
		
		// Now need to decode huffman coded values
		int pixels = self->y * self->x;
		u16* out = self->image;

		// First pixel predicted from base value
		int diff;
		int Px = 1 << (self->bits - 1); // First pixel uses middle grey value
		int col = 0;
		int left = 0;
		int c = 0;

		// First row (except first pixel) is always pixel to the left
		while (c < self->x) {
			diff = nextdiff(self, Px);
			left = Px + diff;
			
			out[c++] = left;
			
			if (self->ix >= self->datalen + 2)
				break;
			Px = left;
		}

		// Remaining rows are always pixel above
		while (c < pixels) {

			// Set pixel to above pixel
			Px = out[c - self->x];

			diff = nextdiff(self, Px);
			left = Px + diff;
			
			out[c++] = left;

			if (self->ix >= self->datalen + 2)
				break;
		}

		if (c >= pixels) ret = LJ92_ERROR_NONE;
		return ret;
	}

	static LJ92_ERRORS parsePred6(ljp* self)
	{
		LJ92_ERRORS ret = LJ92_ERROR_CORRUPT;
		self->ix = self->scanstart;
		//int compcount = self->data[self->ix+2];
		self->ix += BEH(self->data[self->ix]);
		self->cnt = 0;
		self->b = 0;
		int write = self->writelen;
		// Now need to decode huffman coded values
		int c = 0;
		int pixels = self->y * self->x;
		u16* out = self->image;
		u16* temprow;
		u16* thisrow = self->outrow[0];
		u16* lastrow = self->outrow[1];

		// First pixel predicted from base value
		int diff;
		int Px;
		int col = 0;
		int row = 0;
		int left = 0;
		int linear;

		// First pixel
		diff = nextdiff(self, 0);
		Px = 1 << (self->bits - 1);
		left = Px + diff;
		if (self->linearize)
			linear = self->linearize[left];
		else
			linear = left;
		thisrow[col++] = left;
		out[c++] = linear;
		if (self->ix >= self->datalen) return ret;
		--write;
		int rowcount = self->x - 1;
		while (rowcount--) {
			diff = nextdiff(self, 0);
			Px = left;
			left = Px + diff;
			if (self->linearize)
				linear = self->linearize[left];
			else
				linear = left;
			thisrow[col++] = left;
			out[c++] = linear;
			//printf("%d %d %d %d %x\n",col-1,diff,left,thisrow[col-1],&thisrow[col-1]);
			if (self->ix >= self->datalen) return ret;
			if (--write == 0) {
				out += self->skiplen;
				write = self->writelen;
			}
		}
		temprow = lastrow;
		lastrow = thisrow;
		thisrow = temprow;
		row++;
		//printf("%x %x\n",thisrow,lastrow);
		while (c < pixels) {
			col = 0;
			diff = nextdiff(self, 0);
			Px = lastrow[col]; // Use value above for first pixel in row
			left = Px + diff;
			if (self->linearize) {
				if (left > self->linlen) return LJ92_ERROR_CORRUPT;
				linear = self->linearize[left];
			}
			else
				linear = left;
			thisrow[col++] = left;
			//printf("%d %d %d %d\n",col,diff,left,lastrow[col]);
			out[c++] = linear;
			if (self->ix >= self->datalen) break;
			rowcount = self->x - 1;
			if (--write == 0) {
				out += self->skiplen;
				write = self->writelen;
			}
			while (rowcount--) {
				diff = nextdiff(self, 0);
				Px = lastrow[col] + ((left - lastrow[col - 1]) >> 1);
				left = Px + diff;
				//printf("%d %d %d %d %d %x\n",col,diff,left,lastrow[col],lastrow[col-1],&lastrow[col]);
				if (self->linearize) {
					if (left > self->linlen) return LJ92_ERROR_CORRUPT;
					linear = self->linearize[left];
				}
				else
					linear = left;
				thisrow[col++] = left;
				out[c++] = linear;
				if (--write == 0) {
					out += self->skiplen;
					write = self->writelen;
				}
			}
			temprow = lastrow;
			lastrow = thisrow;
			thisrow = temprow;
			if (self->ix >= self->datalen) break;
		}
		if (c >= pixels) ret = LJ92_ERROR_NONE;
		return ret;
	}

	static LJ92_ERRORS parseScan(ljp* self)
	{
		LJ92_ERRORS ret = LJ92_ERROR_CORRUPT;
		memset(self->sssshist, 0, sizeof(self->sssshist));
		self->ix = self->scanstart;
		int compcount = self->data[self->ix + 2];
		int pred = self->data[self->ix + 3 + 2 * compcount];
		if (pred < 0 || pred>7) return ret;
		// Fast path for predictor 2
		if (pred == 2 && self->skiplen == 0 && self->linearize == nullptr)
			return parsePred2NoSkipNoLinearize(self);
		// Fast path for predictor 6
		if (pred == 6)
			return parsePred6(self);
		// Fast path for predictor 1
		if (pred == 1 && self->skiplen == 0 && self->linearize == nullptr )
			return parsePred1NoSkipNoLinearize(self);
		
		self->ix += BEH(self->data[self->ix]);
		self->cnt = 0;
		self->b = 0;
		int write = self->writelen;
		// Now need to decode huffman coded values
		int c = 0;
		int pixels = self->y * self->x;
		u16* out = self->image;
		u16* thisrow = self->outrow[0];
		u16* lastrow = self->outrow[1];

		// First pixel predicted from base value
		int diff;
		int Px;
		int col = 0;
		int row = 0;
		int left = 0;
		while (c < pixels) {
			if (c == 0) {
				Px = 1 << (self->bits - 1);
			}
			else if (row == 0) {
				Px = left;
			}
			else if (col == 0) {
				Px = lastrow[col]; // Use value above for first pixel in row
			}
			else {
				switch (pred) {
				case 0:
					Px = 0; break; // No prediction... should not be used
				case 1:
					Px = left; break;
				case 2:
					Px = lastrow[col]; break;
				case 3:
					Px = lastrow[col - 1]; break;
				case 4:
					Px = left + lastrow[col] - lastrow[col - 1]; break;
				case 5:
					Px = left + ((lastrow[col] - lastrow[col - 1]) >> 1); break;
				case 6:
					Px = lastrow[col] + ((left - lastrow[col - 1]) >> 1); break;
				case 7:
					Px = (left + lastrow[col]) >> 1; break;
				}
			}
			diff = nextdiff(self, Px);
			left = Px + diff;
			//printf("%d %d %d\n",c,diff,left);
			int linear;
			if (self->linearize) {
				if (left > self->linlen) return LJ92_ERROR_CORRUPT;
				linear = self->linearize[left];
			}
			else
				linear = left;
			thisrow[col] = left;
			out[c++] = linear;
			if (++col == self->x) {
				col = 0;
				row++;
				u16* temprow = lastrow;
				lastrow = thisrow;
				thisrow = temprow;
			}
			if (--write == 0) {
				out += self->skiplen;
				write = self->writelen;
			}
			if (self->ix >= self->datalen + 2)
				break;
		}
		if (c >= pixels) ret = LJ92_ERROR_NONE;
		/*for (int h=0;h<17;h++) {
		printf("ssss:%d=%d (%f)\n",h,self->sssshist[h],(float)self->sssshist[h]/(float)(pixels));
		}*/
		return ret;
	}

	static LJ92_ERRORS parseImage(ljp* self)
	{
		LJ92_ERRORS ret = LJ92_ERROR_NONE;
		while (1) {
			int nextMarker = find(self);
			if (nextMarker == 0xc4)
				ret = parseHuff(self);
			else if (nextMarker == 0xc3)
				ret = parseSof3(self);
			else if (nextMarker == 0xfe)// Comment
				ret = parseBlock(self, nextMarker);
			else if (nextMarker == 0xd9) // End of image
				break;
			else if (nextMarker == 0xda) {
				self->scanstart = self->ix;
				ret = LJ92_ERROR_NONE;
				break;
			}
			else if (nextMarker == -1) {
				ret = LJ92_ERROR_CORRUPT;
				break;
			}
			else
				ret = parseBlock(self, nextMarker);
			if (ret != LJ92_ERROR_NONE) break;
		}
		return ret;
	}

	static LJ92_ERRORS findSoI(ljp* self)
	{
		LJ92_ERRORS ret = LJ92_ERROR_CORRUPT;
		if (find(self) == 0xd8)
			ret = parseImage(self);
		return ret;
	}

	static void free_memory(ljp* self)
	{
#ifdef SLOW_HUFF
		free(self->maxcode);
		self->maxcode = NULL;
		free(self->mincode);
		self->mincode = NULL;
		free(self->valptr);
		self->valptr = NULL;
		free(self->huffval);
		self->huffval = NULL;
		free(self->huffsize);
		self->huffsize = NULL;
		free(self->huffcode);
		self->huffcode = NULL;
#else
		free(self->hufflut);
		self->hufflut = nullptr;
#endif
		if (self->rowcache != nullptr) {
			free(self->rowcache);
			self->rowcache = nullptr;
		}
	}

	LJ92_ERRORS lj92_open(lj92& lj, const uint8_t* data, int datalen, int* width, int* height, int* bitdepth/*, std::vector<u16>* pWorkingCache*/)
	{
		ljp* self = &lj;
		memset(self, 0, sizeof(ljp));

		self->data = (u8*)data;
		self->dataend = self->data + datalen;
		self->datalen = datalen;

		LJ92_ERRORS ret = findSoI(self);

		if (ret == LJ92_ERROR_NONE) {
			//if (pWorkingCache == nullptr) {
				u16* rowcache = (u16*)calloc(self->x * 2, sizeof(u16));
				if (rowcache == NULL)
                    ret = LJ92_ERROR_NO_MEMORY;
				else {
					self->rowcache = rowcache;
					self->outrow[0] = rowcache;
					self->outrow[1] = rowcache + self->x;
				}
			/*} else {
				self->rowcache = nullptr;
				pWorkingCache->resize(self->x * 2);
				self->outrow[0] = pWorkingCache->data();
				self->outrow[1] = pWorkingCache->data() + self->x;
			}*/
		}

		if (ret != LJ92_ERROR_NONE)
			free_memory(self);
		else {
			*width = self->x;
			*height = self->y;
			*bitdepth = self->bits;
		}
		return ret;
	}

	LJ92_ERRORS lj92_decode(lj92& lj,
		uint16_t* target, int writeLength, int skipLength,
		uint16_t* linearize, int linearizeLength) 
	{
		LJ92_ERRORS ret = LJ92_ERROR_NONE;
		ljp* self = &lj;
		self->image = target;
		self->writelen = writeLength;
		self->skiplen = skipLength;
		self->linearize = linearize;
		self->linlen = linearizeLength;
		ret = parseScan(self);
		return ret;
	}

	void lj92_close(lj92& lj) 
	{
		ljp* self = &lj;
		if (self != nullptr)
			free_memory(self);
	}
 
    Core::eError LJ92Error(LJ92_ERRORS error)
    {
		switch (error) {
			case LJ92_ERROR_NONE:
			   return Core::eError::None;
			case LJ92_ERROR_CORRUPT:
				return Core::eError::BadImageData;
			case LJ92_ERROR_NO_MEMORY:
				return Core::eError::NotImplmeneted;
			case LJ92_ERROR_BAD_HANDLE:
				return Core::eError::BadImageData;
			case LJ92_ERROR_TOO_WIDE:
				return Core::eError::BadMetadata;
			default:
				return Core::eError::NotImplmeneted;
		}
    }

    extern "C" Core::eError Decode(uint8_t* pOut16Bit, uint32_t outOffsetBytes, uint8_t* pInCompressed, uint32_t compressedSizeBytes, uint32_t width, uint32_t height, uint32_t bitDepth)
    {
        int actualWidth, actualHeight, actualBitDepth;
        lj92 lj;
        auto error = LJ92Error(lj92_open(lj, pInCompressed, compressedSizeBytes, &actualWidth, &actualHeight, &actualBitDepth));
        if ( error == Core::eError::None )
        {
			// Don't compare width/height directly, lossless jpeg bayer compression may change the width and height to improve compression/predictor efficiency
            if ( (actualWidth*actualHeight) == (width*height) && actualBitDepth == bitDepth )
                error = LJ92Error(lj92_decode(lj, (uint16_t*)(pOut16Bit+outOffsetBytes), 0, 0, nullptr, 0));
            else
                error = Core::eError::BadMetadata;
            lj92_close(lj);
        }
        return error;
    }
}
