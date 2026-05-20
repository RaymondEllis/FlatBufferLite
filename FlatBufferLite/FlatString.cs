using System.Runtime.CompilerServices;
using System.Text;

namespace FlatBufferLite;

public readonly ref struct FlatString
{
	public readonly ReadOnlySpan<byte> Buffer;
	public readonly int Position;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FlatString(ReadOnlySpan<byte> buffer, int position)
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

	public ReadOnlySpan<byte> AsBytes
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Position <= 0 ? ReadOnlySpan<byte>.Empty : Buffer.Slice(Position + 4, (int)FlatBufferReader.ReadUnaligned<uint>(Buffer, Position));
	}

	public override string ToString() => Position <= 0 ? string.Empty : Encoding.UTF8.GetString(AsBytes);
}