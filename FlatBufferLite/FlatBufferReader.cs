using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlatBufferLite;

public static class FlatBufferReader
{
	public const int FileIdentifierLength = 4;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ReadUnaligned<T>(ReadOnlySpan<byte> buffer, int offset) where T : unmanaged
	{
		if ((uint)offset + (uint)Unsafe.SizeOf<T>() > (uint)buffer.Length)
			throw new ArgumentOutOfRangeException(nameof(offset), offset, $"Read of {Unsafe.SizeOf<T>()} bytes at offset {offset} exceeds buffer length {buffer.Length}.");
		return Unsafe.ReadUnaligned<T>(ref Unsafe.AsRef(in buffer[offset]));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int GetRootOffset(ReadOnlySpan<byte> buffer) => (int)MemoryMarshal.Read<uint>(buffer);

	public static bool HasIdentifier(ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> identifier)
	{
		if (identifier.Length != FileIdentifierLength)
			return false;
		if (buffer.Length < 4 + FileIdentifierLength)
			return false;
		return buffer.Slice(4, FileIdentifierLength).SequenceEqual(identifier);
	}

}
