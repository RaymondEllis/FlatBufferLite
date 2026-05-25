using System.Runtime.CompilerServices;

namespace FlatBufferLite;

public static class FlatBufferReader
{
	public const int FileIdentifierLength = 4;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ReadUnaligned<T>(ReadOnlySpan<byte> buffer, int offset) where T : unmanaged
	{
		if ((uint)offset + (uint)Unsafe.SizeOf<T>() > (uint)buffer.Length)
			ThrowOutOfRange();
		return Unsafe.ReadUnaligned<T>(ref Unsafe.AsRef(in buffer[offset]));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int GetRootOffset(ReadOnlySpan<byte> buffer) => (int)ReadUnaligned<uint>(buffer, 0);

	public static bool HasIdentifier(ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> identifier)
	{
		if (identifier.Length != FileIdentifierLength)
			return false;
		if (buffer.Length < 4 + FileIdentifierLength)
			return false;
		return buffer.Slice(4, FileIdentifierLength).SequenceEqual(identifier);
	}

	internal static void ThrowOutOfRange() => throw new IndexOutOfRangeException("FlatBuffer read outside buffer bounds.");
}