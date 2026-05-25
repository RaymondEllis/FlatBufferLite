using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlatBufferLite;

public readonly ref struct FlatVector<T> where T : unmanaged
{
	public readonly ReadOnlySpan<byte> Buffer;
	public readonly int Position;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FlatVector(ReadOnlySpan<byte> buffer, int position)
	{
		Buffer = buffer;
		Position = position;
	}

	public bool IsValid
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Position > 0;
	}

	public int Length
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Position <= 0 ? 0 : (int)FlatBufferReader.ReadUnaligned<uint>(Buffer, Position);
	}

	public ReadOnlySpan<T> AsSpan
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			if (Position <= 0)
				return ReadOnlySpan<T>.Empty;
			int len = (int)FlatBufferReader.ReadUnaligned<uint>(Buffer, Position);
			int byteLen = checked(len * Unsafe.SizeOf<T>());
			return MemoryMarshal.Cast<byte, T>(Buffer.Slice(Position + 4, byteLen));
		}
	}

	public T this[int index]
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			Debug.Assert(index >= 0);
			Debug.Assert((uint)index < (uint)Length);
			return FlatBufferReader.ReadUnaligned<T>(Buffer, Position + 4 + index * Unsafe.SizeOf<T>());
		}
	}
}

public readonly ref struct FlatStringVector
{
	public readonly ReadOnlySpan<byte> Buffer;
	public readonly int Position;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FlatStringVector(ReadOnlySpan<byte> buffer, int position)
	{
		Buffer = buffer;
		Position = position;
	}

	public bool IsValid
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Position > 0;
	}

	public int Length
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Position <= 0 ? 0 : (int)FlatBufferReader.ReadUnaligned<uint>(Buffer, Position);
	}

	public FlatString this[int index]
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			Debug.Assert(index >= 0);
			Debug.Assert((uint)index < (uint)Length);
			int eltOff = Position + 4 + index * 4;
			int strOff = eltOff + (int)FlatBufferReader.ReadUnaligned<uint>(Buffer, eltOff);
			return new FlatString(Buffer, strOff);
		}
	}
}