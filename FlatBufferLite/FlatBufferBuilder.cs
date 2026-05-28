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

	public Span<byte> Finish()
	{
		if (_pendingRoot != 0)
		{
			if (_pendingIdentifier.IsEmpty)
				WriteRoot(_pendingRoot);
			else
				WriteRoot(_pendingRoot, _pendingIdentifier);
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
			throw new InvalidOperationException($"Buffer too small: {needed} bytes needed, {_space} available.");
		if (padding > 0)
		{
			_space -= padding;
			_buf.Slice(_space, padding).Clear();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	void PutUnaligned<T>(T value) where T : unmanaged
	{
		_space -= Unsafe.SizeOf<T>();
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

	public StringOffset CreateString(scoped ReadOnlySpan<byte> utf8Bytes)
	{
		int byteCount = utf8Bytes.Length;
		Align(4, byteCount + 1 + 4);
		_buf[--_space] = 0;
		_space -= byteCount;
		utf8Bytes.CopyTo(_buf.Slice(_space, byteCount));
		PutUnaligned(byteCount);
		return new StringOffset(_space);
	}

	public VectorOffset CreateVector<T>(scoped ReadOnlySpan<T> values) where T : unmanaged
	{
		int elt = Unsafe.SizeOf<T>();
		int bytes = values.Length * elt;
		int align = elt < 4 ? 4 : elt;
		Align(align, bytes + 4);
		_space -= bytes;
		MemoryMarshal.AsBytes(values).CopyTo(_buf.Slice(_space, bytes));
		PutUnaligned(values.Length);
		return new VectorOffset(_space);
	}

	public VectorOffset CreateFlexBuffer(scoped ReadOnlySpan<byte> value) => CreateVector(value);

	public VectorOffset CreateVectorOfOffsets(scoped ReadOnlySpan<int> offsets)
	{
		int bytes = offsets.Length * 4;
		Align(4, bytes + 4);
		for (int i = offsets.Length - 1; i >= 0; i--)
		{
			int slotPos = _space - 4;
			int v = offsets[i] - slotPos;
			if (v <= 0)
				throw new InvalidOperationException($"Offset data[{offsets[i]}] must be created before the vector slot at {slotPos}.");
			_space = slotPos;
			Unsafe.WriteUnaligned(ref _buf[_space], v);
		}
		PutUnaligned(offsets.Length);
		return new VectorOffset(_space);
	}

	public VectorOffset CreateVectorOfOffsets<T>(scoped ReadOnlySpan<Offset<T>> offsets) where T : allows ref struct
	{
		int bytes = offsets.Length * 4;
		Align(4, bytes + 4);
		for (int i = offsets.Length - 1; i >= 0; i--)
		{
			int slotPos = _space - 4;
			int v = offsets[i].Value - slotPos;
			if (v <= 0)
				throw new InvalidOperationException($"Offset data[{offsets[i].Value}] must be created before the vector slot at {slotPos}.");
			_space = slotPos;
			Unsafe.WriteUnaligned(ref _buf[_space], v);
		}
		PutUnaligned(offsets.Length);
		return new VectorOffset(_space);
	}

	void WriteRoot(int rootTablePos)
	{
		Align(_minAlign, 4);
		_space -= 4;
		int v = rootTablePos - _space;
		if (v <= 0)
			throw new InvalidOperationException($"Root table at {rootTablePos} must be created before Finish() writes the root offset at {_space}.");
		Unsafe.WriteUnaligned(ref _buf[_space], v);
	}

	void WriteRoot(int rootTablePos, ReadOnlySpan<byte> fileIdentifier)
	{
		if (fileIdentifier.Length != FlatBufferReader.FileIdentifierLength)
			throw new ArgumentException($"File identifier must be exactly {FlatBufferReader.FileIdentifierLength} bytes, got {fileIdentifier.Length}.", nameof(fileIdentifier));
		Align(_minAlign, 4 + FlatBufferReader.FileIdentifierLength);
		_space -= FlatBufferReader.FileIdentifierLength;
		fileIdentifier.CopyTo(_buf.Slice(_space, FlatBufferReader.FileIdentifierLength));
		_space -= 4;
		int v = rootTablePos - _space;
		if (v <= 0)
			throw new InvalidOperationException($"Root table at {rootTablePos} must be created before Finish() writes the root offset at {_space}.");
		Unsafe.WriteUnaligned(ref _buf[_space], v);
	}

}
