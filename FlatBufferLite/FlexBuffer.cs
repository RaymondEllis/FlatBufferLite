using System.Runtime.CompilerServices;
using System.Text;

namespace FlatBufferLite;

public enum FlexBufferType : byte
{
	Null = 0,
	Int = 1,
	UInt = 2,
	Float = 3,
	Key = 4,
	String = 5,
	Map = 9,
	Vector = 10,
	Blob = 25,
	Bool = 26,
}

public readonly struct FlexBufferValue
{
	internal readonly FlexBufferType Type;
	internal readonly long IntValue;
	internal readonly ulong UIntValue;
	internal readonly double FloatValue;
	internal readonly int Offset;

	FlexBufferValue(FlexBufferType type, long signedInt, ulong unsignedInt, double floatingPoint, int offset)
	{
		Type = type;
		IntValue = signedInt;
		UIntValue = unsignedInt;
		FloatValue = floatingPoint;
		Offset = offset;
	}

	public static FlexBufferValue Null => new(FlexBufferType.Null, 0, 0, 0, 0);
	public static FlexBufferValue Bool(bool value) => new(FlexBufferType.Bool, value ? 1 : 0, value ? 1UL : 0, 0, 0);
	public static FlexBufferValue Int(long value) => new(FlexBufferType.Int, value, unchecked((ulong)value), 0, 0);
	public static FlexBufferValue UInt(ulong value) => new(FlexBufferType.UInt, unchecked((long)value), value, 0, 0);
	public static FlexBufferValue Float(double value) => new(FlexBufferType.Float, 0, 0, value, 0);

	internal static FlexBufferValue String(int offset) => new(FlexBufferType.String, 0, 0, 0, offset);
	internal static FlexBufferValue Blob(int offset) => new(FlexBufferType.Blob, 0, 0, 0, offset);
	internal static FlexBufferValue Vector(int offset) => new(FlexBufferType.Vector, 0, 0, 0, offset);
}

public ref struct FlexBufferBuilder
{
	const int Width = 8;
	const int BitWidth = 3;

	Span<byte> _buffer;
	int _pos;

	public FlexBufferBuilder(Span<byte> buffer)
	{
		_buffer = buffer;
		_pos = 0;
	}

	public readonly int Length
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _pos;
	}

	public void Reset() => _pos = 0;

	public FlexBufferValue CreateString(scoped ReadOnlySpan<byte> utf8Bytes)
	{
		int dataOffset = WriteSizedData(utf8Bytes, addTerminator: true);
		return FlexBufferValue.String(dataOffset);
	}

	public FlexBufferValue CreateBlob(scoped ReadOnlySpan<byte> bytes)
	{
		int dataOffset = WriteSizedData(bytes, addTerminator: false);
		return FlexBufferValue.Blob(dataOffset);
	}

	public FlexBufferValue CreateVector(scoped ReadOnlySpan<FlexBufferValue> values)
	{
		Align(Width);
		WriteUInt((ulong)values.Length, Width);
		int dataOffset = _pos;
		for (int i = 0; i < values.Length; i++)
			WriteValue(values[i], Width);
		for (int i = 0; i < values.Length; i++)
			WriteByte(Pack(values[i].Type));
		return FlexBufferValue.Vector(dataOffset);
	}

	public Span<byte> Finish(FlexBufferValue root)
	{
		int width = root.Type == FlexBufferType.Null ? 1 : Width;
		Align(width);
		WriteValue(root, width);
		WriteByte((byte)width);
		WriteByte(Pack(root.Type));
		return _buffer.Slice(0, _pos);
	}

	int WriteSizedData(scoped ReadOnlySpan<byte> bytes, bool addTerminator)
	{
		Align(Width);
		WriteUInt((ulong)bytes.Length, Width);
		int dataOffset = _pos;
		Ensure(bytes.Length + (addTerminator ? 1 : 0));
		bytes.CopyTo(_buffer.Slice(_pos, bytes.Length));
		_pos += bytes.Length;
		if (addTerminator)
			_buffer[_pos++] = 0;
		return dataOffset;
	}

	void WriteValue(FlexBufferValue value, int width)
	{
		switch (value.Type)
		{
			case FlexBufferType.Null:
				WriteUInt(0, width);
				break;
			case FlexBufferType.Bool:
			case FlexBufferType.Int:
				WriteInt(value.IntValue, width);
				break;
			case FlexBufferType.UInt:
				WriteUInt(value.UIntValue, width);
				break;
			case FlexBufferType.Float:
				WriteUInt(BitConverter.DoubleToUInt64Bits(value.FloatValue), width);
				break;
			case FlexBufferType.String:
			case FlexBufferType.Blob:
			case FlexBufferType.Vector:
				int offset = _pos - value.Offset;
				if (offset < 0)
					throw new InvalidOperationException("FlexBuffer child values must be created before the value that references them.");
				WriteUInt((ulong)offset, width);
				break;
			default:
				throw new NotSupportedException($"FlexBuffer type '{value.Type}' is not supported by this builder.");
		}
	}

	void Align(int width)
	{
		int padding = (-_pos) & (width - 1);
		Ensure(padding);
		_buffer.Slice(_pos, padding).Clear();
		_pos += padding;
	}

	void WriteByte(byte value)
	{
		Ensure(1);
		_buffer[_pos++] = value;
	}

	void WriteInt(long value, int width) => WriteUInt(unchecked((ulong)value), width);

	void WriteUInt(ulong value, int width)
	{
		Ensure(width);
		for (int i = 0; i < width; i++)
			_buffer[_pos + i] = (byte)(value >> (8 * i));
		_pos += width;
	}

	void Ensure(int byteCount)
	{
		if (_pos + byteCount > _buffer.Length)
			throw new InvalidOperationException($"Buffer too small: {byteCount} bytes needed, {_buffer.Length - _pos} available.");
	}

	static byte Pack(FlexBufferType type) => (byte)(((byte)type << 2) | BitWidth);
}

public readonly ref struct FlexBuffer
{
	readonly ReadOnlySpan<byte> _buffer;
	readonly int _offset;
	readonly int _byteWidth;

	public FlexBufferType Type { get; }

	internal FlexBuffer(ReadOnlySpan<byte> buffer, int offset, FlexBufferType type, int byteWidth)
	{
		_buffer = buffer;
		_offset = offset;
		Type = type;
		_byteWidth = byteWidth;
	}

	public static FlexBuffer GetRoot(ReadOnlySpan<byte> buffer)
	{
		if (buffer.Length < 3)
			throw new ArgumentException("FlexBuffer data must include a root value, byte width, and type.", nameof(buffer));
		int byteWidth = buffer[^2];
		if (!IsValidByteWidth(byteWidth) || buffer.Length < byteWidth + 2)
			throw new ArgumentException("FlexBuffer root byte width is invalid.", nameof(buffer));
		byte packedType = buffer[^1];
		return new FlexBuffer(buffer, buffer.Length - 2 - byteWidth, UnpackType(packedType), byteWidth);
	}

	public bool IsNull => Type == FlexBufferType.Null;

	public bool AsBool => Type switch
	{
		FlexBufferType.Bool => ReadUInt(_buffer, _offset, _byteWidth) != 0,
		FlexBufferType.Null => false,
		_ => throw WrongType(nameof(AsBool)),
	};

	public long AsInt64 => Type switch
	{
		FlexBufferType.Int => ReadInt(_buffer, _offset, _byteWidth),
		FlexBufferType.UInt => checked((long)ReadUInt(_buffer, _offset, _byteWidth)),
		FlexBufferType.Bool => AsBool ? 1 : 0,
		_ => throw WrongType(nameof(AsInt64)),
	};

	public ulong AsUInt64 => Type switch
	{
		FlexBufferType.UInt => ReadUInt(_buffer, _offset, _byteWidth),
		FlexBufferType.Int => checked((ulong)ReadInt(_buffer, _offset, _byteWidth)),
		FlexBufferType.Bool => AsBool ? 1UL : 0UL,
		_ => throw WrongType(nameof(AsUInt64)),
	};

	public double AsDouble => Type switch
	{
		FlexBufferType.Float when _byteWidth == 4 => BitConverter.Int32BitsToSingle((int)ReadUInt(_buffer, _offset, _byteWidth)),
		FlexBufferType.Float => BitConverter.UInt64BitsToDouble(ReadUInt(_buffer, _offset, _byteWidth)),
		FlexBufferType.Int => AsInt64,
		FlexBufferType.UInt => AsUInt64,
		_ => throw WrongType(nameof(AsDouble)),
	};

	public string AsString => Encoding.UTF8.GetString(AsStringBytes);

	public ReadOnlySpan<byte> AsStringBytes
	{
		get
		{
			if (Type != FlexBufferType.String && Type != FlexBufferType.Key)
				throw WrongType(nameof(AsStringBytes));
			int dataOffset = IndirectOffset();
			int length = checked((int)ReadUInt(_buffer, dataOffset - _byteWidth, _byteWidth));
			return Slice(dataOffset, length);
		}
	}

	public ReadOnlySpan<byte> AsBlob
	{
		get
		{
			if (Type != FlexBufferType.Blob)
				throw WrongType(nameof(AsBlob));
			int dataOffset = IndirectOffset();
			int length = checked((int)ReadUInt(_buffer, dataOffset - _byteWidth, _byteWidth));
			return Slice(dataOffset, length);
		}
	}

	public FlexBufferVector AsVector
	{
		get
		{
			if (Type != FlexBufferType.Vector)
				throw WrongType(nameof(AsVector));
			int dataOffset = IndirectOffset();
			int length = checked((int)ReadUInt(_buffer, dataOffset - _byteWidth, _byteWidth));
			return new FlexBufferVector(_buffer, dataOffset, length, _byteWidth);
		}
	}

	int IndirectOffset()
	{
		ulong distance = ReadUInt(_buffer, _offset, _byteWidth);
		if (distance > (ulong)_offset)
			throw new ArgumentException("FlexBuffer indirect offset points outside the buffer.");
		return _offset - checked((int)distance);
	}

	ReadOnlySpan<byte> Slice(int offset, int length)
	{
		if ((uint)offset + (uint)length > (uint)_buffer.Length)
			throw new ArgumentException("FlexBuffer value points outside the buffer.");
		return _buffer.Slice(offset, length);
	}

	InvalidOperationException WrongType(string member) => new($"Cannot read FlexBuffer {Type} value using {member}.");

	internal static FlexBufferType UnpackType(byte packedType) => (FlexBufferType)(packedType >> 2);
	internal static int UnpackByteWidth(byte packedType) => 1 << (packedType & 3);

	static bool IsValidByteWidth(int byteWidth) => byteWidth is 1 or 2 or 4 or 8;

	internal static ulong ReadUInt(ReadOnlySpan<byte> buffer, int offset, int byteWidth)
	{
		if (!IsValidByteWidth(byteWidth) || (uint)offset + (uint)byteWidth > (uint)buffer.Length)
			throw new ArgumentException("FlexBuffer value points outside the buffer.");
		ulong value = 0;
		for (int i = 0; i < byteWidth; i++)
			value |= (ulong)buffer[offset + i] << (8 * i);
		return value;
	}

	static long ReadInt(ReadOnlySpan<byte> buffer, int offset, int byteWidth)
	{
		ulong value = ReadUInt(buffer, offset, byteWidth);
		int shift = (8 - byteWidth) * 8;
		return ((long)value << shift) >> shift;
	}
}

public readonly ref struct FlexBufferVector
{
	readonly ReadOnlySpan<byte> _buffer;
	readonly int _offset;
	readonly int _length;
	readonly int _byteWidth;

	internal FlexBufferVector(ReadOnlySpan<byte> buffer, int offset, int length, int byteWidth)
	{
		_buffer = buffer;
		_offset = offset;
		_length = length;
		_byteWidth = byteWidth;
	}

	public int Length => _length;

	public FlexBuffer this[int index]
	{
		get
		{
			if ((uint)index >= (uint)_length)
				throw new ArgumentOutOfRangeException(nameof(index));
			int typeOffset = _offset + _length * _byteWidth + index;
			if ((uint)typeOffset >= (uint)_buffer.Length)
				throw new ArgumentException("FlexBuffer vector type table points outside the buffer.");
			byte packedType = _buffer[typeOffset];
			return new FlexBuffer(_buffer, _offset + index * _byteWidth, FlexBuffer.UnpackType(packedType), FlexBuffer.UnpackByteWidth(packedType));
		}
	}
}
