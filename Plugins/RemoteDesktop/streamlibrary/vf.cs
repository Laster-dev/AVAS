using cc.dw.src;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace cc.dw
{
    public abstract class vf
    {
        protected q q;
        protected LzwCompression lzwCompression;
        public abstract ulong asd { get; internal set; }
        protected object ImageProcessLock { get; private set; }

        private int _imageQuality;
        public int ImageQuality
        {
            get { return _imageQuality; }
            set
            {
                _imageQuality = value;
                q = new q(value);
                lzwCompression = new LzwCompression(value);
            }
        }


        public abstract event sc.sa s;
        public abstract event sc.sa o;

        public vf(int ImageQuality = 100)
        {
            this.ImageQuality = ImageQuality;
            this.ImageProcessLock = new object();
        }

        public abstract int vddc { get; }
        public abstract CodecOption c { get; }
        public abstract unsafe void CodeImage(IntPtr Scan0, Rectangle ScanArea, Size ImageSize, PixelFormat Format, Stream outStream);
        public abstract unsafe Bitmap DecodeData(Stream inStream);
        public abstract unsafe Bitmap DecodeData(IntPtr CodecBuffer, uint Length);
    }
}