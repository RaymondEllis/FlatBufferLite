namespace FlatBufferLite.SourceGen.IR;

public enum SchemaBaseType : byte
{
	None = 0,
	UType,
	Bool,
	Byte,
	UByte,
	Short,
	UShort,
	Int,
	UInt,
	Long,
	ULong,
	Float,
	Double,
	String,
	Vector,
	Obj,
	Union,
}

public static class SchemaBaseTypeExtensions
{
	// For Union, returns only the uoffset data portion (4 bytes). The 1-byte type tag is handled
	// separately by the layout engine (AssignTableLayout) which allocates tag + padding + 4 bytes.
	public static int InlineSize(this SchemaBaseType type) => type switch
	{
		SchemaBaseType.Bool or SchemaBaseType.Byte or SchemaBaseType.UByte or SchemaBaseType.UType => 1,
		SchemaBaseType.Short or SchemaBaseType.UShort => 2,
		SchemaBaseType.Int or SchemaBaseType.UInt or SchemaBaseType.Float => 4,
		SchemaBaseType.Long or SchemaBaseType.ULong or SchemaBaseType.Double => 8,
		SchemaBaseType.String or SchemaBaseType.Vector or SchemaBaseType.Obj or SchemaBaseType.Union => 4,
		_ => 0,
	};

	public static bool IsScalar(this SchemaBaseType type)
		=> type >= SchemaBaseType.UType && type <= SchemaBaseType.Double;

	public static string ToCSharpKeyword(this SchemaBaseType type) => type switch
	{
		SchemaBaseType.Bool => "bool",
		SchemaBaseType.Byte => "sbyte",
		SchemaBaseType.UByte or SchemaBaseType.UType => "byte",
		SchemaBaseType.Short => "short",
		SchemaBaseType.UShort => "ushort",
		SchemaBaseType.Int => "int",
		SchemaBaseType.UInt => "uint",
		SchemaBaseType.Long => "long",
		SchemaBaseType.ULong => "ulong",
		SchemaBaseType.Float => "float",
		SchemaBaseType.Double => "double",
		_ => throw new System.InvalidOperationException($"Type {type} has no C# keyword."),
	};
}