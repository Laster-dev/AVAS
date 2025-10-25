using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace MPLib.MP
{
    public unsafe class MsgPackEnum : IEnumerator
    {
        private readonly MsgPack[] _children;
        private int _position = -1;
        private readonly int _count;

        public MsgPackEnum(MsgPack[] children, int count)
        {
            _children = children;
            _count = count;
        }

        object IEnumerator.Current => _children[_position];

        bool IEnumerator.MoveNext()
        {
            _position++;
            return _position < _count;
        }

        void IEnumerator.Reset() => _position = -1;
    }

    public unsafe class MsgPackArray
    {
        private readonly MsgPack _owner;
        private readonly MsgPack[] _children;
        private readonly int _count;

        public MsgPackArray(MsgPack owner, MsgPack[] children, int count)
        {
            _owner = owner;
            _children = children;
            _count = count;
        }

        
        public MsgPack Add()
        {
            return _owner.AddArrayChild();
        }

        
        public MsgPack Add(string value)
        {
            var obj = _owner.AddArrayChild();
            obj.SetAsString(value);
            return obj;
        }

        
        public MsgPack Add(long value)
        {
            var obj = _owner.AddArrayChild();
            obj.SetAsInteger(value);
            return obj;
        }

        
        public MsgPack Add(double value)
        {
            var obj = _owner.AddArrayChild();
            obj.SetAsFloat(value);
            return obj;
        }

        public MsgPack this[int index]
        {
            
            get
            {
                if ((uint)index >= (uint)_count)
                    throw new IndexOutOfRangeException();
                return _children[index];
            }
        }

        public int Length => _count;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct ValueUnion
    {
        [FieldOffset(0)] public long IntValue;
        [FieldOffset(0)] public ulong UIntValue;
        [FieldOffset(0)] public double DoubleValue;
        [FieldOffset(0)] public float FloatValue;
        [FieldOffset(0)] public bool BoolValue;
        [FieldOffset(0)] public IntPtr PtrValue;
    }

    public unsafe class MsgPack : IEnumerable, IDisposable
    {
        private const int INITIAL_CAPACITY = 8;
        private const int MAX_STACK_ALLOC = 1024;

        private string _name;
        private string _lowerName;
        private ValueUnion _value;
        private MsgPackType _valueType;
        private MsgPack[] _children;
        private int _childCount;
        private int _capacity;
        private MsgPackArray _refAsArray;
        private byte[] _binaryData;
        private bool _disposed;

        public MsgPack()
        {
            _valueType = MsgPackType.Unknown;
            _capacity = INITIAL_CAPACITY;
            _children = new MsgPack[_capacity];
        }

        
        private void EnsureCapacity(int requiredCapacity)
        {
            if (requiredCapacity > _capacity)
            {
                var newCapacity = Math.Max(_capacity * 2, requiredCapacity);
                Array.Resize(ref _children, newCapacity);
                _capacity = newCapacity;
            }
        }

        
        private void SetName(string value)
        {
            _name = value;
            _lowerName = value?.ToLowerInvariant();
        }

        
        private void Clear()
        {
            if (_children != null)
            {
                for (int i = 0; i < _childCount; i++)
                {
                    _children[i]?.Dispose();
                    _children[i] = null;
                }
            }
            _childCount = 0;
            _binaryData = null;
            _refAsArray = null;
        }

        
        private MsgPack InnerAdd()
        {
            EnsureCapacity(_childCount + 1);
            var child = new MsgPack();
            _children[_childCount++] = child;
            return child;
        }

        
        private int IndexOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            
            var lowerName = name.ToLowerInvariant();
            for (int i = 0; i < _childCount; i++)
            {
                if (string.Equals(_children[i]._lowerName, lowerName, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        
        public MsgPack FindObject(string name)
        {
            var index = IndexOf(name);
            return index == -1 ? null : _children[index];
        }

        
        private MsgPack InnerAddMapChild()
        {
            if (_valueType != MsgPackType.Map)
            {
                Clear();
                _valueType = MsgPackType.Map;
            }
            return InnerAdd();
        }

        
        private MsgPack InnerAddArrayChild()
        {
            if (_valueType != MsgPackType.Array)
            {
                Clear();
                _valueType = MsgPackType.Array;
            }
            return InnerAdd();
        }

        
        public MsgPack AddArrayChild()
        {
            return InnerAddArrayChild();
        }   
     
        private void WriteMapUnsafe(byte* buffer, ref int offset, int bufferSize)
        {
            var len = _childCount;
            if (len <= 15)
            {
                if (offset >= bufferSize) throw new InvalidOperationException("Buffer overflow");
                buffer[offset++] = (byte)(0x80 + len);
            }
            else if (len <= 65535)
            {
                if (offset + 3 > bufferSize) throw new InvalidOperationException("Buffer overflow");
                buffer[offset++] = 0xDE;
                var lenBytes = (ushort)len;
                buffer[offset++] = (byte)(lenBytes >> 8);
                buffer[offset++] = (byte)(lenBytes & 0xFF);
            }
            else
            {
                if (offset + 5 > bufferSize) throw new InvalidOperationException("Buffer overflow");
                buffer[offset++] = 0xDF;
                var lenBytes = (uint)len;
                buffer[offset++] = (byte)(lenBytes >> 24);
                buffer[offset++] = (byte)((lenBytes >> 16) & 0xFF);
                buffer[offset++] = (byte)((lenBytes >> 8) & 0xFF);
                buffer[offset++] = (byte)(lenBytes & 0xFF);
            }

            for (int i = 0; i < len; i++)
            {
                WriteStringUnsafe(buffer, ref offset, bufferSize, _children[i]._name);
                _children[i].EncodeToBufferUnsafe(buffer, ref offset, bufferSize);
            }
        }

        
        private void WriteArrayUnsafe(byte* buffer, ref int offset, int bufferSize)
        {
            var len = _childCount;
            if (len <= 15)
            {
                if (offset >= bufferSize) throw new InvalidOperationException("Buffer overflow");
                buffer[offset++] = (byte)(0x90 + len);
            }
            else if (len <= 65535)
            {
                if (offset + 3 > bufferSize) throw new InvalidOperationException("Buffer overflow");
                buffer[offset++] = 0xDC;
                var lenBytes = (ushort)len;
                buffer[offset++] = (byte)(lenBytes >> 8);
                buffer[offset++] = (byte)(lenBytes & 0xFF);
            }
            else
            {
                if (offset + 5 > bufferSize) throw new InvalidOperationException("Buffer overflow");
                buffer[offset++] = 0xDD;
                var lenBytes = (uint)len;
                buffer[offset++] = (byte)(lenBytes >> 24);
                buffer[offset++] = (byte)((lenBytes >> 16) & 0xFF);
                buffer[offset++] = (byte)((lenBytes >> 8) & 0xFF);
                buffer[offset++] = (byte)(lenBytes & 0xFF);
            }

            for (int i = 0; i < len; i++)
            {
                _children[i].EncodeToBufferUnsafe(buffer, ref offset, bufferSize);
            }
        }

        
        private static void WriteStringUnsafe(byte* buffer, ref int offset, int bufferSize, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                if (offset >= bufferSize) 
                    throw new InvalidOperationException("Buffer overflow");
                buffer[offset++] = 0xA0; //空
                return;
            }

            var utf8ByteCount = Encoding.UTF8.GetByteCount(value);
            
            if (utf8ByteCount <= 31)
            {
                if (offset + 1 + utf8ByteCount > bufferSize) 
                    throw new InvalidOperationException("Buffer overflow");
                buffer[offset++] = (byte)(0xA0 + utf8ByteCount);
            }
            else if (utf8ByteCount <= 255)
            {
                if (offset + 2 + utf8ByteCount > bufferSize) 
                    throw new InvalidOperationException("Buffer overflow");
                buffer[offset++] = 0xD9;
                buffer[offset++] = (byte)utf8ByteCount;
            }
            else if (utf8ByteCount <= 65535)
            {
                if (offset + 3 + utf8ByteCount > bufferSize) 
                    throw new InvalidOperationException("Buffer overflow");
                buffer[offset++] = 0xDA;
                buffer[offset++] = (byte)(utf8ByteCount >> 8);
                buffer[offset++] = (byte)(utf8ByteCount & 0xFF);
            }
            else
            {
                if (offset + 5 + utf8ByteCount > bufferSize) throw new InvalidOperationException("Buffer overflow");
                buffer[offset++] = 0xDB;
                var len = (uint)utf8ByteCount;
                buffer[offset++] = (byte)(len >> 24);
                buffer[offset++] = (byte)((len >> 16) & 0xFF);
                buffer[offset++] = (byte)((len >> 8) & 0xFF);
                buffer[offset++] = (byte)(len & 0xFF);
            }

            fixed (char* chars = value)
            {
                Encoding.UTF8.GetBytes(chars, value.Length, buffer + offset, utf8ByteCount);
            }
            offset += utf8ByteCount;
        }

        
        private void WriteBinaryUnsafe(byte* buffer, ref int offset, int bufferSize)
        {
            var len = _binaryData?.Length ?? 0;
            if (len == 0)
            {
                if (offset + 2 > bufferSize) throw new InvalidOperationException("Buffer overflow");
                buffer[offset++] = 0xC4;
                buffer[offset++] = 0;
                return;
            }

            if (len <= 255)
            {
                if (offset + 2 + len > bufferSize) throw new InvalidOperationException("Buffer overflow");
                buffer[offset++] = 0xC4;
                buffer[offset++] = (byte)len;
            }
            else if (len <= 65535)
            {
                if (offset + 3 + len > bufferSize) throw new InvalidOperationException("Buffer overflow");
                buffer[offset++] = 0xC5;
                buffer[offset++] = (byte)(len >> 8);
                buffer[offset++] = (byte)(len & 0xFF);
            }
            else
            {
                if (offset + 5 + len > bufferSize) throw new InvalidOperationException("Buffer overflow");
                buffer[offset++] = 0xC6;
                var lenBytes = (uint)len;
                buffer[offset++] = (byte)(lenBytes >> 24);
                buffer[offset++] = (byte)((lenBytes >> 16) & 0xFF);
                buffer[offset++] = (byte)((lenBytes >> 8) & 0xFF);
                buffer[offset++] = (byte)(lenBytes & 0xFF);
            }

            fixed (byte* src = _binaryData)
            {
                Marshal.Copy(_binaryData, 0, new IntPtr(buffer + offset), len);
            }
            offset += len;
        }

        
        private void WriteIntegerUnsafe(byte* buffer, ref int offset, int bufferSize, long value)
        {
            if (value >= 0)
            {
                if (value <= 127)
                {
                    if (offset >= bufferSize) throw new InvalidOperationException("Buffer overflow");
                    buffer[offset++] = (byte)value;
                }
                else if (value <= 255)
                {
                    if (offset + 2 > bufferSize) throw new InvalidOperationException("Buffer overflow");
                    buffer[offset++] = 0xCC;
                    buffer[offset++] = (byte)value;
                }
                else if (value <= 65535)
                {
                    if (offset + 3 > bufferSize) throw new InvalidOperationException("Buffer overflow");
                    buffer[offset++] = 0xCD;
                    var val = (ushort)value;
                    buffer[offset++] = (byte)(val >> 8);
                    buffer[offset++] = (byte)(val & 0xFF);
                }
                else if (value <= 0xFFFFFFFF)
                {
                    if (offset + 5 > bufferSize) throw new InvalidOperationException("Buffer overflow");
                    buffer[offset++] = 0xCE;
                    var val = (uint)value;
                    buffer[offset++] = (byte)(val >> 24);
                    buffer[offset++] = (byte)((val >> 16) & 0xFF);
                    buffer[offset++] = (byte)((val >> 8) & 0xFF);
                    buffer[offset++] = (byte)(val & 0xFF);
                }
                else
                {
                    if (offset + 9 > bufferSize) throw new InvalidOperationException("Buffer overflow");
                    buffer[offset++] = 0xD3;
                    var val = (ulong)value;
                    buffer[offset++] = (byte)(val >> 56);
                    buffer[offset++] = (byte)((val >> 48) & 0xFF);
                    buffer[offset++] = (byte)((val >> 40) & 0xFF);
                    buffer[offset++] = (byte)((val >> 32) & 0xFF);
                    buffer[offset++] = (byte)((val >> 24) & 0xFF);
                    buffer[offset++] = (byte)((val >> 16) & 0xFF);
                    buffer[offset++] = (byte)((val >> 8) & 0xFF);
                    buffer[offset++] = (byte)(val & 0xFF);
                }
            }
            else
            {
                if (value >= -32)
                {
                    if (offset >= bufferSize) throw new InvalidOperationException("Buffer overflow");
                    buffer[offset++] = (byte)value;
                }
                else if (value >= -128)
                {
                    if (offset + 2 > bufferSize) throw new InvalidOperationException("Buffer overflow");
                    buffer[offset++] = 0xD0;
                    buffer[offset++] = (byte)value;
                }
                else if (value >= -32768)
                {
                    if (offset + 3 > bufferSize) throw new InvalidOperationException("Buffer overflow");
                    buffer[offset++] = 0xD1;
                    var val = (short)value;
                    buffer[offset++] = (byte)(val >> 8);
                    buffer[offset++] = (byte)(val & 0xFF);
                }
                else if (value >= int.MinValue)
                {
                    if (offset + 5 > bufferSize) throw new InvalidOperationException("Buffer overflow");
                    buffer[offset++] = 0xD2;
                    var val = (int)value;
                    buffer[offset++] = (byte)(val >> 24);
                    buffer[offset++] = (byte)((val >> 16) & 0xFF);
                    buffer[offset++] = (byte)((val >> 8) & 0xFF);
                    buffer[offset++] = (byte)(val & 0xFF);
                }
                else
                {
                    if (offset + 9 > bufferSize) throw new InvalidOperationException("Buffer overflow");
                    buffer[offset++] = 0xD3;
                    var val = (ulong)value;
                    buffer[offset++] = (byte)(val >> 56);
                    buffer[offset++] = (byte)((val >> 48) & 0xFF);
                    buffer[offset++] = (byte)((val >> 40) & 0xFF);
                    buffer[offset++] = (byte)((val >> 32) & 0xFF);
                    buffer[offset++] = (byte)((val >> 24) & 0xFF);
                    buffer[offset++] = (byte)((val >> 16) & 0xFF);
                    buffer[offset++] = (byte)((val >> 8) & 0xFF);
                    buffer[offset++] = (byte)(val & 0xFF);
                }
            }
        }        
        
        private void WriteFloatUnsafe(byte* buffer, ref int offset, int bufferSize, double value)
        {
            if (offset + 9 > bufferSize) throw new InvalidOperationException("Buffer overflow");
            buffer[offset++] = 0xCB;
            var bits = *(ulong*)&value;
            buffer[offset++] = (byte)(bits >> 56);
            buffer[offset++] = (byte)((bits >> 48) & 0xFF);
            buffer[offset++] = (byte)((bits >> 40) & 0xFF);
            buffer[offset++] = (byte)((bits >> 32) & 0xFF);
            buffer[offset++] = (byte)((bits >> 24) & 0xFF);
            buffer[offset++] = (byte)((bits >> 16) & 0xFF);
            buffer[offset++] = (byte)((bits >> 8) & 0xFF);
            buffer[offset++] = (byte)(bits & 0xFF);
        }

        
        private void WriteSingleUnsafe(byte* buffer, ref int offset, int bufferSize, float value)
        {
            if (offset + 5 > bufferSize) throw new InvalidOperationException("Buffer overflow");
            buffer[offset++] = 0xCA;
            var bits = *(uint*)&value;
            buffer[offset++] = (byte)(bits >> 24);
            buffer[offset++] = (byte)((bits >> 16) & 0xFF);
            buffer[offset++] = (byte)((bits >> 8) & 0xFF);
            buffer[offset++] = (byte)(bits & 0xFF);
        }

        
        public void EncodeToBufferUnsafe(byte* buffer, ref int offset, int bufferSize)
        {
            switch (_valueType)
            {
                case MsgPackType.Unknown:
                case MsgPackType.Null:
                    if (offset >= bufferSize) throw new InvalidOperationException("Buffer overflow");
                    buffer[offset++] = 0xC0;
                    break;

                case MsgPackType.String:
                    var handle = GCHandle.FromIntPtr(_value.PtrValue);
                    var str = (string)handle.Target;
                    WriteStringUnsafe(buffer, ref offset, bufferSize, str);
                    break;

                case MsgPackType.Integer:
                    WriteIntegerUnsafe(buffer, ref offset, bufferSize, _value.IntValue);
                    break;

                case MsgPackType.UInt64:
                    if (offset + 9 > bufferSize) throw new InvalidOperationException("Buffer overflow");
                    buffer[offset++] = 0xCF;
                    var val = _value.UIntValue;
                    buffer[offset++] = (byte)(val >> 56);
                    buffer[offset++] = (byte)((val >> 48) & 0xFF);
                    buffer[offset++] = (byte)((val >> 40) & 0xFF);
                    buffer[offset++] = (byte)((val >> 32) & 0xFF);
                    buffer[offset++] = (byte)((val >> 24) & 0xFF);
                    buffer[offset++] = (byte)((val >> 16) & 0xFF);
                    buffer[offset++] = (byte)((val >> 8) & 0xFF);
                    buffer[offset++] = (byte)(val & 0xFF);
                    break;

                case MsgPackType.Boolean:
                    if (offset >= bufferSize) throw new InvalidOperationException("Buffer overflow");
                    buffer[offset++] = _value.BoolValue ? (byte)0xC3 : (byte)0xC2;
                    break;

                case MsgPackType.Float:
                    WriteFloatUnsafe(buffer, ref offset, bufferSize, _value.DoubleValue);
                    break;

                case MsgPackType.Single:
                    WriteSingleUnsafe(buffer, ref offset, bufferSize, _value.FloatValue);
                    break;

                case MsgPackType.DateTime:
                    WriteIntegerUnsafe(buffer, ref offset, bufferSize, _value.IntValue);
                    break;

                case MsgPackType.Binary:
                    WriteBinaryUnsafe(buffer, ref offset, bufferSize);
                    break;

                case MsgPackType.Map:
                    WriteMapUnsafe(buffer, ref offset, bufferSize);
                    break;

                case MsgPackType.Array:
                    WriteArrayUnsafe(buffer, ref offset, bufferSize);
                    break;

                default:
                    if (offset >= bufferSize) throw new InvalidOperationException("Buffer overflow");
                    buffer[offset++] = 0xC0;
                    break;
            }
        }

        
        public void SetAsInteger(long value)
        {
            _value.IntValue = value;
            _valueType = MsgPackType.Integer;
        }

        
        public void SetAsUInt64(ulong value)
        {
            _value.UIntValue = value;
            _valueType = MsgPackType.UInt64;
        }

        
        public void SetAsFloat(double value)
        {
            _value.DoubleValue = value;
            _valueType = MsgPackType.Float;
        }

        
        public void SetAsSingle(float value)
        {
            _value.FloatValue = value;
            _valueType = MsgPackType.Single;
        }

        
        public void SetAsBoolean(bool value)
        {
            _value.BoolValue = value;
            _valueType = MsgPackType.Boolean;
        }

        
        public void SetAsString(string value)
        {
            var handle = GCHandle.Alloc(value, GCHandleType.Pinned);
            _value.PtrValue = GCHandle.ToIntPtr(handle);
            _valueType = MsgPackType.String;
        }

        
        public void SetAsBytes(byte[] value)
        {
            _binaryData = value;
            _valueType = MsgPackType.Binary;
        }

        
        public void SetAsNull()
        {
            Clear();
            _value = default;
            _valueType = MsgPackType.Null;
        }

        
        public long GetAsInteger()
        {
            switch (_valueType)
            {
                case MsgPackType.Integer:
                    return _value.IntValue;
                case MsgPackType.UInt64:
                    return (long)_value.UIntValue;
                case MsgPackType.Float:
                    return (long)_value.DoubleValue;
                case MsgPackType.Single:
                    return (long)_value.FloatValue;
                case MsgPackType.Boolean:
                    return _value.BoolValue ? 1 : 0;
                case MsgPackType.String:
                    var handle = GCHandle.FromIntPtr(_value.PtrValue);
                    var str = (string)handle.Target;
                    return long.TryParse(str, out var result) ? result : 0;
                default:
                    return 0;
            }
        }

        
        public ulong GetAsUInt64()
        {
            switch (_valueType)
            {
                case MsgPackType.UInt64:
                    return _value.UIntValue;
                case MsgPackType.Integer:
                    return (ulong)Math.Max(0, _value.IntValue);
                case MsgPackType.Float:
                    return (ulong)Math.Max(0, _value.DoubleValue);
                case MsgPackType.Single:
                    return (ulong)Math.Max(0, _value.FloatValue);
                case MsgPackType.Boolean:
                    return _value.BoolValue ? 1UL : 0UL;
                case MsgPackType.String:
                    var handle = GCHandle.FromIntPtr(_value.PtrValue);
                    var str = (string)handle.Target;
                    return ulong.TryParse(str, out var result) ? result : 0;
                default:
                    return 0;
            }
        }

        
        public double GetAsFloat()
        {
            switch (_valueType)
            {
                case MsgPackType.Float:
                    return _value.DoubleValue;
                case MsgPackType.Single:
                    return _value.FloatValue;
                case MsgPackType.Integer:
                    return _value.IntValue;
                case MsgPackType.UInt64:
                    return _value.UIntValue;
                case MsgPackType.Boolean:
                    return _value.BoolValue ? 1.0 : 0.0;
                case MsgPackType.String:
                    var handle = GCHandle.FromIntPtr(_value.PtrValue);
                    var str = (string)handle.Target;
                    return double.TryParse(str, out var result) ? result : 0.0;
                default:
                    return 0.0;
            }
        }

        
        public string GetAsString()
        {
            switch (_valueType)
            {
                case MsgPackType.String:
                    var handle = GCHandle.FromIntPtr(_value.PtrValue);
                    return (string)handle.Target ?? string.Empty;
                case MsgPackType.Integer:
                    return _value.IntValue.ToString();
                case MsgPackType.UInt64:
                    return _value.UIntValue.ToString();
                case MsgPackType.Float:
                    return _value.DoubleValue.ToString();
                case MsgPackType.Single:
                    return _value.FloatValue.ToString();
                case MsgPackType.Boolean:
                    return _value.BoolValue.ToString();
                case MsgPackType.Binary:
                    return Convert.ToBase64String(_binaryData ?? new byte[0]);
                default:
                    return string.Empty;
            }
        }

        
        public byte[] GetAsBytes()
        {
            switch (_valueType)
            {
                case MsgPackType.Binary:
                    return _binaryData ?? new byte[0];
                case MsgPackType.String:
                    var handle = GCHandle.FromIntPtr(_value.PtrValue);
                    var str = (string)handle.Target;
                    return Encoding.UTF8.GetBytes(str ?? string.Empty);
                case MsgPackType.Integer:
                    return BitConverter.GetBytes(_value.IntValue);
                case MsgPackType.UInt64:
                    return BitConverter.GetBytes(_value.UIntValue);
                case MsgPackType.Float:
                    return BitConverter.GetBytes(_value.DoubleValue);
                case MsgPackType.Single:
                    return BitConverter.GetBytes(_value.FloatValue);
                case MsgPackType.Boolean:
                    return BitConverter.GetBytes(_value.BoolValue);
                default:
                    return new byte[0];
            }
        }    
    
        public void DecodeFromBytesUnsafe(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return;

            // 解压缩数据
            var decompressed = Zip.Decompress(bytes);
            
            fixed (byte* ptr = decompressed)
            {
                int offset = 0;
                DecodeFromBufferUnsafe(ptr, ref offset, decompressed.Length);
            }
        }

        
        public void DecodeFromBufferUnsafe(byte* buffer, ref int offset, int bufferSize)
        {
            if (offset >= bufferSize) return;

            var typeByte = buffer[offset++];

            if (typeByte <= 0x7F)
            {
                // positive fixint
                SetAsInteger(typeByte);
            }
            else if (typeByte >= 0x80 && typeByte <= 0x8F)
            {
                // fixmap
                Clear();
                _valueType = MsgPackType.Map;
                var count = typeByte - 0x80;
                EnsureCapacity(count);
                
                for (int i = 0; i < count; i++)
                {
                    var key = ReadStringUnsafe(buffer, ref offset, bufferSize);
                    var child = InnerAdd();
                    child.SetName(key);
                    child.DecodeFromBufferUnsafe(buffer, ref offset, bufferSize);
                }
            }
            else if (typeByte >= 0x90 && typeByte <= 0x9F)
            {
                // fixarray
                Clear();
                _valueType = MsgPackType.Array;
                var count = typeByte - 0x90;
                EnsureCapacity(count);
                
                for (int i = 0; i < count; i++)
                {
                    var child = InnerAdd();
                    child.DecodeFromBufferUnsafe(buffer, ref offset, bufferSize);
                }
            }
            else if (typeByte >= 0xA0 && typeByte <= 0xBF)
            {
                // fixstr
                var length = typeByte - 0xA0;
                var str = ReadStringUnsafe(buffer, ref offset, bufferSize, length);
                SetAsString(str);
            }
            else if (typeByte >= 0xE0)
            {
                // negative fixint
                SetAsInteger((sbyte)typeByte);
            }
            else
            {
                switch (typeByte)
                {
                    case 0xC0: // nil
                        SetAsNull();
                        break;
                    case 0xC2: // false
                        SetAsBoolean(false);
                        break;
                    case 0xC3: // true
                        SetAsBoolean(true);
                        break;
                    case 0xC4: // bin 8
                        {
                            if (offset >= bufferSize) throw new InvalidOperationException("Buffer underflow");
                            var length = buffer[offset++];
                            var data = ReadBinaryUnsafe(buffer, ref offset, bufferSize, length);
                            SetAsBytes(data);
                        }
                        break;
                    case 0xC5: // bin 16
                        {
                            if (offset + 2 > bufferSize) throw new InvalidOperationException("Buffer underflow");
                            var length = (buffer[offset] << 8) | buffer[offset + 1];
                            offset += 2;
                            var data = ReadBinaryUnsafe(buffer, ref offset, bufferSize, length);
                            SetAsBytes(data);
                        }
                        break;
                    case 0xC6: // bin 32
                        {
                            if (offset + 4 > bufferSize) throw new InvalidOperationException("Buffer underflow");
                            var length = (buffer[offset] << 24) | (buffer[offset + 1] << 16) | 
                                        (buffer[offset + 2] << 8) | buffer[offset + 3];
                            offset += 4;
                            var data = ReadBinaryUnsafe(buffer, ref offset, bufferSize, length);
                            SetAsBytes(data);
                        }
                        break;
                    case 0xCA: // float 32
                        {
                            if (offset + 4 > bufferSize) throw new InvalidOperationException("Buffer underflow");
                            var bits = (uint)((buffer[offset] << 24) | (buffer[offset + 1] << 16) | 
                                             (buffer[offset + 2] << 8) | buffer[offset + 3]);
                            offset += 4;
                            SetAsSingle(*(float*)&bits);
                        }
                        break;
                    case 0xCB: // float 64
                        {
                            if (offset + 8 > bufferSize) throw new InvalidOperationException("Buffer underflow");
                            var bits = ((ulong)buffer[offset] << 56) | ((ulong)buffer[offset + 1] << 48) |
                                      ((ulong)buffer[offset + 2] << 40) | ((ulong)buffer[offset + 3] << 32) |
                                      ((ulong)buffer[offset + 4] << 24) | ((ulong)buffer[offset + 5] << 16) |
                                      ((ulong)buffer[offset + 6] << 8) | buffer[offset + 7];
                            offset += 8;
                            SetAsFloat(*(double*)&bits);
                        }
                        break;
                    case 0xCC: // uint 8
                        if (offset >= bufferSize) throw new InvalidOperationException("Buffer underflow");
                        SetAsInteger(buffer[offset++]);
                        break;
                    case 0xCD: // uint 16
                        {
                            if (offset + 2 > bufferSize) throw new InvalidOperationException("Buffer underflow");
                            var value = (buffer[offset] << 8) | buffer[offset + 1];
                            offset += 2;
                            SetAsInteger(value);
                        }
                        break;
                    case 0xCE: // uint 32
                        {
                            if (offset + 4 > bufferSize) throw new InvalidOperationException("Buffer underflow");
                            var value = (uint)((buffer[offset] << 24) | (buffer[offset + 1] << 16) | 
                                              (buffer[offset + 2] << 8) | buffer[offset + 3]);
                            offset += 4;
                            SetAsInteger(value);
                        }
                        break;
                    case 0xCF: // uint 64
                        {
                            if (offset + 8 > bufferSize) throw new InvalidOperationException("Buffer underflow");
                            var value = ((ulong)buffer[offset] << 56) | ((ulong)buffer[offset + 1] << 48) |
                                       ((ulong)buffer[offset + 2] << 40) | ((ulong)buffer[offset + 3] << 32) |
                                       ((ulong)buffer[offset + 4] << 24) | ((ulong)buffer[offset + 5] << 16) |
                                       ((ulong)buffer[offset + 6] << 8) | buffer[offset + 7];
                            offset += 8;
                            SetAsUInt64(value);
                        }
                        break;
                    case 0xD0: // int 8
                        if (offset >= bufferSize) throw new InvalidOperationException("Buffer underflow");
                        SetAsInteger((sbyte)buffer[offset++]);
                        break;
                    case 0xD1: // int 16
                        {
                            if (offset + 2 > bufferSize) throw new InvalidOperationException("Buffer underflow");
                            var value = (short)((buffer[offset] << 8) | buffer[offset + 1]);
                            offset += 2;
                            SetAsInteger(value);
                        }
                        break;
                    case 0xD2: // int 32
                        {
                            if (offset + 4 > bufferSize) throw new InvalidOperationException("Buffer underflow");
                            var value = (buffer[offset] << 24) | (buffer[offset + 1] << 16) | 
                                       (buffer[offset + 2] << 8) | buffer[offset + 3];
                            offset += 4;
                            SetAsInteger(value);
                        }
                        break;
                    case 0xD3: // int 64
                        {
                            if (offset + 8 > bufferSize) throw new InvalidOperationException("Buffer underflow");
                            var value = ((long)buffer[offset] << 56) | ((long)buffer[offset + 1] << 48) |
                                       ((long)buffer[offset + 2] << 40) | ((long)buffer[offset + 3] << 32) |
                                       ((long)buffer[offset + 4] << 24) | ((long)buffer[offset + 5] << 16) |
                                       ((long)buffer[offset + 6] << 8) | buffer[offset + 7];
                            offset += 8;
                            SetAsInteger(value);
                        }
                        break;
                    case 0xD9: // str 8
                        {
                            if (offset >= bufferSize) throw new InvalidOperationException("Buffer underflow");
                            var length = buffer[offset++];
                            var str = ReadStringUnsafe(buffer, ref offset, bufferSize, length);
                            SetAsString(str);
                        }
                        break;
                    case 0xDA: // str 16
                        {
                            if (offset + 2 > bufferSize) throw new InvalidOperationException("Buffer underflow");
                            var length = (buffer[offset] << 8) | buffer[offset + 1];
                            offset += 2;
                            var str = ReadStringUnsafe(buffer, ref offset, bufferSize, length);
                            SetAsString(str);
                        }
                        break;
                    case 0xDB: // str 32
                        {
                            if (offset + 4 > bufferSize) throw new InvalidOperationException("Buffer underflow");
                            var length = (buffer[offset] << 24) | (buffer[offset + 1] << 16) | 
                                        (buffer[offset + 2] << 8) | buffer[offset + 3];
                            offset += 4;
                            var str = ReadStringUnsafe(buffer, ref offset, bufferSize, length);
                            SetAsString(str);
                        }
                        break;
                    case 0xDC: // array 16
                        {
                            if (offset + 2 > bufferSize) throw new InvalidOperationException("Buffer underflow");
                            var count = (buffer[offset] << 8) | buffer[offset + 1];
                            offset += 2;
                            Clear();
                            _valueType = MsgPackType.Array;
                            EnsureCapacity(count);
                            
                            for (int i = 0; i < count; i++)
                            {
                                var child = InnerAdd();
                                child.DecodeFromBufferUnsafe(buffer, ref offset, bufferSize);
                            }
                        }
                        break;
                    case 0xDD: // array 32
                        {
                            if (offset + 4 > bufferSize) throw new InvalidOperationException("Buffer underflow");
                            var count = (buffer[offset] << 24) | (buffer[offset + 1] << 16) | 
                                       (buffer[offset + 2] << 8) | buffer[offset + 3];
                            offset += 4;
                            Clear();
                            _valueType = MsgPackType.Array;
                            EnsureCapacity(count);
                            
                            for (int i = 0; i < count; i++)
                            {
                                var child = InnerAdd();
                                child.DecodeFromBufferUnsafe(buffer, ref offset, bufferSize);
                            }
                        }
                        break;
                    case 0xDE: // map 16
                        {
                            if (offset + 2 > bufferSize) throw new InvalidOperationException("Buffer underflow");
                            var count = (buffer[offset] << 8) | buffer[offset + 1];
                            offset += 2;
                            Clear();
                            _valueType = MsgPackType.Map;
                            EnsureCapacity(count);
                            
                            for (int i = 0; i < count; i++)
                            {
                                var key = ReadStringUnsafe(buffer, ref offset, bufferSize);
                                var child = InnerAdd();
                                child.SetName(key);
                                child.DecodeFromBufferUnsafe(buffer, ref offset, bufferSize);
                            }
                        }
                        break;
                    case 0xDF: // map 32
                        {
                            if (offset + 4 > bufferSize) throw new InvalidOperationException("Buffer underflow");
                            var count = (buffer[offset] << 24) | (buffer[offset + 1] << 16) | 
                                       (buffer[offset + 2] << 8) | buffer[offset + 3];
                            offset += 4;
                            Clear();
                            _valueType = MsgPackType.Map;
                            EnsureCapacity(count);
                            
                            for (int i = 0; i < count; i++)
                            {
                                var key = ReadStringUnsafe(buffer, ref offset, bufferSize);
                                var child = InnerAdd();
                                child.SetName(key);
                                child.DecodeFromBufferUnsafe(buffer, ref offset, bufferSize);
                            }
                        }
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown MessagePack type: 0x{typeByte:X2}");
                }
            }
        } 
       
        private static string ReadStringUnsafe(byte* buffer, ref int offset, int bufferSize)
        {
            if (offset >= bufferSize) throw new InvalidOperationException("Buffer underflow");
            
            var typeByte = buffer[offset++];
            int length;

            if (typeByte >= 0xA0 && typeByte <= 0xBF)
            {
                length = typeByte - 0xA0;
            }
            else if (typeByte == 0xD9)
            {
                if (offset >= bufferSize) throw new InvalidOperationException("Buffer underflow");
                length = buffer[offset++];
            }
            else if (typeByte == 0xDA)
            {
                if (offset + 2 > bufferSize) throw new InvalidOperationException("Buffer underflow");
                length = (buffer[offset] << 8) | buffer[offset + 1];
                offset += 2;
            }
            else if (typeByte == 0xDB)
            {
                if (offset + 4 > bufferSize) throw new InvalidOperationException("Buffer underflow");
                length = (buffer[offset] << 24) | (buffer[offset + 1] << 16) | 
                        (buffer[offset + 2] << 8) | buffer[offset + 3];
                offset += 4;
            }
            else
            {
                throw new InvalidOperationException($"Invalid string type: 0x{typeByte:X2}");
            }

            return ReadStringUnsafe(buffer, ref offset, bufferSize, length);
        }

        
        private static string ReadStringUnsafe(byte* buffer, ref int offset, int bufferSize, int length)
        {
            if (length == 0) return string.Empty;
            if (offset + length > bufferSize) throw new InvalidOperationException("Buffer underflow");

            var managedBytes = new byte[length];
            Marshal.Copy(new IntPtr(buffer + offset), managedBytes, 0, length);
            var result = Encoding.UTF8.GetString(managedBytes, 0, length);
            offset += length;
            return result;
        }

        
        private static byte[] ReadBinaryUnsafe(byte* buffer, ref int offset, int bufferSize, int length)
        {
            if (length == 0) return new byte[0];
            if (offset + length > bufferSize) throw new InvalidOperationException("Buffer underflow");

            var result = new byte[length];
            fixed (byte* dest = result)
            {
                Marshal.Copy(new IntPtr(buffer + offset), result, 0, length);
            }
            offset += length;
            return result;
        }

        public virtual byte[] Encode2Bytes()
        {
            // 估算缓冲区大小
            var estimatedSize = EstimateSize();
            var buffer = new byte[estimatedSize];
            
            fixed (byte* ptr = buffer)
            {
                int offset = 0;
                EncodeToBufferUnsafe(ptr, ref offset, estimatedSize);
                
                // 创建实际大小的数组
                var result = new byte[offset];
                Array.Copy(buffer, result, offset);
                
                // 压缩并返回
                return Zip.Compress(result);
            }
        }

        
        private int EstimateSize()
        {
            switch (_valueType)
            {
                case MsgPackType.Null:
                case MsgPackType.Boolean:
                    return 1;
                case MsgPackType.Integer:
                case MsgPackType.UInt64:
                    return 9; // 最大情况
                case MsgPackType.Float:
                    return 9;
                case MsgPackType.Single:
                    return 5;
                case MsgPackType.String:
                    var handle = GCHandle.FromIntPtr(_value.PtrValue);
                    var str = (string)handle.Target;
                    return 5 + Encoding.UTF8.GetByteCount(str ?? string.Empty);
                case MsgPackType.Binary:
                    return 5 + (_binaryData?.Length ?? 0);
                case MsgPackType.Array:
                case MsgPackType.Map:
                    var size = 5; // header
                    for (int i = 0; i < _childCount; i++)
                    {
                        if (_valueType == MsgPackType.Map)
                        {
                            size += 5 + Encoding.UTF8.GetByteCount(_children[i]._name ?? string.Empty);
                        }
                        size += _children[i].EstimateSize();
                    }
                    return size;
                default:
                    return 1;
            }
        }

        // 便利方法和属性
        public void Add(string key, string value)
        {
            var child = InnerAddMapChild();
            child.SetName(key);
            child.SetAsString(value);
        }

        public void Add(string key, long value)
        {
            var child = InnerAddMapChild();
            child.SetName(key);
            child.SetAsInteger(value);
        }

        public void Add(string key, double value)
        {
            var child = InnerAddMapChild();
            child.SetName(key);
            child.SetAsFloat(value);
        }

        public void Add(string key, bool value)
        {
            var child = InnerAddMapChild();
            child.SetName(key);
            child.SetAsBoolean(value);
        }

        public void Add(string key, byte[] value)
        {
            var child = InnerAddMapChild();
            child.SetName(key);
            child.SetAsBytes(value);
        }

        public MsgPack ForcePathObject(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            var current = this;
            var parts = path.Split(new[] { '.', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length - 1; i++)
            {
                var existing = current.FindObject(parts[i]);
                if (existing == null)
                {
                    existing = current.InnerAddMapChild();
                    existing.SetName(parts[i]);
                }
                current = existing;
            }

            var finalPart = parts[parts.Length - 1];
            var final = current.FindObject(finalPart);
            if (final == null)
            {
                final = current.InnerAddMapChild();
                final.SetName(finalPart);
            }

            return final;
        }

        public bool LoadFileAsBytes(string fileName)
        {
            try
            {
                if (!File.Exists(fileName)) return false;
                var data = File.ReadAllBytes(fileName);
                SetAsBytes(data);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool SaveBytesToFile(string fileName)
        {
            try
            {
                if (_binaryData == null) return false;
                File.WriteAllBytes(fileName, _binaryData);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // 属性
        public string AsString
        {
            get => GetAsString();
            set => SetAsString(value);
        }

        public long AsInteger
        {
            get => GetAsInteger();
            set => SetAsInteger(value);
        }

        public double AsFloat
        {
            get => GetAsFloat();
            set => SetAsFloat(value);
        }

        public MsgPackArray AsArray
        {
            get
            {
                if (_refAsArray == null)
                {
                    _refAsArray = new MsgPackArray(this, _children, _childCount);
                }
                return _refAsArray;
            }
        }

        public MsgPackType ValueType => _valueType;

        // IEnumerable 实现
        IEnumerator IEnumerable.GetEnumerator()
        {
            return new MsgPackEnum(_children, _childCount);
        }

        // IDisposable 实现
        public void Dispose()
        {
            if (_disposed) return;

            Clear();
            
            // 释放字符串的 GCHandle
            if (_valueType == MsgPackType.String && _value.PtrValue != IntPtr.Zero)
            {
                try
                {
                    var handle = GCHandle.FromIntPtr(_value.PtrValue);
                    if (handle.IsAllocated)
                        handle.Free();
                }
                catch { }
            }

            _disposed = true;
        }

        ~MsgPack()
        {
            Dispose();
        }
    }
}