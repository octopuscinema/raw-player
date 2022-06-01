using System;
using System.IO;
using TiffLibrary;

namespace Octopus.Player.Core.IO.DNG
{
    public class Reader : IDisposable
    {
        public string FilePath { get; private set; }
        
        private TiffFileReader Tiff { get; set; }
        private TiffFieldReader FieldReader { get; set; }
        
        public Reader(string filePath)
        {
            // Open TIFF file
            Tiff = TiffFileReader.Open(filePath);
            if (Tiff != null)
                FilePath = filePath;


//            FieldReader = Tiff.CreateFieldReader();
            //            var ifd = Tiff.ReadImageFileDirectory();
    //        var tagReader = new TiffTagReader(fieldReader, ifd);

            
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

        public void Dispose()
        {
            Tiff?.Dispose();
            FieldReader?.Dispose();
            Tiff = null;
            FieldReader = null;
        }

        public void Sandbox()
        {
            /*
            const int threadCount = 10;
            var list = new List<int>(threadCount);
            for (var i = 0; i < threadCount; i++) list.Add(i);

            using (var countdownEvent = new CountdownEvent(threadCount))
            {
                for (var i = 0; i < threadCount; i++)
                    ThreadPool.QueueUserWorkItem(
                        x =>
                        {
                            Console.WriteLine(x);
                            countdownEvent.Signal();
                        }, list[i]);

                countdownEvent.Wait();
            }
            Console.WriteLine("done");
            */
        }
    }
}

