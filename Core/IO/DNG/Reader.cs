using OpenTK.Mathematics;
using System;
using System.IO;
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
        private Maths.Rational? CachedFramerate { get; set; }
        private uint? CachedBitDepth { get; set; }

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
                    var framerate = TagReader.ReadSRationalField((TiffTag)TiffTagsCinemaDNG.FrameRate, 1).GetFirstOrDefault();
                    CachedFramerate = new Maths.Rational(framerate.Numerator, framerate.Denominator);
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

