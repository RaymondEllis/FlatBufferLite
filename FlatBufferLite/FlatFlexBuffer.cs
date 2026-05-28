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
	IndirectInt = 6,
	IndirectUInt = 7,
	IndirectFloat = 8,
	Map = 9,
	Vector = 10,
	VectorInt = 11,
	VectorUInt = 12,
	VectorFloat = 13,
	VectorKey = 14,
	VectorStringDeprecated = 15,
	VectorInt2 = 16,
	VectorUInt2 = 17,
	VectorFloat2 = 18,
	VectorInt3 = 19,
	VectorUInt3 = 20,
	VectorFloat3 = 21,
	VectorInt4 = 22,
	VectorUInt4 = 23,
	VectorFloat4 = 24,
	Blob = 25,
	Bool = 26,
	VectorBool = 36
}

public readonly ref struct FlatFlexBuffer
{
	readonly Span<byte> _buffer;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FlatFlexBuffer(Span<byte> buffer)
	{
		_buffer = buffer;
	}

	public readonly Span<byte> AsSpan
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _buffer;
	}

	public readonly bool IsValid
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Root.IsValid;
	}

	public readonly FlatFlexBufferValue Root
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => FlatFlexBufferValue.ReadRoot(_buffer);
	}
}

public readonly ref struct FlatFlexBufferValue
{
	readonly Span<byte> _buffer;
	readonly int _valuePos;
	readonly int _parentWidth;
	readonly int _valueWidth;
	readonly FlexBufferType _type;
	readonly bool _isValid;

	FlatFlexBufferValue(Span<byte> buffer, int valuePos, int parentWidth, int valueWidth, FlexBufferType type)
	{
		_buffer = buffer;
		_valuePos = valuePos;
		_parentWidth = parentWidth;
		_valueWidth = valueWidth;
		_type = type;
		_isValid = true;
	}

	public readonly bool IsValid
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _isValid;
	}

	public readonly FlexBufferType Type
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _type;
	}

	public static FlatFlexBufferValue ReadRoot(Span<byte> buffer)
	{
		if (buffer.Length < 3)
			return default;

		int parentWidth = buffer[^1];
		if (parentWidth != 1 && parentWidth != 2 && parentWidth != 4 && parentWidth != 8)
			return default;

		byte packedType = buffer[^2];
		int valueWidth = 1 << (packedType & 0x03);
		int valuePos = buffer.Length - 2 - parentWidth;
		if (valuePos < 0)
			return default;

		return new FlatFlexBufferValue(buffer, valuePos, parentWidth, valueWidth, (FlexBufferType)(packedType >> 2));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGetInt64(out long value)
	{
		return TryGetInt64Core(out value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long AsInt64(long defaultValue = default) => TryGetInt64Core(out long value) ? value : defaultValue;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGetUInt64(out ulong value)
	{
		return TryGetUInt64Core(out value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ulong AsUInt64(ulong defaultValue = default) => TryGetUInt64Core(out ulong value) ? value : defaultValue;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGetDouble(out double value)
	{
		return TryGetDoubleCore(out value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public double AsDouble(double defaultValue = default) => TryGetDoubleCore(out double value) ? value : defaultValue;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGetBool(out bool value)
	{
		if (_type != FlexBufferType.Bool)
		{
			value = default;
			return false;
		}
		if (!TryReadUnsigned(_valuePos, _valueWidth, out ulong raw))
		{
			value = default;
			return false;
		}
		value = raw != 0;
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool AsBool(bool defaultValue = default) => TryGetBool(out bool value) ? value : defaultValue;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGetStringBytes(out ReadOnlySpan<byte> utf8)
	{
		if (_type != FlexBufferType.String && _type != FlexBufferType.Key)
		{
			utf8 = default;
			return false;
		}
		return TryGetIndirectBytes(expectNullTerminator: _type == FlexBufferType.String, out utf8);
	}

	public readonly ReadOnlySpan<byte> AsStringBytes => TryGetStringBytes(out var utf8) ? utf8 : default;

	public override string ToString()
	{
		if (TryGetStringBytes(out var utf8))
			return Encoding.UTF8.GetString(utf8);
		return string.Empty;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGetBlobBytes(out ReadOnlySpan<byte> bytes)
	{
		if (_type != FlexBufferType.Blob)
		{
			bytes = default;
			return false;
		}
		return TryGetIndirectBytes(expectNullTerminator: false, out bytes);
	}

	public readonly ReadOnlySpan<byte> AsBlobBytes => TryGetBlobBytes(out var bytes) ? bytes : default;

	bool TryGetInt64Core(out long value)
	{
		switch (_type)
		{
			case FlexBufferType.Int:
				if (TryReadSigned(_valuePos, _valueWidth, out value))
					return true;
				break;
			case FlexBufferType.UInt:
				if (TryReadUnsigned(_valuePos, _valueWidth, out ulong uv))
				{
					value = (long)uv;
					return true;
				}
				break;
			case FlexBufferType.IndirectInt:
				if (TryReadIndirectSigned(out value))
					return true;
				break;
			case FlexBufferType.IndirectUInt:
				if (TryReadIndirectUnsigned(out uv))
				{
					value = (long)uv;
					return true;
				}
				break;
			case FlexBufferType.Bool:
				if (TryReadUnsigned(_valuePos, _valueWidth, out uv))
				{
					value = uv != 0 ? 1 : 0;
					return true;
				}
				break;
		}
		value = default;
		return false;
	}

	bool TryGetUInt64Core(out ulong value)
	{
		switch (_type)
		{
			case FlexBufferType.UInt:
				return TryReadUnsigned(_valuePos, _valueWidth, out value);
			case FlexBufferType.Int:
				if (TryReadSigned(_valuePos, _valueWidth, out long sv) && sv >= 0)
				{
					value = (ulong)sv;
					return true;
				}
				break;
			case FlexBufferType.IndirectUInt:
				return TryReadIndirectUnsigned(out value);
			case FlexBufferType.IndirectInt:
				if (TryReadIndirectSigned(out sv) && sv >= 0)
				{
					value = (ulong)sv;
					return true;
				}
				break;
			case FlexBufferType.Bool:
				if (TryReadUnsigned(_valuePos, _valueWidth, out ulong bv))
				{
					value = bv != 0 ? 1UL : 0UL;
					return true;
				}
				break;
		}
		value = default;
		return false;
	}

	bool TryGetDoubleCore(out double value)
	{
		switch (_type)
		{
			case FlexBufferType.Float:
				if (TryReadFloat(_valuePos, _valueWidth, out value))
					return true;
				break;
			case FlexBufferType.Int:
				if (TryReadSigned(_valuePos, _valueWidth, out long sv))
				{
					value = sv;
					return true;
				}
				break;
			case FlexBufferType.UInt:
				if (TryReadUnsigned(_valuePos, _valueWidth, out ulong uv))
				{
					value = uv;
					return true;
				}
				break;
			case FlexBufferType.IndirectFloat:
				if (TryReadIndirectFloat(out value))
					return true;
				break;
			case FlexBufferType.IndirectInt:
				if (TryReadIndirectSigned(out sv))
				{
					value = sv;
					return true;
				}
				break;
			case FlexBufferType.IndirectUInt:
				if (TryReadIndirectUnsigned(out uv))
				{
					value = uv;
					return true;
				}
				break;
			case FlexBufferType.Bool:
				if (TryReadUnsigned(_valuePos, _valueWidth, out uv))
				{
					value = uv != 0 ? 1d : 0d;
					return true;
				}
				break;
		}
		value = default;
		return false;
	}

	bool TryReadIndirectSigned(out long value)
	{
		if (!TryGetIndirectAddress(out int target))
		{
			value = default;
			return false;
		}
		return TryReadSigned(target, _valueWidth, out value);
	}

	bool TryReadIndirectUnsigned(out ulong value)
	{
		if (!TryGetIndirectAddress(out int target))
		{
			value = default;
			return false;
		}
		return TryReadUnsigned(target, _valueWidth, out value);
	}

	bool TryReadIndirectFloat(out double value)
	{
		if (!TryGetIndirectAddress(out int target))
		{
			value = default;
			return false;
		}
		return TryReadFloat(target, _valueWidth, out value);
	}

	bool TryGetIndirectAddress(out int target)
	{
		target = default;
		if (!TryReadUnsigned(_valuePos, _parentWidth, out ulong offset))
			return false;
		if (offset > (ulong)_valuePos)
			return false;
		target = _valuePos - (int)offset;
		return true;
	}

	bool TryGetIndirectBytes(bool expectNullTerminator, out ReadOnlySpan<byte> bytes)
	{
		if (!TryGetIndirectAddress(out int start))
		{
			bytes = default;
			return false;
		}
		int lenPos = start - _valueWidth;
		if (!TryReadUnsigned(lenPos, _valueWidth, out ulong lenU))
		{
			bytes = default;
			return false;
		}
		if (lenU > int.MaxValue)
		{
			bytes = default;
			return false;
		}
		int len = (int)lenU;
		int total = expectNullTerminator ? len + 1 : len;
		if (start < 0 || total < 0 || start > _buffer.Length || total > _buffer.Length - start)
		{
			bytes = default;
			return false;
		}
		if (expectNullTerminator && _buffer[start + len] != 0)
		{
			bytes = default;
			return false;
		}
		bytes = _buffer.Slice(start, len);
		return true;
	}

	bool TryReadFloat(int position, int width, out double value)
	{
		if (width == 4)
		{
			if (position < 0 || position > _buffer.Length - 4)
			{
				value = default;
				return false;
			}
			value = FlatBufferReader.ReadUnaligned<float>(_buffer, position);
			return true;
		}
		if (width == 8)
		{
			if (position < 0 || position > _buffer.Length - 8)
			{
				value = default;
				return false;
			}
			value = FlatBufferReader.ReadUnaligned<double>(_buffer, position);
			return true;
		}
		value = default;
		return false;
	}

	bool TryReadSigned(int position, int width, out long value)
	{
		if (!TryReadUnsigned(position, width, out ulong raw))
		{
			value = default;
			return false;
		}
		value = width switch
		{
			1 => (sbyte)raw,
			2 => (short)raw,
			4 => (int)raw,
			8 => (long)raw,
			_ => default
		};
		return width is 1 or 2 or 4 or 8;
	}

	bool TryReadUnsigned(int position, int width, out ulong value)
	{
		value = default;
		if (position < 0 || width <= 0 || position > _buffer.Length - width)
			return false;
		switch (width)
		{
			case 1:
				value = _buffer[position];
				return true;
			case 2:
				value = FlatBufferReader.ReadUnaligned<ushort>(_buffer, position);
				return true;
			case 4:
				value = FlatBufferReader.ReadUnaligned<uint>(_buffer, position);
				return true;
			case 8:
				value = FlatBufferReader.ReadUnaligned<ulong>(_buffer, position);
				return true;
			default:
				return false;
		}
	}
}
