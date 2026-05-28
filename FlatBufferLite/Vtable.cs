using System.Runtime.CompilerServices;

namespace FlatBufferLite;

public static class Vtable
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int Offset(ReadOnlySpan<byte> buffer, int tablePos, int vtableField)
	{
		int vt = tablePos - FlatBufferReader.ReadUnaligned<int>(buffer, tablePos);
		int vtSize = FlatBufferReader.ReadUnaligned<ushort>(buffer, vt);
		return vtableField < vtSize ? FlatBufferReader.ReadUnaligned<ushort>(buffer, vt + vtableField) : 0;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool HasField(ReadOnlySpan<byte> buffer, int tablePos, int vtableField)
		=> Offset(buffer, tablePos, vtableField) != 0;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T Read<T>(ReadOnlySpan<byte> buffer, int tablePos, int vtableField, T def) where T : unmanaged
	{
		int o = Offset(buffer, tablePos, vtableField);
		return o == 0 ? def : FlatBufferReader.ReadUnaligned<T>(buffer, tablePos + o);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int ReadIndirect(ReadOnlySpan<byte> buffer, int tablePos, int vtableField)
	{
		int o = Offset(buffer, tablePos, vtableField);
		if (o == 0)
			return 0;
		int abs = tablePos + o;
		return abs + (int)FlatBufferReader.ReadUnaligned<uint>(buffer, abs);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int StructOffset(ReadOnlySpan<byte> buffer, int tablePos, int vtableField)
	{
		int o = Offset(buffer, tablePos, vtableField);
		return o == 0 ? 0 : tablePos + o;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Write<T>(Span<byte> buffer, int tablePos, int vtableField, int inlineOffset, T value, T def)
		where T : unmanaged, IEquatable<T>
	{
		int vt = tablePos - FlatBufferReader.ReadUnaligned<int>(buffer, tablePos);
		ushort slot = FlatBufferReader.ReadUnaligned<ushort>(buffer, vt + vtableField);
		if (slot == 0 && value.Equals(def))
			return;
		Unsafe.WriteUnaligned(ref buffer[vt + vtableField], (ushort)inlineOffset);
		Unsafe.WriteUnaligned(ref buffer[tablePos + inlineOffset], value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void WriteForced<T>(Span<byte> buffer, int tablePos, int vtableField, int inlineOffset, in T value)
		where T : unmanaged
	{
		int vt = tablePos - FlatBufferReader.ReadUnaligned<int>(buffer, tablePos);
		Unsafe.WriteUnaligned(ref buffer[vt + vtableField], (ushort)inlineOffset);
		Unsafe.WriteUnaligned(ref buffer[tablePos + inlineOffset], value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void WriteOffset(Span<byte> buffer, int tablePos, int vtableField, int inlineOffset, int dataAbsPos)
	{
		int vt = tablePos - FlatBufferReader.ReadUnaligned<int>(buffer, tablePos);
		ushort slot = FlatBufferReader.ReadUnaligned<ushort>(buffer, vt + vtableField);
		if (slot != 0)
			throw new InvalidOperationException($"Variable-size field at vtable slot {vtableField} cannot be set more than once.");
		int slotPos = tablePos + inlineOffset;
		int v = dataAbsPos - slotPos;
		if (v <= 0)
			throw new InvalidOperationException($"Data at {dataAbsPos} must be created before the table field at {slotPos}.");
		Unsafe.WriteUnaligned(ref buffer[vt + vtableField], (ushort)inlineOffset);
		Unsafe.WriteUnaligned(ref buffer[slotPos], v);
	}

}
