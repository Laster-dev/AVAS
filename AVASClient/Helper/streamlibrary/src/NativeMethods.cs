using AVASClient.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace StreamLibrary.src
{
    public class NativeMethods
    {

        private static int ManagedMemcmp(IntPtr ptr1, IntPtr ptr2, UIntPtr count)
        {
            unsafe
            {
                return ManagedMemcmp((byte*)ptr1.ToPointer(), (byte*)ptr2.ToPointer(), (uint)count);
            }
        }

        private static IntPtr ManagedMemcpy(IntPtr dest, IntPtr src, UIntPtr count)
        {
            unsafe
            {
                ManagedMemcpy(dest.ToPointer(), src.ToPointer(), (uint)count);
                return dest;
            }
        }
        public static unsafe int memcmp(byte* ptr1, byte* ptr2, uint count)
        {
            if (ptr1 == null || ptr2 == null)
                throw new ArgumentNullException(ptr1 == null ? nameof(ptr1) : nameof(ptr2));

            // Use platform-specific implementation
            return ManagedMemcmp(new IntPtr(ptr1), new IntPtr(ptr2), new UIntPtr(count));
        }

        // Managed implementation for fallback
        private static unsafe int ManagedMemcmp(byte* ptr1, byte* ptr2, uint count)
        {
            int length = checked((int)count);
            int index = 0;

            // Vectorized compare for large chunks
            if (Vector.IsHardwareAccelerated && length >= Vector<byte>.Count)
            {
                int vectorCount = Vector<byte>.Count;
                int lastVectorIndex = length - vectorCount;
                while (index <= lastVectorIndex)
                {
                    var v1 = new Vector<byte>(new ReadOnlySpan<byte>(ptr1 + index, vectorCount));
                    var v2 = new Vector<byte>(new ReadOnlySpan<byte>(ptr2 + index, vectorCount));
                    var diff = Vector.Xor(v1, v2);
                    if (!Vector.EqualsAll(diff, Vector<byte>.Zero))
                    {
                        // Find first differing byte within this vector
                        for (int k = 0; k < vectorCount; k++)
                        {
                            int a = ptr1[index + k];
                            int b = ptr2[index + k];
                            int d = a - b;
                            if (d != 0) return d;
                        }
                    }
                    index += vectorCount;
                }
            }

            // Process remaining bytes
            for (; index < length; index++)
            {
                int a = ptr1[index];
                int b = ptr2[index];
                int d = a - b;
                if (d != 0) return d;
            }

            return 0;
        }

        public static int memcmp(IntPtr ptr1, IntPtr ptr2, uint count)
        {
            // Use platform-specific implementation
            return ManagedMemcmp(ptr1, ptr2, new UIntPtr(count));
        }

        public static int memcpy(IntPtr dst, IntPtr src, uint count)
        {
            // Use platform-specific implementation
            ManagedMemcpy(dst, src, new UIntPtr(count));
            return 0;
        }

        public static unsafe int memcpy(void* dst, void* src, uint count)
        {
            if (dst == null || src == null)
                throw new ArgumentNullException(dst == null ? nameof(dst) : nameof(src));

            // Use platform-specific implementation
            ManagedMemcpy(new IntPtr(dst), new IntPtr(src), new UIntPtr(count));
            return 0;
        }

        // Managed implementation for fallback
        private static unsafe void ManagedMemcpy(void* dst, void* src, uint count)
        {
            Buffer.MemoryCopy(src, dst, count, count);
        }
    }
}