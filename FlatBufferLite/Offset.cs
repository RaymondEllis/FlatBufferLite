namespace FlatBufferLite;

public readonly struct StringOffset
{
	public readonly int Value;
	public StringOffset(int value) => Value = value;
	public static implicit operator int(StringOffset o) => o.Value;
}

public readonly struct VectorOffset
{
	public readonly int Value;
	public VectorOffset(int value) => Value = value;
	public static implicit operator int(VectorOffset o) => o.Value;
}

public readonly struct Offset<T> where T : allows ref struct
{
	public readonly int Value;
	public Offset(int value) => Value = value;
	public static implicit operator int(Offset<T> o) => o.Value;
}
