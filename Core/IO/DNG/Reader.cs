using Octopus.Player.Core.Decoders;
using Octopus.Player.Core.Maths;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TiffLibrary;

namespace Octopus.Player.Core.IO.DNG
{
    public sealed class Reader : IDisposable
    {
        public string FilePath { get; private set; }
        public bool Valid { get; private set; }

        private TiffFileReader Tiff { get; set; }
        private TiffFieldReader FieldReader { get; set; }
        private TiffImageFileDirectory Ifd { get; set; }
        private TiffTagReader TagReader { get; set; }

        private Vector2i? CachedDimensions { get; set; }
        private Rational? CachedFramerate { get; set; }
        private uint? CachedBitDepth { get; set; }
        private uint? CachedDecodedBitDepth { get; set; }
        private CFAPattern? CachedCFAPattern { get; set; }
        private Vector2i? CachedCFARepeatPatternDimensions { get; set; }
        private Compression? CachedCompression { get; set; }
        private PhotometricInterpretation? CachedPhotometricInterpretation { get; set; }
        private bool? CachedIsTiled { get; set; }
        private uint? CachedTileCount { get; set; }
        private uint? CachedStripCount { get; set; }
        private Vector2i? CachedTileDimensions { get; set; }
        private ushort[] CachedLinearizationTable { get; set; }
        private ushort? CachedBlackLevel { get; set; }
        private ushort? CachedWhiteLevel { get; set; }

        public Reader(string filePath)
        {
            // Open TIFF file
            Tiff = TiffFileReader.Open(filePath);
            if (Tiff == null)
                return;
            Valid = true;
            FilePath = filePath;

            // Set up IFD field reader
            FieldReader = Tiff.CreateFieldReader();
            Ifd = Tiff.ReadImageFileDirectory();
            TagReader = new TiffTagReader(FieldReader, Ifd);

            /*
            // Get offsets to the strip/tile data
            TiffValueCollection<ulong> offsets, byteCounts;
            if (ifd.Contains(TiffTag.TileOffsets))
            {
                offsets = tagReader.ReadTileOffsets();
                byteCounts = tagReader.ReadTileByteCounts();
            }
            else if (ifd.Contains(TiffTag.StripOffsets))
            {
                offsets = tagReader.ReadStripOffsets();
                byteCounts = tagReader.ReadStripByteCounts();
            }
            else
            {
                throw new InvalidDataException("This TIFF file is neither striped or tiled.");
            }
            if (offsets.Count != byteCounts.Count)
            {
                throw new InvalidDataException();
            }

            // Extract strip/tile data
            using var contentReader = tiff.CreateContentReader();
            int count = offsets.Count;
            for (int i = 0; i < count; i++)
            {
                long offset = (long)offsets[i];
                int byteCount = (int)byteCounts[i];
                byte[] data = System.Buffers.ArrayPool<byte>.Shared.Rent(byteCount);
                try
                {
                    contentReader.Read(offset, data.AsMemory(0, byteCount));
                    using var fs = new FileStream(@$"C:\Test\extracted-{i}.dat", FileMode.Create, FileAccess.Write);
                    fs.Write(data, 0, byteCount);
                }
                finally
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(data);
                }
            }
            */
        }

        public Error DecodeImageData(byte[] dataOut)
        {
            CachedIsTiled = false;
            Valid = false;

            // Get offsets to the strip/tile data
            TiffValueCollection<ulong> offsets, byteCounts;
            if (Ifd.Contains(TiffTag.TileOffsets))
            {
                CachedIsTiled = true;
                offsets = TagReader.ReadTileOffsets();
                byteCounts = TagReader.ReadTileByteCounts();
                CachedTileCount = (uint)offsets.Count;
            }
            else if (Ifd.Contains(TiffTag.StripOffsets))
            {
                offsets = TagReader.ReadStripOffsets();
                byteCounts = TagReader.ReadStripByteCounts();
                CachedStripCount = (uint)offsets.Count;
            }
            else
                return Error.BadImageData;
            if (offsets.Count != byteCounts.Count)
                return Error.BadImageData;
            Valid = true;

            switch(Compression)
            {
                case Compression.LosslessJPEG:
                    return DecodeCompressedImageData(ref offsets, ref byteCounts, dataOut);
                case Compression.None:
                    return DecodeUncompressedImageData(ref offsets, ref byteCounts, dataOut);
                default:
                    return Error.NotImplmeneted;
            }
        }

        private Error DecodeUncompressedImageData(ref TiffValueCollection<ulong> offsets, ref TiffValueCollection<ulong> byteCounts, byte[] dataOut/*, bool multithread = false*/)
        {
            using var contentReader = Tiff.CreateContentReader();
            var offsetsCount = offsets.Count;
            var expectedDataSize = (Dimensions.Area() * BitDepth) / 8;
            var expectedDataOutSize = (Dimensions.Area() * DecodedBitDepth) / 8;
            Debug.Assert(dataOut.Length >= expectedDataOutSize, "Data output buffer too small");
            var dataOutOffset = 0;

            switch (BitDepth)
            {
                // For 8 or 16-bit uncompressed, we don't need to unpack, just read directly to output buffer
                case 8:
                case 16:
                    for (int i = 0; i < offsetsCount; i++)
                    {
                        var expectedRemainingData = expectedDataSize - dataOutOffset;
                        var segmentSizeBytes = Math.Min((int)byteCounts[i], (int)expectedRemainingData);
                        try
                        {
                            contentReader.Read((long)offsets[i], dataOut.AsMemory(dataOutOffset, segmentSizeBytes));
                            dataOutOffset += segmentSizeBytes;
                        }
                        catch
                        {
                            return Error.BadImageData;
                        }
                    }
                    break;

                // 12 or 14-bit packed, read into a temporary buffer then unpack to target buffer
                case 12:
                case 14:
                    var packedDataOffset = 0;
                    for (int i = 0; i < offsetsCount; i++)
                    {
                        var expectedRemainingData = expectedDataSize - packedDataOffset;
                        var segmentSizeBytes = Math.Min((int)expectedRemainingData, (int)byteCounts[i]);
                        byte[] packedData = System.Buffers.ArrayPool<byte>.Shared.Rent(segmentSizeBytes);
                        try
                        {
                            contentReader.Read((long)offsets[i], packedData.AsMemory(0, segmentSizeBytes));
                            if (BitDepth == 12)
                                Unpack.Unpack12to16Bit(dataOut, (UIntPtr)dataOutOffset, packedData, (UIntPtr)segmentSizeBytes);
                            else
                                Unpack.Unpack14to16Bit(dataOut, (UIntPtr)dataOutOffset, packedData, (UIntPtr)segmentSizeBytes);
                            packedDataOffset += segmentSizeBytes;
                            dataOutOffset += (segmentSizeBytes * 16) / (int)BitDepth;
                        }
                        catch
                        {
                            return Error.BadImageData;
                        }
                        finally
                        {
                            System.Buffers.ArrayPool<byte>.Shared.Return(packedData);
                        }
                    }
                    break;
                default:
                    return Error.NotImplmeneted;
            }

            // Sanity check the output size
            Debug.Assert(dataOutOffset == expectedDataOutSize);
            if (dataOutOffset != expectedDataOutSize)
                return Error.BadImageData;
            return Error.None;
        }

        private Error DecodeCompressedImageData(ref TiffValueCollection<ulong> offsets, ref TiffValueCollection<ulong> byteCounts, byte[] dataOut)
        {
            using var contentReader = Tiff.CreateContentReader();

            int count = offsets.Count;
            for (int i = 0; i < count; i++)
            {
                var offset = (long)offsets[i];
                int byteCount = (int)byteCounts[i];
                byte[] compressedData = System.Buffers.ArrayPool<byte>.Shared.Rent(byteCount);

                try
                {
                    contentReader.Read(offset, compressedData.AsMemory(0, byteCount));
                    //using var fs = new FileStream(@$"C:\Test\extracted-{i}.dat", FileMode.Create, FileAccess.Write);
                    //fs.Write(data, 0, byteCount);
                }
                finally
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(compressedData);
                }

            }

            return Error.NotImplmeneted;
        }

        public Vector2i Dimensions
        {
            get
            {
                if (!CachedDimensions.HasValue)
                    CachedDimensions = new Vector2i((int)TagReader.ReadImageWidth(), (int)TagReader.ReadImageLength());
                return CachedDimensions.Value;
            }
        }

        public Maths.Rational Framerate
        {
            get
            {
                if (!CachedFramerate.HasValue)
                {
                    try
                    {
                        // CinemaDNG spec states framerate is signed rational
                        var framerate = TagReader.ReadSRationalField((TiffTag)TiffTagsCinemaDNG.FrameRate, 1).GetFirstOrDefault();
                        CachedFramerate = new Maths.Rational(framerate.Numerator, framerate.Denominator);
                    }
                    catch
                    {
                        // Handle OCTOPUSCAMERA dng bug where framerate was written out as unsigned rational
                        var framerate = TagReader.ReadRationalField((TiffTag)TiffTagsCinemaDNG.FrameRate, 1).GetFirstOrDefault();
                        CachedFramerate = new Maths.Rational((int)framerate.Numerator, (int)framerate.Denominator);
                    }
                }
                return CachedFramerate.Value;
            }
        }

        public uint BitDepth
        {
            get
            {
                if (!CachedBitDepth.HasValue)
                {
                    var bitDepth = TagReader.ReadShortField(TiffTag.BitsPerSample, 1).GetFirstOrDefault();
                    CachedBitDepth = bitDepth;
                }
                return CachedBitDepth.Value;
            }
        }

        public uint DecodedBitDepth
        {
            get
            {
                if (!CachedDecodedBitDepth.HasValue)
                {
                    switch(Compression)
                    {
                        case Compression.None:
                            CachedDecodedBitDepth = BitDepth > 8u ? 16u : 8u;
                            break;
                        case Compression.LosslessJPEG:
                            CachedDecodedBitDepth = 16u;
                            break;
                        default:
                            CachedDecodedBitDepth = BitDepth;
                            break;
                    }
                }
                return CachedDecodedBitDepth.Value;
            }
        }

        public Vector2i CFARepeatPatternDimensions
        {
            get
            {
                if (!CachedCFARepeatPatternDimensions.HasValue)
                {
                    var repeatpattern = TagReader.ReadShortField((TiffTag)TiffTagsDNG.CFARepeatPatternDim);
                    CachedCFARepeatPatternDimensions = (repeatpattern.Count == 2) ? new Vector2i(repeatpattern[0], repeatpattern[1]) : new Vector2i(0, 0);
                }
                return CachedCFARepeatPatternDimensions.Value;
            }
        }

        public CFAPattern CFAPattern
        {
            get
            {
                if (!CachedCFAPattern.HasValue)
                {
                    // Read CFA tag
                    var pattern = TagReader.ReadShortField((TiffTag)TiffTagsDNG.CFAPattern);
                    switch (pattern.Count)
                    {
                        case 0:
                            CachedCFAPattern = CFAPattern.None;
                            break;
                        case 4:
                            if (pattern.ToArray().SequenceEqual(new ushort[] { 0, 1, 1, 2 }))
                                CachedCFAPattern = CFAPattern.RGGB;
                            else if (pattern.ToArray().SequenceEqual(new ushort[] { 2, 1, 1, 0 }))
                                CachedCFAPattern = CFAPattern.BGGR;
                            else if (pattern.ToArray().SequenceEqual(new ushort[] { 1, 0, 2, 1 }))
                                CachedCFAPattern = CFAPattern.GRBG;
                            else
                                CachedCFAPattern = CFAPattern.Unknown;
                            break;
                        default:
                            CachedCFAPattern = CFAPattern.Unknown;
                            break;
                    }
                }
                return CachedCFAPattern.Value;
            }
        }

        public Compression Compression
        {
            get
            {
                if (!CachedCompression.HasValue)
                {
                    var tiffCompression = TagReader.ReadCompression();
                    CachedCompression = (tiffCompression == TiffCompression.NoCompression || tiffCompression == TiffCompression.Jpeg) ?
                        (Compression)tiffCompression : Compression.Unknown;
                }
                return CachedCompression.Value;
            }
        }

        public PhotometricInterpretation PhotometricInterpretation
        {
            get
            {
                if (!CachedPhotometricInterpretation.HasValue)
                {
                    var photometricInterpretation = (PhotometricInterpretation)TagReader.ReadPhotometricInterpretation();
                    CachedPhotometricInterpretation = (photometricInterpretation == PhotometricInterpretation.LinearRaw || photometricInterpretation == PhotometricInterpretation.ColorFilterArray) ?
                        photometricInterpretation : PhotometricInterpretation.Unknown;
                }
                return CachedPhotometricInterpretation.Value;
            }
        }

        public bool Monochrome { get { return PhotometricInterpretation == PhotometricInterpretation.LinearRaw; } }

        public bool IsTiled
        {
            get
            {
                if (!CachedIsTiled.HasValue)
                    CachedIsTiled = Ifd.Contains(TiffTag.TileOffsets);
                return CachedIsTiled.Value;
            }
        }

        public Vector2i TileDimensions
        {
            get
            {
                Debug.Assert(IsTiled);
                if (!CachedTileDimensions.HasValue)
                    CachedTileDimensions = new Vector2i((int)TagReader.ReadTileWidth().GetValueOrDefault(0), (int)TagReader.ReadTileLength().GetValueOrDefault(0));
                return CachedTileDimensions.Value;
            }
        }

        public uint TileCount
        {
            get
            {
                Debug.Assert(IsTiled);
                if ( !CachedTileCount.HasValue)
                    CachedTileCount = (uint)TagReader.ReadTileOffsets().Count;
                return CachedTileCount.Value;
            }
        }

        public uint StripCount
        {
            get
            {
                Debug.Assert(!IsTiled);
                if ( !CachedStripCount.HasValue)
                    CachedStripCount = (uint)TagReader.ReadStripOffsets().Count;
                return CachedStripCount.Value;
            }
        }

        public ushort[] LinearizationTable
        {
            get
            {
                if ( CachedLinearizationTable == null )
                {
                    if (Ifd.Contains((TiffTag)TiffTagsDNG.LinearizationTable))
                        CachedLinearizationTable = TagReader.ReadShortField((TiffTag)TiffTagsDNG.LinearizationTable).ToArray();
                    else
                        CachedLinearizationTable = new ushort[0];
                }
                return CachedLinearizationTable;
            }
        }

        public ushort BlackLevel
        {
            get
            {
                if (!CachedBlackLevel.HasValue)
                    CachedBlackLevel = (ushort)(Ifd.Contains((TiffTag)TiffTagsDNG.BlackLevel) ? TagReader.ReadLongField((TiffTag)TiffTagsDNG.BlackLevel, 1).First() : 0);
                return CachedBlackLevel.Value;
            }
        }

        public ushort WhiteLevel
        {
            get
            {
                if (!CachedWhiteLevel.HasValue)
                    CachedWhiteLevel = (ushort)(Ifd.Contains((TiffTag)TiffTagsDNG.WhiteLevel) ? TagReader.ReadLongField((TiffTag)TiffTagsDNG.WhiteLevel, 1).First() : (uint)((1 << (int)BitDepth) - 1));
                return CachedWhiteLevel.Value;
            }
        }

        public void Dispose()
        {
            Ifd = null;
            Tiff?.Dispose();
            FieldReader?.Dispose();
            Tiff = null;
            FieldReader = null;
        }
    }
}

