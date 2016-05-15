using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public class FileCacheDatabaseWriteStream : Stream
    {
        public string HashKey { get; set; }
        public string Filename { get; set; }
        public FileCacheDatabase Cache { get; set; }

        public const int InitialStreamSize = 1024 * 1024;

        MemoryStream mem = new MemoryStream();

        protected override void Dispose(bool disposing)
        {
            Close();
            base.Dispose(disposing);
            if (mem != null) mem.Dispose();
            mem = null;
        }

        public override void Close()
        {
            if (Cache != null)
            {
                Cache.ReplaceBinaryFileContent(HashKey, Filename, this);
            }       
        }

        public override bool CanRead
        {
            get { return mem.CanRead; }
        }

        public override bool CanSeek
        {
            get { return mem.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return mem.CanWrite; }
        }

        public override void Flush()
        {
            mem.Flush();
        }

        public override long Length
        {
            get { return mem.Length; }
        }

        public override long Position
        {
            get
            {
                return mem.Position;
            }
            set
            {
                mem.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return mem.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return mem.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            mem.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            mem.Write(buffer, offset, count);
        }
    }
}
