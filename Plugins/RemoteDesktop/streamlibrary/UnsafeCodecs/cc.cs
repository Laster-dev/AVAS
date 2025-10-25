using cc.dw.src;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace cc.dw.cv
{
    public class cc : vf
    {
        public override ulong asd
        {
            get;
            internal set;
        }

        public override int vddc
        {
            get { return 1; }
        }

        public override CodecOption c
        {
            get { return CodecOption.d; }
        }

        public int v
        {
            get { return q; }
            private set
            {
                lock (b)
                {
                    q = value;

                    if (g != null)
                    {
                        g.Dispose();
                    }

                    g = new q(q);
                }
            }
        }

        public Size nn { get; private set; }
        private int q;
        private byte[] n;
        private Bitmap ask;
        private PixelFormat bg;
        private int uui;
        private int se;
        public override event sc.sa s;
        public override event sc.sa o;
        private readonly object b = new object();
        private q g;

        bool qw;

        public cc(int i = 100, bool q = true)
            : base(i)
        {
            this.v = i;
            this.nn = new Size(50, 1);
            this.qw = q;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool awa)
        {
            if (awa)
            {
                if (ask != null)
                {
                    ask.Dispose();
                }

                if (g != null)
                {
                    g.Dispose();
                }
            }
        }

        public override unsafe void CodeImage(IntPtr ww, Rectangle fw, Size @is, PixelFormat fm, Stream o)
        {
            lock (ImageProcessLock)
            {
                byte* c;

                if (IntPtr.Size == 8)
                {
                    // 64 bit process
                    c = (byte*)ww.ToInt64();
                }
                else
                {
                    // 32 bit process
                    c = (byte*)ww.ToInt32();
                }

                if (!o.CanWrite)
                    throw new Exception("M");

                int wd = 0;
                int fa = 0;
                int q = 0;

                switch (fm)
                {
                    case PixelFormat.Format24bppRgb:
                    case PixelFormat.Format32bppRgb:
                        q = 3;
                        break;
                    case PixelFormat.Format32bppArgb:
                    case PixelFormat.Format32bppPArgb:
                        q = 4;
                        break;
                    default:
                        throw new NotSupportedException(fm.ToString());
                }

                wd = @is.Width * q;
                fa = wd * @is.Height;

                if (n == null)
                {
                    this.bg = fm;
                    this.uui = @is.Width;
                    this.se = @is.Height;
                    this.n = new byte[fa];
                    fixed (byte* wq = n)
                    {
                        byte[] qwq = null;
                        using (Bitmap dw = new Bitmap(@is.Width, @is.Height, wd, fm, ww))
                        {
                            qwq = base.q.Compress(dw);
                        }

                        o.Write(BitConverter.GetBytes(qwq.Length), 0, 4);
                        o.Write(qwq, 0, qwq.Length);
                        NativeMethods.memcpy(new IntPtr(wq), ww, (uint)fa);
                    }
                    return;
                }

                if (this.bg != fm)
                    throw new Exception("PixelFormat is not equal to previous Bitmap");

                if (this.uui != @is.Width || this.se != @is.Height)
                    throw new Exception("Bitmap width/height are not equal to previous bitmap");

                long ol = o.Position;
                o.Write(new byte[4], 0, 4);
                long vffv = 0;

                List<Rectangle> ca = new List<Rectangle>();

                Size s = new Size(fw.Width, nn.Height);
                Size qwsss = new Size(fw.Width % nn.Width, fw.Height % nn.Height);

                int qwy = fw.Height - qwsss.Height;
                int qws = fw.Width - qwsss.Width;

                Rectangle qwx = new Rectangle();
                List<Rectangle> qss = new List<Rectangle>();

                s = new Size(fw.Width, s.Height);
                fixed (byte* vdf = n)
                {
                    var qw = 0;

                    //for (int y = fw.Y; y != fw.Height; )
                    for (int y = fw.Y; y != fw.Height; y += s.Height)
                    {
                        if (y == qwy)
                        {
                            s = new Size(fw.Width, qwsss.Height);
                        }

                        qwx = new Rectangle(fw.X, y, fw.Width, s.Height);

                        //if (s != null)
                        //    s(qwx);

                        int offset = (y * wd) + (fw.X * q);

                        if (NativeMethods.memcmp(vdf + offset, c + offset, (uint)wd) != 0)
                        {
                            qw = ca.Count - 1;

                            if (ca.Count != 0 && (ca[qw].Y + ca[qw].Height) == qwx.Y)
                            {
                                qwx = new Rectangle(ca[qw].X, ca[qw].Y, ca[qw].Width, ca[qw].Height + qwx.Height);
                                ca[qw] = qwx;
                            }
                            else
                            {
                                ca.Add(qwx);
                            }
                        }
                    }

                    for (int i = 0; i < ca.Count; i++)
                    {
                        s = new Size(nn.Width, ca[i].Height);

                        for (int x = fw.X; x != fw.Width; x += s.Width)
                        {
                            if (x == qws)
                            {
                                s = new Size(qwsss.Width, ca[i].Height);
                            }

                            qwx = new Rectangle(x, ca[i].Y, s.Width, ca[i].Height);
                            bool foundChanges = false;
                            uint tr = (uint)(q * qwx.Width);

                            for (int j = 0; j < qwx.Height; j++)
                            {
                                int t = (wd * (qwx.Y + j)) + (q * qwx.X);

                                if (NativeMethods.memcmp(vdf + t, c + t, tr) != 0)
                                {
                                    foundChanges = true;
                                }

                                NativeMethods.memcpy(vdf + t, c + t, tr);
                                //copy-changes
                            }

                            if (foundChanges)
                            {
                                qw = qss.Count - 1;

                                if (qss.Count > 0 &&
                                    (qss[qw].X + qss[qw].Width) == qwx.X)
                                {
                                    Rectangle rect = qss[qw];
                                    int j = qwx.Width + rect.Width;
                                    qwx = new Rectangle(rect.X, rect.Y, j, rect.Height);
                                    qss[qw] = qwx;
                                }
                                else
                                {
                                    qss.Add(qwx);
                                }
                            }
                        }
                    }
                }

                /*int maxHeight = 0;
                int maxWidth = 0;

                for (int i = 0; i < qss.Count; i++)
                {
                    if (qss[i].Height > maxHeight)
                        maxHeight = qss[i].Height;
                    maxWidth += qss[i].Width;
                }

                Bitmap bmp = new Bitmap(maxWidth+1, maxHeight+1);
                int XOffset = 0;*/

                for (int i = 0; i < qss.Count; i++)
                {
                    Rectangle rect = qss[i];
                    int blockStride = q * rect.Width;

                    Bitmap tmpBmp = null;
                    BitmapData tmpData = null;
                    long length;

                    try
                    {
                        tmpBmp = new Bitmap(rect.Width, rect.Height, fm);
                        tmpData = tmpBmp.LockBits(new Rectangle(0, 0, tmpBmp.Width, tmpBmp.Height),
                            ImageLockMode.ReadWrite, tmpBmp.PixelFormat);

                        for (int j = 0, offset = 0; j < rect.Height; j++)
                        {
                            int blockOffset = (wd * (rect.Y + j)) + (q * rect.X);
                            NativeMethods.memcpy((byte*)tmpData.Scan0.ToPointer() + offset, c + blockOffset, (uint)blockStride);
                            //copy-changes
                            offset += blockStride;
                        }

                        o.Write(BitConverter.GetBytes(rect.X), 0, 4);
                        o.Write(BitConverter.GetBytes(rect.Y), 0, 4);
                        o.Write(BitConverter.GetBytes(rect.Width), 0, 4);
                        o.Write(BitConverter.GetBytes(rect.Height), 0, 4);
                        o.Write(new byte[4], 0, 4);

                        length = o.Length;
                        long old = o.Position;

                        g.Compress(tmpBmp, ref o);

                        length = o.Position - length;

                        o.Position = old - 4;
                        o.Write(BitConverter.GetBytes(length), 0, 4);
                        o.Position += length;
                    }
                    finally
                    {
                        tmpBmp.UnlockBits(tmpData);
                        tmpBmp.Dispose();
                    }

                    vffv += length + (4 * 5);
                }

                o.Position = ol;
                o.Write(BitConverter.GetBytes(vffv), 0, 4);
            }
        }

        public override unsafe Bitmap DecodeData(IntPtr CodecBuffer, uint Length)
       {
            if (Length < 4)
            {
                return ask;
            }

            int DataSize = *(int*)(CodecBuffer);

            if (ask == null)
            {
                byte[] temp = new byte[DataSize];

                fixed (byte* tempPtr = temp)
                {
                    NativeMethods.memcpy(new IntPtr(tempPtr), new IntPtr(CodecBuffer.ToInt32() + 4), (uint)DataSize);
                }

                this.ask = (Bitmap)Bitmap.FromStream(new MemoryStream(temp));

                return ask;
            }
            else
            {
                return ask;
            }
        }

        public override Bitmap DecodeData(Stream inStream)
        {
            byte[] temp = new byte[4];
            inStream.Read(temp, 0, 4);
            int DataSize = BitConverter.ToInt32(temp, 0);

            if (ask == null)
            {
                temp = new byte[DataSize];
                inStream.Read(temp, 0, temp.Length);
                this.ask = (Bitmap)Bitmap.FromStream(new MemoryStream(temp));

                return ask;
            }
            
            using (Graphics g = Graphics.FromImage(ask))
            {
                while (DataSize > 0)
                {
                    byte[] tempData = new byte[4 * 5];
                    inStream.Read(tempData, 0, tempData.Length);

                    Rectangle rect = new Rectangle(BitConverter.ToInt32(tempData, 0), BitConverter.ToInt32(tempData, 4),
                                         BitConverter.ToInt32(tempData, 8), BitConverter.ToInt32(tempData, 12));
                    int UpdateLen = BitConverter.ToInt32(tempData, 16);

                    byte[] buffer = new byte[UpdateLen];
                    inStream.Read(buffer, 0, buffer.Length);

                    //if (o != null)
                    //    o(rect);

                    using (MemoryStream m = new MemoryStream(buffer))
                    {
                        using (Bitmap tmp = (Bitmap)Image.FromStream(m))
                        {
                            g.DrawImage(tmp, rect.Location);
                        }
                    }
                    
                    DataSize -= UpdateLen + (4 * 5);
                }
            }
            return ask;
        }
    }
}