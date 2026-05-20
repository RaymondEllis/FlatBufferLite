using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlatBufferLite;

public ref struct FlatBufferBuilder
{
	Span<byte> _buf;
	int _space;
	int _minAlign;
	int _pendingRoot;
	ReadOnlySpan<byte> _pendingIdentifier;

	public FlatBufferBuilder(Span<byte> buffer)
	{
		_buf = buffer;
		_space = buffer.Length;
		_minAlign = 1;
	}

	public readonly Span<byte> Buffer
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _buf;
	}

	public readonly int Length
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _buf.Length - _space;
	}

	public ReadOnlySpan<byte> AsSpan()
	{
		if (_pendingRoot != 0)
		{
			if (_pendingIdentifier.IsEmpty)
				Finish(_pendingRoot);
			else
				Finish(_pendingRoot, _pendingIdentifier);
			_pendingRoot = 0;
			_pendingIdentifier = default;
		}
		return _buf.Slice(_space);
	}

	public void MarkRoot(int pos, ReadOnlySpan<byte> fileIdentifier = default)
	{
		_pendingRoot = pos;
		_pendingIdentifier = fileIdentifier;
	}

	public void Reset()
	{
		_space = _buf.Length;
		_minAlign = 1;
		_pendingRoot = 0;
		_pendingIdentifier = default;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	void Align(int alignment, int upcomingBytes)
	{
		if (alignment > _minAlign)
			_minAlign = alignment;
		int written = _buf.Length - _space + upcomingBytes;
		int padding = (-written) & (alignment - 1);
		int needed = padding + upcomingBytes;
		if (_space < needed)
			ThrowBufferTooSmall();
		while (padding-- > 0)
			_buf[--_space] = 0;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	unsafe void PutUnaligned<T>(T value) where T : unmanaged
	{
		_space -= sizeof(T);
		Unsafe.WriteUnaligned(ref _buf[_space], value);
	}

	public int StartTable(int slotCount, int inlineSize, int inlineAlign)
	{
		int vtableSize = 4 + 2 * slotCount;
		int total = vtableSize + 4 + inlineSize;
		int align = inlineAlign < 4 ? 4 : inlineAlign;
		Align(align, total);
		_space -= total;
		int vtStart = _space;
		int tablePos = vtStart + vtableSize;
		_buf.Slice(vtStart, total).Clear();
		Unsafe.WriteUnaligned(ref _buf[vtStart], (ushort)vtableSize);
		Unsafe.WriteUnaligned(ref _buf[vtStart + 2], (ushort)(4 + inlineSize));
		Unsafe.WriteUnaligned(ref _buf[tablePos], vtableSize);
		return tablePos;
	}

	public int CreateString(ReadOnlySpan<byte> utf8Bytes)
	{
		int byteCount = utf8Bytes.Length;
		Align(4, byteCount + 1 + 4);
		_buf[--_space] = 0;
		_space -= byteCount;
		utf8Bytes.CopyTo(_buf.Slice(_space, byteCount));
		PutUnaligned(byteCount);
		return _space;
	}

	public int CreateVector<T>(ReadOnlySpan<T> values) where T : unmanaged
	{
		int elt = Unsafe.SizeOf<T>();
		int bytes = values.Length * elt;
		int align = elt < 4 ? 4 : elt;
		Align(align, bytes + 4);
		_space -= bytes;
		MemoryMarshal.AsBytes(values).CopyTo(_buf.Slice(_space, bytes));
		PutUnaligned(values.Length);
		return _space;
	}

	public int CreateVectorOfOffsets(ReadOnlySpan<int> offsets)
	{
		int bytes = offsets.Length * 4;
		Align(4, bytes + 4);
		for (int i = offsets.Length - 1; i >= 0; i--)
		{
			int slotPos = _space - 4;
			int v = offsets[i] - slotPos;
			if (v <= 0)
				ThrowBackwardOffset();
			_space = slotPos;
			Unsafe.WriteUnaligned(ref _buf[_space], v);
		}
		PutUnaligned(offsets.Length);
		return _space;
	}

	void Finish(int rootTablePos)
	{
		Align(_minAlign, 4);
		_space -= 4;
		int v = rootTablePos - _space;
		if (v <= 0)
			ThrowBackwardOffset();
		Unsafe.WriteUnaligned(ref _buf[_space], v);
	}

	void Finish(int rootTablePos, ReadOnlySpan<byte> fileIdentifier)
	{
		if (fileIdentifier.Length != FlatBufferReader.FileIdentifierLength)
			ThrowBadIdentifier();
		Align(_minAlign, 4 + FlatBufferReader.FileIdentifierLength);
		_space -= FlatBufferReader.FileIdentifierLength;
		fileIdentifier.CopyTo(_buf.Slice(_space, FlatBufferReader.FileIdentifierLength));
		_space -= 4;
		int v = rootTablePos - _space;
		if (v <= 0)
			ThrowBackwardOffset();
		Unsafe.WriteUnaligned(ref _buf[_space], v);
	}

	static void ThrowBufferTooSmall() => throw new InvalidOperationException("FlatBufferBuilder destination buffer is too small.");
	static void ThrowBackwardOffset() => throw new InvalidOperationException("Referenced data must be created before the table/vector that references it.");
	static void ThrowBadIdentifier() => throw new ArgumentException("File identifier must be exactly 4 bytes.");
}