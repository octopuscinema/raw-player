using Octopus.Player.Core.Decoders;
using Octopus.Player.Core.Maths;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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
        private TiffImageFileDirectory ImageDataIfd { get; set; }
        private TiffTagReader TagReader { get; set; }
        private TiffTagReader ImageDataTagReader { get; set; }
        private Vector2i? CachedDimensions { get; set; }
        private Vector2i? CachedPaddedDimensions { get; set; }
        private bool? CachedContainsFramerate { get; set; }
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
        private Matrix3? CachedColorMatrix1 { get; set; }
        private Matrix3? CachedColorMatrix2 { get; set; }
        private Matrix3? CachedForwardMatrix1 { get; set; }
        private Matrix3? CachedForwardMatrix2 { get; set; }
        private Maths.Color.Illuminant? CachedCalibrationIlluminant1 { get; set; }
        private Maths.Color.Illuminant? CachedCalibrationIlluminant2 { get; set; }
        private Vector2? CachedAsShotWhiteXY { get; set; }
        private Vector3? CachedAsShotNeutral { get; set; }
        private bool? CachedHasAsShotWhiteXY { get; set; }
        private bool? CachedHasAsShotNeutral { get; set; }
        private bool? CachedIsDualIlluminant { get; set; }
        private bool? CachedHasForwardMatrix { get; set; }
        private float? CachedBaselineExposure { get; set; }
        private string CachedUniqueCameraModel { get; set; }
        private SMPTETimeCode? CachedTimeCode { get; set; }
        private bool? CachedContainsTimeCode { get; set; }
        private bool? CachedContainsDefaultScale { get; set; }
        private Vector2? CachedDefaultScale { get; set; }

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
            ImageDataIfd = Ifd;
            TagReader = new TiffTagReader(FieldReader, Ifd);
            ImageDataTagReader = TagReader;

            // If this is not the main image, we need to check the subifds
            var subFileType = TagReader.ReadNewSubfileType();
            if (subFileType != TiffNewSubfileType.None )
            {
                var subIfds = TagReader.ReadSubIFDs();
                foreach(var subIfd in subIfds)
                {
                    var ifd = Tiff.ReadImageFileDirectory(subIfd);
                    if ( ifd.FindEntry(TiffTag.NewSubfileType).ValueOffset == (long)TiffNewSubfileType.None )
                    {
                        ImageDataIfd = ifd;
                        ImageDataTagReader = new TiffTagReader(FieldReader, ImageDataIfd);
                    }
                }
            }
        }

        public Error DecodeImageData(byte[] dataOut)
        {
            CachedIsTiled = false;
            Valid = false;

            // Get offsets to the strip/tile data
            TiffValueCollection<ulong> offsets, byteCounts;
            if (ImageDataIfd.Contains(TiffTag.TileOffsets))
            {
                CachedIsTiled = true;
                offsets = ImageDataTagReader.ReadTileOffsets();
                byteCounts = ImageDataTagReader.ReadTileByteCounts();
                CachedTileCount = (uint)offsets.Count;
            }
            else if (ImageDataIfd.Contains(TiffTag.StripOffsets))
            {
                offsets = ImageDataTagReader.ReadStripOffsets();
                byteCounts = ImageDataTagReader.ReadStripByteCounts();
                CachedStripCount = (uint)offsets.Count;
            }
            else
                return Error.BadImageData;
            if (offsets.Count != byteCounts.Count)
                return Error.BadImageData;
            Valid = true;

            switch (Compression)
            {
                case Compression.LosslessJPEG:
                    return DecodeCompressedImageDataMulticore(ref offsets, ref byteCounts, dataOut);
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
            var expectedDataSize = (PaddedDimensions.Area() * BitDepth) / 8;
            var expectedDataOutSize = (PaddedDimensions.Area() * DecodedBitDepth) / 8;
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
                    var inputOffset = BitDepth == 12 ? (int)Unpack.Unpack12InputOffsetBytes() : 0;
                    for (int i = 0; i < offsetsCount; i++)
                    {
                        var expectedRemainingData = expectedDataSize - packedDataOffset;
                        var segmentSizeBytes = Math.Min((int)expectedRemainingData, (int)byteCounts[i]);
                        byte[] packedData = System.Buffers.ArrayPool<byte>.Shared.Rent(segmentSizeBytes + inputOffset);
                        try
                        {
                            contentReader.Read((long)offsets[i], packedData.AsMemory(inputOffset, segmentSizeBytes));
                            if (BitDepth == 12)
                                Unpack.Unpack12to16Bit(dataOut, (UIntPtr)dataOutOffset, packedData, (uint)inputOffset, (UIntPtr)segmentSizeBytes);
                            else
                                Unpack.Unpack14to16Bit(dataOut, (UIntPtr)dataOutOffset, packedData, (UIntPtr)segmentSizeBytes);
                            packedDataOffset += segmentSizeBytes;
                            dataOutOffset += (segmentSizeBytes * (int)DecodedBitDepth) / (int)BitDepth;
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

        private Error DecodeCompressedImageDataMulticore(ref TiffValueCollection<ulong> offsets, ref TiffValueCollection<ulong> byteCounts, byte[] dataOut)
        {
            // Use single threaded version if there is only one segment
            if (offsets.Count <= 1)
                return DecodeCompressedImageData(ref offsets, ref byteCounts, dataOut);

            using var contentReader = Tiff.CreateContentReader();
            var expectedDataOutSize = (PaddedDimensions.Area() * DecodedBitDepth) / 8;
            Debug.Assert(dataOut.Length >= expectedDataOutSize, "Data output buffer too small");

            // Reserve temporary memory for all segments to run concurrently
            int totalCompressedDataSize = 0;
            foreach (var count in byteCounts)
                totalCompressedDataSize += (int)count;
            byte[] compressedData = System.Buffers.ArrayPool<byte>.Shared.Rent(totalCompressedDataSize);

            // Read and decode each segment as a new task
            Error lastError = Error.None;
            Task[] tasks = new Task[offsets.Count];
            int taskMemoryOffset = 0;
            for (int i = 0; i < tasks.Length; i++)
            {
                var segmentIndex = i;
                var offset = (long)offsets[segmentIndex];
                int byteCount = (int)byteCounts[segmentIndex];
                var taskMemoryStart = taskMemoryOffset;
                taskMemoryOffset += byteCount;
                tasks[i] = Task.Factory.StartNew((Object obj) =>
                {
                    try
                    {
                        contentReader.Read(offset, compressedData.AsMemory(taskMemoryStart, byteCount));
                        var segmentDimensions = IsTiled ? TileDimensions : (PaddedDimensions / new Vector2i(1, (int)StripCount));
                        var dataOutOffset = ((segmentDimensions.Area() * (int)DecodedBitDepth) / 8) * segmentIndex;
                        var decodeError = LJ92.Decode(dataOut, (uint)dataOutOffset, compressedData, (uint)taskMemoryStart, (uint)byteCount, (uint)segmentDimensions.X, (uint)segmentDimensions.Y, BitDepth);
                        if (decodeError != Error.None)
                            lastError = decodeError;
                    }
                    catch
                    {
                        lastError = Error.BadImageData;
                    }
                }, segmentIndex);
            }

            // Wait for all tasks
            Task.WaitAll(tasks);
            foreach (var task in tasks)
                task.Dispose();

            // Done with temporary data
            System.Buffers.ArrayPool<byte>.Shared.Return(compressedData);
            return lastError;
        }

        private Error DecodeCompressedImageData(ref TiffValueCollection<ulong> offsets, ref TiffValueCollection<ulong> byteCounts, byte[] dataOut)
        {
            using var contentReader = Tiff.CreateContentReader();
            var offsetsCount = offsets.Count;
            var expectedDataOutSize = (PaddedDimensions.Area() * DecodedBitDepth) / 8;
            Debug.Assert(dataOut.Length >= expectedDataOutSize, "Data output buffer too small");
            int dataOutOffset = 0;

            // Reserve temporary memory large enough for largest segment
            int largestByteCount = 0;
            foreach (var count in byteCounts)
                largestByteCount = Math.Max(largestByteCount, (int)count);
            byte[] compressedData = System.Buffers.ArrayPool<byte>.Shared.Rent(largestByteCount);

            // Read and decode each segment
            try
            {
                for (int i = 0; i < offsetsCount; i++)
                {
                    var offset = (long)offsets[i];
                    int byteCount = (int)byteCounts[i];
                    contentReader.Read(offset, compressedData.AsMemory(0, byteCount));
                    var segmentDimensions = IsTiled ? TileDimensions : (PaddedDimensions / new Vector2i(1, (int)StripCount));
                    var decodeError = LJ92.Decode(dataOut, (uint)dataOutOffset, compressedData, 0, (uint)byteCount, (uint)segmentDimensions.X, (uint)segmentDimensions.Y, BitDepth);
                    dataOutOffset += (segmentDimensions.Area() * (int)DecodedBitDepth) / 8;
                    if (decodeError != Error.None)
                        return decodeError;
                }
            }
            catch
            {
                return Error.BadImageData;
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(compressedData);
            }

            // Sanity check the output size
            Debug.Assert(dataOutOffset == expectedDataOutSize);
            if (dataOutOffset != expectedDataOutSize)
                return Error.BadImageData;
            return Error.None;
        }

        public Vector2i Dimensions
        {
            get
            {
                if (!CachedDimensions.HasValue)
                    CachedDimensions = new Vector2i((int)ImageDataTagReader.ReadImageWidth(), (int)ImageDataTagReader.ReadImageLength());
                return CachedDimensions.Value;
            }
        }

        public Vector2i PaddedDimensions
        {
            get
            {
                if (!CachedPaddedDimensions.HasValue)
                {
                    var realDimensions = new Vector2i((int)ImageDataTagReader.ReadImageWidth(), (int)ImageDataTagReader.ReadImageLength());

                    // Make dimensions a multiple of the tile dimensions
                    if (IsTiled)
                    {
                        if ((realDimensions.X / TileDimensions.X) * TileDimensions.X != realDimensions.X)
                            realDimensions = new Vector2i((int)Math.Ceiling((double)realDimensions.X / (double)TileDimensions.X) * TileDimensions.X, realDimensions.Y);

                        if ((realDimensions.Y / TileDimensions.Y) * TileDimensions.Y != realDimensions.Y)
                            realDimensions = new Vector2i(realDimensions.X, (int)Math.Ceiling((double)realDimensions.Y / (double)TileDimensions.Y) * TileDimensions.Y);
                    }

                    CachedPaddedDimensions = realDimensions;
                }
                return CachedPaddedDimensions.Value;
            }
        }

        public bool ContainsFramerate
        {
            get
            {
                if (!CachedContainsFramerate.HasValue)
                    CachedContainsFramerate = Ifd.Contains((TiffTag)TiffTagCinemaDNG.FrameRate) || ImageDataIfd.Contains((TiffTag)TiffTagCinemaDNG.FrameRate);
                return CachedContainsFramerate.Value;
            }
        }

        public Rational Framerate
        {
            get
            {
                if (!CachedFramerate.HasValue)
                {
                    var tagReader = Ifd.Contains((TiffTag)TiffTagCinemaDNG.FrameRate) ? TagReader : ImageDataTagReader;
                    try
                    {
                        // CinemaDNG spec states framerate is signed rational
                        var framerate = tagReader.ReadSRationalField((TiffTag)TiffTagCinemaDNG.FrameRate, 1).GetFirstOrDefault();
                        CachedFramerate = new Rational(framerate.Numerator, framerate.Denominator);
                    }
                    catch
                    {
                        // Handle OCTOPUSCAMERA dng bug where framerate was written out as unsigned rational
                        var framerate = tagReader.ReadRationalField((TiffTag)TiffTagCinemaDNG.FrameRate, 1).GetFirstOrDefault();
                        CachedFramerate = new Rational((int)framerate.Numerator, (int)framerate.Denominator);
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
                    var bitDepth = ImageDataTagReader.ReadShortField(TiffTag.BitsPerSample, 1).GetFirstOrDefault();
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
                    switch (Compression)
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
                    var repeatpattern = ImageDataTagReader.ReadShortField((TiffTag)TiffTagDNG.CFARepeatPatternDim);
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
                    var pattern = ImageDataTagReader.ReadShortField((TiffTag)TiffTagDNG.CFAPattern);
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
                            else if (pattern.ToArray().SequenceEqual(new ushort[] { 1, 2, 0, 1 }))
                                CachedCFAPattern = CFAPattern.GBRG;
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
                    var tiffCompression = ImageDataTagReader.ReadCompression();
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
                    var photometricInterpretation = (PhotometricInterpretation)ImageDataTagReader.ReadPhotometricInterpretation();
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
                if (!CachedTileCount.HasValue)
                    CachedTileCount = (uint)TagReader.ReadTileOffsets().Count;
                return CachedTileCount.Value;
            }
        }

        public uint StripCount
        {
            get
            {
                Debug.Assert(!IsTiled);
                if (!CachedStripCount.HasValue)
                    CachedStripCount = (uint)TagReader.ReadStripOffsets().Count;
                return CachedStripCount.Value;
            }
        }

        public ushort[] LinearizationTable
        {
            get
            {
                if (CachedLinearizationTable == null)
                {
                    if (Ifd.Contains((TiffTag)TiffTagDNG.LinearizationTable))
                        CachedLinearizationTable = TagReader.ReadShortField((TiffTag)TiffTagDNG.LinearizationTable).ToArray();
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
                {
                    try
                    {
                        CachedBlackLevel = (ushort)(ImageDataIfd.Contains((TiffTag)TiffTagDNG.BlackLevel) ? ImageDataTagReader.ReadLongField((TiffTag)TiffTagDNG.BlackLevel, 1).First() : 0);
                    }
                    catch
                    {
                        var blackLevelRational = ImageDataIfd.Contains((TiffTag)TiffTagDNG.BlackLevel) ? ImageDataTagReader.ReadRationalField((TiffTag)TiffTagDNG.BlackLevel, 1).First() : new TiffRational();
                        CachedBlackLevel = blackLevelRational.Denominator == 0 ? (ushort)0 : (ushort)(blackLevelRational.Numerator / blackLevelRational.Denominator);
                    }
                }
                return CachedBlackLevel.Value;
            }
        }

        public ushort WhiteLevel
        {
            get
            {
                if (!CachedWhiteLevel.HasValue)
                    CachedWhiteLevel = (ushort)(ImageDataIfd.Contains((TiffTag)TiffTagDNG.WhiteLevel) ? ImageDataTagReader.ReadLongField((TiffTag)TiffTagDNG.WhiteLevel, 1).First() : (uint)((1 << (int)BitDepth) - 1));
                return CachedWhiteLevel.Value;
            }
        }

        public Matrix3 ColorMatrix1
        {
            get
            {
                if (!CachedColorMatrix1.HasValue)
                    CachedColorMatrix1 = ReadMatrix3x3(TiffTagDNG.ColorMatrix1);
                return CachedColorMatrix1.Value;
            }
        }

        public Matrix3 ColorMatrix2
        {
            get
            {
                if (!CachedColorMatrix2.HasValue)
                    CachedColorMatrix2 = ReadMatrix3x3(TiffTagDNG.ColorMatrix2);
                return CachedColorMatrix2.Value;
            }
        }

        public Matrix3 ForwardMatrix1
        {
            get
            {
                if (!CachedForwardMatrix1.HasValue)
                    CachedForwardMatrix1 = ReadMatrix3x3(TiffTagDNG.ForwardMatrix1);
                return CachedForwardMatrix1.Value;
            }
        }

        public Matrix3 ForwardMatrix2
        {
            get
            {
                if (!CachedForwardMatrix2.HasValue)
                    CachedForwardMatrix2 = ReadMatrix3x3(TiffTagDNG.ForwardMatrix2);
                return CachedForwardMatrix2.Value;
            }
        }

        public Maths.Color.Illuminant CalibrationIlluminant1
        {
            get
            {
                if (!CachedCalibrationIlluminant1.HasValue)
                    CachedCalibrationIlluminant1 = (Maths.Color.Illuminant)TagReader.ReadShortField((TiffTag)TiffTagDNG.CalibrationIlluminant1, 1).First();
                return CachedCalibrationIlluminant1.Value;
            }
        }

        public Maths.Color.Illuminant CalibrationIlluminant2
        {
            get
            {
                if (!CachedCalibrationIlluminant2.HasValue)
                    CachedCalibrationIlluminant2 = (Maths.Color.Illuminant)TagReader.ReadShortField((TiffTag)TiffTagDNG.CalibrationIlluminant2, 1).First();
                return CachedCalibrationIlluminant2.Value;
            }
        }

        public Vector2 AsShotWhiteXY
        {
            get
            {
                if (!CachedAsShotWhiteXY.HasValue)
                {
                    var asShotWhiteXY = TagReader.ReadRationalField((TiffTag)TiffTagDNG.AsShotWhiteXY, 2);
                    CachedAsShotWhiteXY = new Vector2(asShotWhiteXY[0].ToSingle(), asShotWhiteXY[1].ToSingle());
                }
                return CachedAsShotWhiteXY.Value;
            }
        }

        public Vector3 AsShotNeutral
        {
            get
            {
                if (!CachedAsShotNeutral.HasValue)
                {
                    try
                    {
                        var asShotNeutral = TagReader.ReadRationalField((TiffTag)TiffTagDNG.AsShotNeutral, 3);
                        CachedAsShotNeutral = new Vector3(asShotNeutral[0].ToSingle(), asShotNeutral[1].ToSingle(), asShotNeutral[2].ToSingle());
                    }
                    catch
                    {
                        var asShotNeutral = TagReader.ReadShortField((TiffTag)TiffTagDNG.AsShotNeutral, 3);
                        CachedAsShotNeutral = new Vector3(asShotNeutral[0], asShotNeutral[1], asShotNeutral[2]);
                    }
                }
                return CachedAsShotNeutral.Value;
            }
        }

        public bool HasAsShotWhiteXY
        {
            get
            {
                if (!CachedHasAsShotWhiteXY.HasValue)
                    CachedHasAsShotWhiteXY = Ifd.Contains((TiffTag)TiffTagDNG.AsShotWhiteXY);
                return CachedHasAsShotWhiteXY.Value;
            }
        }

        public bool HasAsShotNeutral
        {
            get
            {
                if (!CachedHasAsShotNeutral.HasValue)
                    CachedHasAsShotNeutral = Ifd.Contains((TiffTag)TiffTagDNG.AsShotNeutral);
                return CachedHasAsShotNeutral.Value;
            }
        }

        public bool IsDualIlluminant
        {
            get
            {
                if (!CachedIsDualIlluminant.HasValue)
                    CachedIsDualIlluminant = Ifd.Contains((TiffTag)TiffTagDNG.ColorMatrix2);
                return CachedIsDualIlluminant.Value;
            }
        }

        public bool HasForwardMatrix
        {
            get
            {
                if (!CachedHasForwardMatrix.HasValue)
                    CachedHasForwardMatrix = Ifd.Contains((TiffTag)TiffTagDNG.ForwardMatrix1);
                return CachedHasForwardMatrix.Value;
            }
        }

        public float BaselineExposure
        {
            get
            {
                if (!CachedBaselineExposure.HasValue)
                    CachedBaselineExposure = Ifd.Contains((TiffTag)TiffTagDNG.BaselineExposure) ? (float)TagReader.ReadSRationalField((TiffTag)TiffTagDNG.BaselineExposure, 1).First().ToSingle() : 0.0f; 
                return CachedBaselineExposure.Value;
            }
        }

        public string UniqueCameraModel
        {
            get
            {
                if (CachedUniqueCameraModel == null)
                    CachedUniqueCameraModel = Ifd.Contains((TiffTag)TiffTagDNG.UniqueCameraModel) ? TagReader.ReadASCIIFieldFirstString((TiffTag)TiffTagDNG.UniqueCameraModel) : "";
                return CachedUniqueCameraModel;
            }
        }

        public SMPTETimeCode TimeCode
        {
            get
            {
                if(!CachedTimeCode.HasValue)
                {
                    var timeCodes = TagReader.ReadByteField((TiffTag)TiffTagCinemaDNG.TimeCodes, 8).ToArray<byte>();
                    GCHandle handle = GCHandle.Alloc(timeCodes, GCHandleType.Pinned);
                    CachedTimeCode = (SMPTETimeCode)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(SMPTETimeCode));
                    handle.Free();
                }

                return CachedTimeCode.Value;
            }
        }

        public bool ContainsTimeCode
        {
            get
            {
                if (!CachedContainsTimeCode.HasValue)
                    CachedContainsTimeCode = Ifd.Contains((TiffTag)TiffTagCinemaDNG.TimeCodes);
                return CachedContainsTimeCode.Value;
            }
        }

        public Vector2 DefaultScale
        {
            get
            {
                if(!CachedDefaultScale.HasValue)
                {
                    var defaultScale = TagReader.ReadRationalField((TiffTag)TiffTagDNG.DefaultScale, 2);
                    CachedDefaultScale = new Vector2(defaultScale[0].ToSingle(), defaultScale[1].ToSingle());
                }
                return CachedDefaultScale.Value;
            }
        }

        public bool ContainsDefaultScale
        {
            get
            {
                if (!CachedContainsDefaultScale.HasValue)
                    CachedContainsDefaultScale = Ifd.Contains((TiffTag)TiffTagDNG.DefaultScale);
                return CachedContainsDefaultScale.Value;
            }
        }

        private Matrix3 ReadMatrix3x3(TiffTagDNG tag)
        {
            var elements = TagReader.ReadSRationalField((TiffTag)tag, 9);
            return new Matrix3(new Vector3((float)elements[0].ToSingle(), (float)elements[1].ToSingle(), (float)elements[2].ToSingle()), 
                new Vector3((float)elements[3].ToSingle(), (float)elements[4].ToSingle(), (float)elements[5].ToSingle()),
                new Vector3((float)elements[6].ToSingle(), (float)elements[7].ToSingle(), (float)elements[8].ToSingle()));
        }

        public void Dispose()
        {
            Ifd = null;
            ImageDataIfd = null;
            Tiff?.Dispose();
            FieldReader?.Dispose();
            Tiff = null;
            FieldReader = null;
        }
    }
}

