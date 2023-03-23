#include "LJ92.h"

// Adapted from Adobe DNG SDK 1.5.1: https://github.com/shahminfikri/dng_sdk_1.5.1_-_gpr_sdk_1.0.0/blob/master/dng_sdk/dng_lossless_jpeg.cpp

#include "JpegMarker.h"

#include <assert.h>
#include <vector>

#ifdef _MSC_VER

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

BOOL APIENTRY DllMain(HMODULE hModule, DWORD  ul_reason_for_call, LPVOID lpReserved )
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
	static inline void ThrowBadFormat()
	{
		// Handle error here
	}

	struct sHuffmanTable
	{
		/*
		 * These two fields directly represent the contents of a JPEG DHT
		 * marker
		 */
		uint8_t bits[17];
		uint8_t huffval[256];

		/*
		 * The remaining fields are computed from the above to allow more
		 * efficient coding and decoding.  These fields should be considered
		 * private to the Huffman compression & decompression modules.
		 */
		uint16_t mincode[17];
		int32_t maxcode[18];
		int16_t valptr[17];
		int32_t numbits[256];
		int32_t value[256];

		uint16_t ehufco[256];
		int8_t ehufsi[256];
	};

	// Computes the derived fields in the Huffman table structure.
	static void FixHuffTbl(sHuffmanTable* htbl)
	{

		int32_t l;
		int32_t i;

		const uint32_t bitMask[] =
		{
            0xffffffff, 0x7fffffff, 0x3fffffff, 0x1fffffff,
            0x0fffffff, 0x07ffffff, 0x03ffffff, 0x01ffffff,
            0x00ffffff, 0x007fffff, 0x003fffff, 0x001fffff,
            0x000fffff, 0x0007ffff, 0x0003ffff, 0x0001ffff,
            0x0000ffff, 0x00007fff, 0x00003fff, 0x00001fff,
            0x00000fff, 0x000007ff, 0x000003ff, 0x000001ff,
            0x000000ff, 0x0000007f, 0x0000003f, 0x0000001f,
            0x0000000f, 0x00000007, 0x00000003, 0x00000001
		};

		// Figure C.1: make table of Huffman code length for each symbol
		// Note that this is in code-length order.

		int8_t huffsize[257];

		int32_t p = 0;

		for (l = 1; l <= 16; l++)
		{
			for (i = 1; i <= (int32_t)htbl->bits[l]; i++)
				huffsize[p++] = (int8_t)l;
		}

		huffsize[p] = 0;
		int32_t lastp = p;

		// Figure C.2: generate the codes themselves
		// Note that this is in code-length order.

		uint16_t huffcode[257];
		uint16_t code = 0;
		int32_t si = huffsize[0];

		p = 0;

		while (huffsize[p])
		{
			while (((int32_t)huffsize[p]) == si)
			{
				huffcode[p++] = code;
				code++;
			}

			code <<= 1;

			si++;
		}

		// Figure C.3: generate encoding tables
		// These are code and size indexed by symbol value
		// Set any codeless symbols to have code length 0; this allows
		// EmitBits to detect any attempt to emit such symbols.

		memset(htbl->ehufsi, 0, sizeof(htbl->ehufsi));

		for (p = 0; p < lastp; p++)
		{
			htbl->ehufco[htbl->huffval[p]] = huffcode[p];
			htbl->ehufsi[htbl->huffval[p]] = huffsize[p];
		}

		// Figure F.15: generate decoding tables
		p = 0;

		for (l = 1; l <= 16; l++)
		{
			if (htbl->bits[l])
			{
				htbl->valptr[l] = (int16_t)p;
				htbl->mincode[l] = huffcode[p];

				p += htbl->bits[l];

				htbl->maxcode[l] = huffcode[p - 1];
			}
			else
			{
				htbl->maxcode[l] = -1;
			}
		}

		// We put in this value to ensure HuffDecode terminates.
		htbl->maxcode[17] = 0xFFFFFL;

		// Build the numbits, value lookup tables.
		// These table allow us to gather 8 bits from the bits stream,
		// and immediately lookup the size and value of the huffman codes.
		// If size is zero, it means that more than 8 bits are in the huffman
		// code (this happens about 3-4% of the time).
		memset(htbl->numbits, 0, sizeof(htbl->numbits));

		for (p = 0; p < lastp; p++)
		{
			int32_t size = huffsize[p];

			if (size <= 8)
			{
				int32_t value = htbl->huffval[p];

				code = huffcode[p];

				int32_t ll = code << (8 - size);

				int32_t ul = (size < 8 ? ll | bitMask[24 + size]
					: ll);

				if (ul >= static_cast<int32_t> (sizeof(htbl->numbits) / sizeof(htbl->numbits[0])) ||
					ul >= static_cast<int32_t> (sizeof(htbl->value) / sizeof(htbl->value[0])))
				{
					ThrowBadFormat();
				}

				for (i = ll; i <= ul; i++)
				{
					htbl->numbits[i] = size;
					htbl->value[i] = value;
				}
			}
		}
	}

	/*****************************************************************************/

	/*
	 * The following structure stores basic information about one component.
	 */

	struct sJpegComponentInfo
	{
		/*
		 * These values are fixed over the whole image.
		 * They are read from the SOF marker.
		 */
		int16_t componentId;		/* identifier for this component (0..255) */
		int16_t componentIndex;	/* its index in SOF or cPtr->compInfo[]   */

		/*
		 * Downsampling is not normally used in lossless JPEG, although
		 * it is permitted by the JPEG standard (DIS). We set all sampling
		 * factors to 1 in this program.
		 */
		int16_t hSampFactor;		/* horizontal sampling factor */
		int16_t vSampFactor;		/* vertical sampling factor   */

		/*
		 * Huffman table selector (0..3). The value may vary
		 * between scans. It is read from the SOS marker.
		 */
		int16_t dcTblNo;
	};

	/*
	 * One of the following structures is used to pass around the
	 * decompression information.
	 */
	struct sDecompressInfo
	{
		/*
		 * Image width, height, and image data precision (bits/sample)
		 * These fields are set by ReadFileHeader or ReadScanHeader
		 */
		int32_t imageWidth;
		int32_t imageHeight;
		int32_t dataPrecision;

		/*
		 * compInfo[i] describes component that appears i'th in SOF
		 * numComponents is the # of color components in JPEG image.
		 */
		sJpegComponentInfo* compInfo;
		int16_t numComponents;

		/*
		 * *curCompInfo[i] describes component that appears i'th in SOS.
		 * compsInScan is the # of color components in current scan.
		 */
		sJpegComponentInfo* curCompInfo[4];
		int16_t compsInScan;

		/*
		 * MCUmembership[i] indexes the i'th component of MCU into the
		 * curCompInfo array.
		 */
		int16_t MCUmembership[10];

		/*
		 * ptrs to Huffman coding tables, or NULL if not defined
		 */
		sHuffmanTable* dcHuffTblPtrs[4];

		/*
		 * prediction selection value (PSV) and point transform parameter (Pt)
		 */
		int32_t Ss;
		int32_t Pt;

		/*
		 * In lossless JPEG, restart interval shall be an integer
		 * multiple of the number of MCU in a MCU row.
		 */
		int32_t restartInterval;/* MCUs per restart interval, 0 = no restart */
		int32_t restartInRows; /*if > 0, MCU rows per restart interval; 0 = no restart*/

		/*
		 * these fields are private data for the entropy decoder
		 */
		int32_t restartRowsToGo;	/* MCUs rows left in this restart interval */
		int16_t nextRestartNum;	/* # of next RSTn marker (0..7) */
	};


	typedef uint16_t ComponentType;
	typedef ComponentType* MCU;

	class DecoderInput
	{
	public:
		DecoderInput(uint8_t* pStream)
			: m_pStream(pStream)
			, m_position(0ull)
		{}

		inline uint8_t Get_uint8() { return m_pStream[m_position++]; }
		inline uint64_t Position() const { return m_position; }
		inline void SetReadPosition(uint64_t position) { m_position = position; }
		inline void Skip(uint64_t length) { m_position += length; }

	private:

		uint8_t* m_pStream;
		uint64_t m_position;
	};

	class DecoderOutput
	{
	public:
		DecoderOutput(uint8_t* pOutput, uint64_t outputSize)
			: m_pOutput(pOutput)
			, m_pBufferEnd(pOutput + outputSize)
		{
		}

		inline void Spool(const void* pData, uint32_t count)
		{
			assert((m_pOutput + count) <= m_pBufferEnd);

			memcpy(m_pOutput, pData, count);
			m_pOutput += count;
		}

	private:

		uint8_t* m_pOutput;
		uint8_t* m_pBufferEnd;
	};
 
    class LosslessJpegAllocator
    {
    public:
        LosslessJpegAllocator(uint8_t* pBuffer, uint64_t bufferSize)
        : m_pBuffer(pBuffer)
        , m_bufferSize(bufferSize)
        {}
        
        inline bool CanAllocate(uint64_t size)
        {
            return ( size <= m_bufferSize );
        }
        
        inline uint8_t* Allocate(uint64_t size)
        {
            if ( CanAllocate(size) )
            {
                const auto pBuffer = m_pBuffer;
                m_pBuffer += size;
                m_bufferSize -= size;
                return pBuffer;
            }
            return nullptr;
        }
    
    private:
    
        uint8_t* m_pBuffer;
        uint64_t m_bufferSize;
    };
    
    class LosslessJpegMemory
	{
	public:
 
        LosslessJpegMemory(LosslessJpegAllocator* pAllocator)
        : m_pStaticBuffer(nullptr)
        , m_pAllocator(pAllocator)
        {}

		inline void Allocate(uint64_t size)
		{
            if ( m_pAllocator && m_pAllocator->CanAllocate(size))
                m_pStaticBuffer = m_pAllocator->Allocate(size);
            else
                m_data.resize(size);
		}

		inline void Allocate(uint64_t count, uint64_t elementSize)
		{
            const auto size = count * elementSize;
            if ( m_pAllocator && m_pAllocator->CanAllocate(size))
                m_pStaticBuffer = m_pAllocator->Allocate(size);
            else
                m_data.resize(count * size);
		}

		inline uint8_t* Buffer() { return m_pStaticBuffer ? m_pStaticBuffer : m_data.data(); }

	private:

        LosslessJpegAllocator* m_pAllocator;
        uint8_t* m_pStaticBuffer;
		std::vector<uint8_t> m_data;
	};

	class LosslessJpegDecoder
	{
	public:

		LosslessJpegDecoder(DecoderInput* stream, DecoderOutput* spooler, bool bug16, LosslessJpegAllocator* pAllocator = nullptr)
			: fStream(stream)
			, fSpooler(spooler)
			, fBug16(bug16)
            , huffmanBuffer{pAllocator, pAllocator, pAllocator, pAllocator}
			, compInfoBuffer(pAllocator)
			, info()
			, mcuBuffer1(pAllocator)
			, mcuBuffer2(pAllocator)
			, mcuBuffer3(pAllocator)
			, mcuBuffer4(pAllocator)
			, mcuROW1(nullptr)
			, mcuROW2(nullptr)
			, getBuffer(0)
			, bitsLeft(0)
		{
			memset(&info, 0, sizeof(info));
		}

		void StartRead(uint32_t& imageWidth, uint32_t& imageHeight, uint32_t& imageChannels)
		{
			ReadFileHeader();
			ReadScanHeader();
			DecoderStructInit();
			HuffDecoderInit();

			imageWidth = info.imageWidth;
			imageHeight = info.imageHeight;
			imageChannels = info.compsInScan;
		}

		void FinishRead()
		{
			DecodeImage();
		}

	private:

		inline uint8_t GetJpegChar()
		{
			return fStream->Get_uint8();
		}

		inline void UnGetJpegChar()
		{
			fStream->SetReadPosition(fStream->Position() - 1);
		}

		inline uint16_t Get2bytes()
		{
			uint16_t a = GetJpegChar();
			return (uint16_t)((a << 8) + GetJpegChar());
		}

		inline void SkipVariable()
		{
			uint32_t length = Get2bytes() - 2;
			fStream->Skip(length);
		}

		void GetDht()
		{
			int32_t length = Get2bytes() - 2;

			while (length > 0)
			{

				int32_t index = GetJpegChar();

				if (index < 0 || index >= 4)
				{
					ThrowBadFormat();
				}

				sHuffmanTable*& htblptr = info.dcHuffTblPtrs[index];

				if (htblptr == NULL)
				{
					huffmanBuffer[index].Allocate(sizeof(sHuffmanTable));
					htblptr = (sHuffmanTable*)huffmanBuffer[index].Buffer();
				}

				htblptr->bits[0] = 0;

				int32_t count = 0;

				for (int32_t i = 1; i <= 16; i++)
				{
					htblptr->bits[i] = GetJpegChar();
					count += htblptr->bits[i];
				}

				if (count > 256)
				{
					ThrowBadFormat();
				}

				for (int32_t j = 0; j < count; j++)
				{
					htblptr->huffval[j] = GetJpegChar();
				}

				length -= 1 + 16 + count;
			}
		}

		void GetDri()
		{
			if (Get2bytes() != 4)
			{
				ThrowBadFormat();
			}
			info.restartInterval = Get2bytes();
		}

		inline void GetApp0()
		{
			SkipVariable();
		}

		void GetSof(int32_t code)
		{
			int32_t length = Get2bytes();

			info.dataPrecision = GetJpegChar();
			info.imageHeight = Get2bytes();
			info.imageWidth = Get2bytes();
			info.numComponents = GetJpegChar();

			// We don't support files in which the image height is initially
			// specified as 0 and is later redefined by DNL.  As long as we
			// have to check that, might as well have a general sanity check.
			if ((info.imageHeight <= 0) || (info.imageWidth <= 0) || (info.numComponents <= 0))
			{
				ThrowBadFormat();
			}

			// Lossless JPEG specifies data precision to be from 2 to 16 bits/sample.

			const int32_t MinPrecisionBits = 2;
			const int32_t MaxPrecisionBits = 16;

			if ((info.dataPrecision < MinPrecisionBits) || (info.dataPrecision > MaxPrecisionBits))
			{
				ThrowBadFormat();
			}

			// Check length of tag.
			if (length != (info.numComponents * 3 + 8))
			{
				ThrowBadFormat();
			}

			// Allocate per component info.

			// We can cast info.numComponents to a uint32_t because the check above
			// guarantees that it cannot be negative.
			compInfoBuffer.Allocate(static_cast<uint32_t> (info.numComponents),
				sizeof(sJpegComponentInfo));

			info.compInfo = (sJpegComponentInfo*)compInfoBuffer.Buffer();

			// Read in the per compent info.
			for (int32_t ci = 0; ci < info.numComponents; ci++)
			{
				sJpegComponentInfo* compptr = &info.compInfo[ci];

				compptr->componentIndex = (int16_t)ci;

				compptr->componentId = GetJpegChar();

				int32_t c = GetJpegChar();

				compptr->hSampFactor = (int16_t)((c >> 4) & 15);
				compptr->vSampFactor = (int16_t)((c) & 15);

				(void)GetJpegChar();   /* skip Tq */
			}
		}

		void GetSos()
		{
			int32_t length = Get2bytes();

			// Get the number of image components.

			int32_t n = GetJpegChar();
			info.compsInScan = (int16_t)n;

			// Check length.

			length -= 3;

			if (length != (n * 2 + 3) || n < 1 || n > 4)
			{
				ThrowBadFormat();
			}

			// Find index and huffman table for each component.
			for (int32_t i = 0; i < n; i++)
			{

				int32_t cc = GetJpegChar();
				int32_t c = GetJpegChar();

				int32_t ci;

				for (ci = 0; ci < info.numComponents; ci++)
				{
					if (cc == info.compInfo[ci].componentId)
					{
						break;
					}
				}

				if (ci >= info.numComponents)
				{
					ThrowBadFormat();
				}

				sJpegComponentInfo* compptr = &info.compInfo[ci];

				info.curCompInfo[i] = compptr;

				compptr->dcTblNo = (int16_t)((c >> 4) & 15);
			}

			// Get the PSV, skip Se, and get the point transform parameter.

			info.Ss = GetJpegChar();

			(void)GetJpegChar();

			info.Pt = GetJpegChar() & 0x0F;
		}

		void GetSoi()
		{
			// Reset all parameters that are defined to be reset by SOI
			info.restartInterval = 0;
		}

		int32_t NextMarker()
		{
			int32_t c;

			do
			{
				// skip any non-FF bytes
				do
				{
					c = GetJpegChar();
				} while (c != 0xFF);

				// skip any duplicate FFs, since extra FFs are legal
				do
				{
					c = GetJpegChar();
				} while (c == 0xFF);

			} while (c == 0);		// repeat if it was a stuffed FF/00

			return c;
		}

		JpegMarker ProcessTables()
		{
			while (true)
			{
				int32_t c = NextMarker();

				switch (c)
				{
				case M_SOF0:
				case M_SOF1:
				case M_SOF2:
				case M_SOF3:
				case M_SOF5:
				case M_SOF6:
				case M_SOF7:
				case M_JPG:
				case M_SOF9:
				case M_SOF10:
				case M_SOF11:
				case M_SOF13:
				case M_SOF14:
				case M_SOF15:
				case M_SOI:
				case M_EOI:
				case M_SOS:
					return (JpegMarker)c;
				case M_DHT:
					GetDht();
					break;
				case M_DQT:
					break;
				case M_DRI:
					GetDri();
					break;
				case M_APP0:
					GetApp0();
					break;
				case M_RST0:	// these are all parameterless
				case M_RST1:
				case M_RST2:
				case M_RST3:
				case M_RST4:
				case M_RST5:
				case M_RST6:
				case M_RST7:
				case M_TEM:
					break;
				default:		// must be DNL, DHP, EXP, APPn, JPGn, COM, or RESn
					SkipVariable();
					break;
				}
			}

			return M_ERROR;
		}

		void ReadFileHeader()
		{
			// Demand an SOI marker at the start of the stream --- otherwise it's
			// probably not a JPEG stream at all.
			int32_t c = GetJpegChar();
			int32_t c2 = GetJpegChar();

			if ((c != 0xFF) || (c2 != M_SOI))
			{
				ThrowBadFormat();
			}

			// OK, process SOI
			GetSoi();

			// Process markers until SOF
			c = ProcessTables();

			switch (c)
			{
			case M_SOF0:
			case M_SOF1:
			case M_SOF3:
				GetSof(c);
				break;
			default:
				ThrowBadFormat();
				break;
			}
		}

		int32_t ReadScanHeader()
		{
			// Process markers until SOS or EOI
			int32_t c = ProcessTables();

			switch (c)
			{

			case M_SOS:
				GetSos();
				return 1;
			case M_EOI:
				return 0;
			default:
				ThrowBadFormat();
				break;
			}

			return 0;
		}

		void DecoderStructInit()
		{
			int32_t ci;
			{
				// Check sampling factor validity.
				for (ci = 0; ci < info.numComponents; ci++)
				{
					sJpegComponentInfo* compPtr = &info.compInfo[ci];

					if (compPtr->hSampFactor != 1 ||
						compPtr->vSampFactor != 1)
					{
						ThrowBadFormat();
					}
				}
			}

			// Prepare array describing MCU composition.
			if (info.compsInScan < 0 || info.compsInScan > 4)
			{
				ThrowBadFormat();
			}

			for (ci = 0; ci < info.compsInScan; ci++)
			{
				info.MCUmembership[ci] = (int16_t)ci;
			}

			// Initialize mucROW1 and mcuROW2 which buffer two rows of
			// pixels for predictor calculation.

			// This multiplication cannot overflow because info.compsInScan is
			// guaranteed to be between 0 and 4 inclusive (see checks above).

			int32_t mcuSize = info.compsInScan * (uint32_t)sizeof(ComponentType);

			mcuBuffer1.Allocate(info.imageWidth, sizeof(MCU));
			mcuBuffer2.Allocate(info.imageWidth, sizeof(MCU));

			mcuROW1 = (MCU*)mcuBuffer1.Buffer();
			mcuROW2 = (MCU*)mcuBuffer2.Buffer();

			mcuBuffer3.Allocate(info.imageWidth, mcuSize);
			mcuBuffer4.Allocate(info.imageWidth, mcuSize);

			mcuROW1[0] = (ComponentType*)mcuBuffer3.Buffer();
			mcuROW2[0] = (ComponentType*)mcuBuffer4.Buffer();

			for (int32_t j = 1; j < info.imageWidth; j++)
			{
				mcuROW1[j] = mcuROW1[j - 1] + info.compsInScan;
				mcuROW2[j] = mcuROW2[j - 1] + info.compsInScan;
			}
		}

		void HuffDecoderInit()
		{
			// Initialize bit parser state
			getBuffer = 0;
			bitsLeft = 0;

			// Prepare Huffman tables.
			for (int16_t ci = 0; ci < info.compsInScan; ci++)
			{
				sJpegComponentInfo* compptr = info.curCompInfo[ci];

				// Make sure requested tables are present
				if (compptr->dcTblNo < 0 || compptr->dcTblNo > 3)
				{
					ThrowBadFormat();
				}

				if (info.dcHuffTblPtrs[compptr->dcTblNo] == NULL)
				{
					ThrowBadFormat();
				}

				// Compute derived values for Huffman tables.
				// We may do this more than once for same table, but it's not a
				// big deal
				FixHuffTbl(info.dcHuffTblPtrs[compptr->dcTblNo]);
			}

			// Initialize restart stuff
			info.restartInRows = info.restartInterval / info.imageWidth;
			info.restartRowsToGo = info.restartInRows;
			info.nextRestartNum = 0;
		}

		inline void ProcessRestart()
		{
			// Throw away and unused odd bits in the bit buffer.
			fStream->SetReadPosition(fStream->Position() - bitsLeft / 8);

			bitsLeft = 0;
			getBuffer = 0;

			// Scan for next JPEG marker
			int32_t c;

			do
			{
				// skip any non-FF bytes
				do
				{
					c = GetJpegChar();
				} while (c != 0xFF);

				// skip any duplicate FFs
				do
				{
					c = GetJpegChar();
				} while (c == 0xFF);

			} while (c == 0);		// repeat if it was a stuffed FF/00

			// Verify correct restart code.
			if (c != (M_RST0 + info.nextRestartNum))
			{
				ThrowBadFormat();
			}

			// Update restart state.
			info.restartRowsToGo = info.restartInRows;
			info.nextRestartNum = (info.nextRestartNum + 1) & 7;
		}

		inline void FillBitBuffer(int32_t nbits)
		{
			const int32_t kMinGetBits = sizeof(uint32_t) * 8 - 7;

			while (bitsLeft < kMinGetBits)
			{
				int32_t c = GetJpegChar();

				// If it's 0xFF, check and discard stuffed zero byte
				if (c == 0xFF)
				{
					int32_t c2 = GetJpegChar();

					if (c2 != 0)
					{
						// Oops, it's actually a marker indicating end of
						// compressed data.  Better put it back for use later.
						UnGetJpegChar();
						UnGetJpegChar();

						// There should be enough bits still left in the data
						// segment; if so, just break out of the while loop.
						if (bitsLeft >= nbits)
							break;

						// Uh-oh.  Corrupted data: stuff zeroes into the data
						// stream, since this sometimes occurs when we are on the
						// last show_bits8 during decoding of the Huffman
						// segment.
						c = 0;
					}
				}

				getBuffer = (getBuffer << 8) | c;
				bitsLeft += 8;
			}
		}

		inline int32_t show_bits8()
		{
			if (bitsLeft < 8)
				FillBitBuffer(8);

			return (int32_t)((getBuffer >> (bitsLeft - 8)) & 0xff);
		}

		inline void flush_bits(int32_t nbits)
		{
			bitsLeft -= nbits;
		}

		inline int32_t get_bits(int32_t nbits)
		{
			if (nbits > 16)
			{
				ThrowBadFormat();
			}

			if (bitsLeft < nbits)
				FillBitBuffer(nbits);

			return (int32_t)((getBuffer >> (bitsLeft -= nbits)) & (0x0FFFF >> (16 - nbits)));
		}

		inline int32_t get_bit()
		{
			if (!bitsLeft)
				FillBitBuffer(1);

			return (int32_t)((getBuffer >> (--bitsLeft)) & 1);
		}

		inline int32_t HuffDecode(sHuffmanTable* htbl)
		{
			// If the huffman code is less than 8 bits, we can use the fast
			// table lookup to get its value.  It's more than 8 bits about
			// 3-4% of the time.
			int32_t code = show_bits8();

			if (htbl->numbits[code])
			{
				flush_bits(htbl->numbits[code]);

				return htbl->value[code];
			}
			else
			{
				flush_bits(8);

				int32_t l = 8;

				while (code > htbl->maxcode[l])
				{
					code = (code << 1) | get_bit();
					l++;
				}

				// With garbage input we may reach the sentinel value l = 17.
				if (l > 16)
				{
					return 0;		// fake a zero as the safest result
				}
				else
				{
					return htbl->huffval[htbl->valptr[l] +
						((int32_t)(code - htbl->mincode[l]))];
				}
			}
		}

#ifdef __clang__
		__attribute__((no_sanitize("undefined")))
#endif
		inline void HuffExtend(int32_t& x, int32_t s)
		{
			if (x < (0x08000 >> (16 - s)))
			{
				x += (-1 << s) + 1;
			}
		}

		inline void PmPutRow(MCU* buf, int32_t numComp, int32_t numCol, int32_t row)
		{
			uint16_t* sPtr = &buf[0][0];

			uint32_t pixels = numCol * numComp;

			fSpooler->Spool(sPtr, pixels * (uint32_t)sizeof(uint16_t));
		}

		inline void DecodeFirstRow(MCU* curRowBuf)
		{
			int32_t compsInScan = info.compsInScan;

			// Process the first column in the row.
			for (int32_t curComp = 0; curComp < compsInScan; curComp++)
			{
				int32_t ci = info.MCUmembership[curComp];

				sJpegComponentInfo* compptr = info.curCompInfo[ci];

				sHuffmanTable* dctbl = info.dcHuffTblPtrs[compptr->dcTblNo];

				// Section F.2.2.1: decode the difference
				int32_t d = 0;
				int32_t s = HuffDecode(dctbl);

				if (s)
				{
					if (s == 16 && !fBug16)
					{
						d = -32768;
					}
					else
					{
						d = get_bits(s);
						HuffExtend(d, s);
					}
				}

				// Add the predictor to the difference.
				int32_t Pr = info.dataPrecision;
				int32_t Pt = info.Pt;

				curRowBuf[0][curComp] = (ComponentType)(d + (1 << (Pr - Pt - 1)));
			}

			// Process the rest of the row.
			int32_t numCOL = info.imageWidth;

			for (int32_t col = 1; col < numCOL; col++)
			{
				for (int32_t curComp = 0; curComp < compsInScan; curComp++)
				{
					int32_t ci = info.MCUmembership[curComp];

					sJpegComponentInfo* compptr = info.curCompInfo[ci];

					sHuffmanTable* dctbl = info.dcHuffTblPtrs[compptr->dcTblNo];

					// Section F.2.2.1: decode the difference
					int32_t d = 0;

					int32_t s = HuffDecode(dctbl);

					if (s)
					{
						if (s == 16 && !fBug16)
						{
							d = -32768;
						}
						else
						{
							d = get_bits(s);
							HuffExtend(d, s);
						}
					}

					// Add the predictor to the difference.
					curRowBuf[col][curComp] = (ComponentType)(d + curRowBuf[col - 1][curComp]);
				}
			}

			// Update the restart counter
			if (info.restartInRows)
			{
				info.restartRowsToGo--;
			}
		}

#define swap(type,a,b) {type c; c=(a); (a)=(b); (b)=c;}

        void DecodeImage2ComponentsPredictor7()
        {
            int32_t numCOL = info.imageWidth;
			int32_t numROW = info.imageHeight;
			const int32_t compsInScan = 2;
            assert(info.compsInScan == compsInScan);

			// Precompute the decoding table for each table.
			sHuffmanTable* ht[4];

			memset(ht, 0, sizeof(ht));

			for (int32_t curComp = 0; curComp < compsInScan; curComp++)
			{
				int32_t ci = info.MCUmembership[curComp];

				sJpegComponentInfo* compptr = info.curCompInfo[ci];

				ht[curComp] = info.dcHuffTblPtrs[compptr->dcTblNo];
			}

			MCU* prevRowBuf = mcuROW1;
			MCU* curRowBuf = mcuROW2;

			// Decode the first row of image. Output the row and
			// turn this row into a previous row for later predictor
			// calculation.
			DecodeFirstRow(mcuROW1);
			PmPutRow(mcuROW1, compsInScan, numCOL, 0);

			// Process each row.
			for (int32_t row = 1; row < numROW; row++)
			{
				// Account for restart interval, process restart marker if needed.
				if (info.restartInRows)
				{
					if (info.restartRowsToGo == 0)
					{
						ProcessRestart();

						// Reset predictors at restart.
						DecodeFirstRow(curRowBuf);

						PmPutRow(curRowBuf, compsInScan, numCOL, row);

						swap(MCU*, prevRowBuf, curRowBuf);

						continue;
					}

					info.restartRowsToGo--;
				}

				// The upper neighbors are predictors for the first column.
				{
                    const int32_t curComp = 0;
					// Section F.2.2.1: decode the difference
					int32_t d = 0;
					int32_t s = HuffDecode(ht[curComp]);

					if (s)
					{
						if (s == 16 && !fBug16)
						{
							d = -32768;
						}
						else
						{
							d = get_bits(s);
							HuffExtend(d, s);
						}
					}

					// First column of row above is predictor for first column.
					curRowBuf[0][curComp] = (ComponentType)(d + prevRowBuf[0][curComp]);
				}
                {
                    const int32_t curComp = 1;
					// Section F.2.2.1: decode the difference
					int32_t d = 0;
					int32_t s = HuffDecode(ht[curComp]);

					if (s)
					{
						if (s == 16 && !fBug16)
						{
							d = -32768;
						}
						else
						{
							d = get_bits(s);
							HuffExtend(d, s);
						}
					}

					// First column of row above is predictor for first column.
					curRowBuf[0][curComp] = (ComponentType)(d + prevRowBuf[0][curComp]);
				}
                
                for (int32_t col = 1; col < numCOL; col++)
                {
                    {
                        const int32_t curComp = 0;
                        // Section F.2.2.1: decode the difference
                        int32_t d = 0;
                        int32_t s = HuffDecode(ht[curComp]);

                        if (s)
                        {
                            if (s == 16 && !fBug16)
                            {
                                d = -32768;
                            }
                            else
                            {
                                d = get_bits(s);
                                HuffExtend(d, s);
                            }
                        }

                        // Predict the pixel value.
                        const int32_t upper = prevRowBuf[col][curComp];
                        const int32_t left = curRowBuf[col - 1][curComp];
                        int32_t predictor = (left + upper) >> 1;
                        
                        // Save the difference.
                        curRowBuf[col][curComp] = (ComponentType)(d + predictor);
                    }
                    {
                        const int32_t curComp = 1;
                        // Section F.2.2.1: decode the difference
                        int32_t d = 0;
                        int32_t s = HuffDecode(ht[curComp]);

                        if (s)
                        {
                            if (s == 16 && !fBug16)
                            {
                                d = -32768;
                            }
                            else
                            {
                                d = get_bits(s);
                                HuffExtend(d, s);
                            }
                        }

                        // Predict the pixel value.
                        const int32_t upper = prevRowBuf[col][curComp];
                        const int32_t left = curRowBuf[col - 1][curComp];
                        int32_t predictor = (left + upper) >> 1;
                        
                        // Save the difference.
                        curRowBuf[col][curComp] = (ComponentType)(d + predictor);
                    }
                }
				

				PmPutRow(curRowBuf, compsInScan, numCOL, row);
				swap(MCU*, prevRowBuf, curRowBuf);
			}
        }

		void DecodeImage()
		{
			int32_t numCOL = info.imageWidth;
			int32_t numROW = info.imageHeight;
			int32_t compsInScan = info.compsInScan;
   
            if ( compsInScan == 2 && info.Ss == 7 )
            {
                DecodeImage2ComponentsPredictor7();
                return;
            }

			// Precompute the decoding table for each table.

			sHuffmanTable* ht[4];

			memset(ht, 0, sizeof(ht));

			for (int32_t curComp = 0; curComp < compsInScan; curComp++)
			{
				int32_t ci = info.MCUmembership[curComp];

				sJpegComponentInfo* compptr = info.curCompInfo[ci];

				ht[curComp] = info.dcHuffTblPtrs[compptr->dcTblNo];
			}

			MCU* prevRowBuf = mcuROW1;
			MCU* curRowBuf = mcuROW2;

			// Decode the first row of image. Output the row and
			// turn this row into a previous row for later predictor
			// calculation.
			DecodeFirstRow(mcuROW1);
			PmPutRow(mcuROW1, compsInScan, numCOL, 0);

			// Process each row.
			for (int32_t row = 1; row < numROW; row++)
			{
				// Account for restart interval, process restart marker if needed.
				if (info.restartInRows)
				{
					if (info.restartRowsToGo == 0)
					{
						ProcessRestart();

						// Reset predictors at restart.
						DecodeFirstRow(curRowBuf);

						PmPutRow(curRowBuf, compsInScan, numCOL, row);

						swap(MCU*, prevRowBuf, curRowBuf);

						continue;
					}

					info.restartRowsToGo--;
				}

				// The upper neighbors are predictors for the first column.
				for (int32_t curComp = 0; curComp < compsInScan; curComp++)
				{
					// Section F.2.2.1: decode the difference
					int32_t d = 0;
					int32_t s = HuffDecode(ht[curComp]);

					if (s)
					{
						if (s == 16 && !fBug16)
						{
							d = -32768;
						}
						else
						{
							d = get_bits(s);
							HuffExtend(d, s);
						}
					}

					// First column of row above is predictor for first column.
					curRowBuf[0][curComp] = (ComponentType)(d + prevRowBuf[0][curComp]);
				}

				// For the rest of the column on this row, predictor
				// calculations are based on PSV. 
				if (compsInScan == 2 && info.Ss == 1 && numCOL > 1)
				{
					// This is the combination used by both the Canon and Kodak raw formats. 
					// Unrolling the general case logic results in a significant speed increase.
					uint16_t* dPtr = &curRowBuf[1][0];

					int32_t prev0 = dPtr[-2];
					int32_t prev1 = dPtr[-1];

					for (int32_t col = 1; col < numCOL; col++)
					{
						int32_t s = HuffDecode(ht[0]);

						if (s)
						{

							int32_t d;

							if (s == 16 && !fBug16)
							{
								d = -32768;
							}
							else
							{
								d = get_bits(s);
								HuffExtend(d, s);
							}

							prev0 += d;
						}

						s = HuffDecode(ht[1]);

						if (s)
						{
							int32_t d;

							if (s == 16 && !fBug16)
							{
								d = -32768;
							}
							else
							{
								d = get_bits(s);
								HuffExtend(d, s);
							}

							prev1 += d;
						}

						dPtr[0] = (uint16_t)prev0;
						dPtr[1] = (uint16_t)prev1;

						dPtr += 2;
					}
				}
				else
				{
					for (int32_t col = 1; col < numCOL; col++)
					{
						for (int32_t curComp = 0; curComp < compsInScan; curComp++)
						{
							// Section F.2.2.1: decode the difference
							int32_t d = 0;
							int32_t s = HuffDecode(ht[curComp]);

							if (s)
							{
								if (s == 16 && !fBug16)
								{
									d = -32768;
								}
								else
								{
									d = get_bits(s);
									HuffExtend(d, s);
								}
							}

							// Predict the pixel value.
                            int32_t predictor = 0;
                            {
                                const int32_t diag = prevRowBuf[col - 1][curComp];
                                const int32_t upper = prevRowBuf[col][curComp];
                                const int32_t left = curRowBuf[col - 1][curComp];

                                switch (info.Ss)
                                {
                                case 1:
                                    predictor = left;
                                    break;
                                case 2:
                                    predictor = upper;
                                    break;
                                case 3:
                                    predictor = diag;
                                    break;
                                case 4:
                                    predictor = left + upper - diag;
                                    break;
                                case 5:
                                    predictor = left + ((upper - diag) >> 1);
                                    break;
                                case 6:
                                    predictor = upper + ((left - diag) >> 1);
                                    break;
                                case 7:
                                    predictor = (left + upper) >> 1;
                                    break;
                                }
                            }
                            
							// Save the difference.
							curRowBuf[col][curComp] = (ComponentType)(d + predictor);
						}
					}
				}

				PmPutRow(curRowBuf, compsInScan, numCOL, row);
				swap(MCU*, prevRowBuf, curRowBuf);
			}
		}
  
  #undef swap

		DecoderInput* fStream;		// Input data.

		DecoderOutput* fSpooler;		// Output data.

		bool fBug16;				// Decode data with the "16-bit" bug.

		LosslessJpegMemory huffmanBuffer[4];

		LosslessJpegMemory compInfoBuffer;

		sDecompressInfo info;

		LosslessJpegMemory mcuBuffer1;
		LosslessJpegMemory mcuBuffer2;
		LosslessJpegMemory mcuBuffer3;
		LosslessJpegMemory mcuBuffer4;

		MCU* mcuROW1;
		MCU* mcuROW2;

		uint64_t getBuffer;			// current bit-extraction buffer
		int32_t bitsLeft;
	};
    
	extern "C" Core::eError Decode(uint8_t* pOut16Bit, uint32_t outOffsetBytes, uint8_t* pInCompressed, uint32_t inOffsetBytes, uint32_t compressedSizeBytes, uint32_t width, uint32_t height, uint32_t bitDepth)
	{
        DecoderInput stream(pInCompressed + inOffsetBytes);
		DecoderOutput output(pOut16Bit + outOffsetBytes, width * height * sizeof(uint16_t));

		LosslessJpegDecoder decoder(&stream, &output, false);

		uint32_t imageWidth;
		uint32_t imageHeight;
		uint32_t imageChannels;
		decoder.StartRead(imageWidth, imageHeight, imageChannels);
		if (imageWidth * imageHeight * imageChannels != width * height)
			return Core::eError::BadMetadata;
		decoder.FinishRead();

		if (stream.Position() > compressedSizeBytes)
			return Core::eError::BadImageData;

		return Core::eError::None;
	}
}
