using cc.dw.src;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace cc.dw
{
    public abstract class sc
    {
        public delegate void VideoCodeProgress(Stream stream, Rectangle[] MotionChanges);
        public delegate void VideoDecodeProgress(Bitmap bitmap);
        public delegate void sa(Rectangle ScanArea);

        public abstract event VideoCodeProgress onVideoStreamCoding;
        public abstract event VideoDecodeProgress onVideoStreamDecoding;
        public abstract event sa onCodeDebugScan;
        public abstract event sa onDecodeDebugScan;
        protected q jpgCompression;
        public abstract ulong CachedSize { get; internal set; }
        public int ImageQuality { get; set; }

        public sc(int ImageQuality = 100)
        {
            this.jpgCompression = new q(ImageQuality);
            this.ImageQuality = ImageQuality;
        }

        public abstract int BufferCount { get; }
        public abstract CodecOption CodecOptions { get; }
        public abstract void CodeImage(Bitmap bitmap, Stream outStream);
        public abstract Bitmap DecodeData(Stream inStream);
    }
}