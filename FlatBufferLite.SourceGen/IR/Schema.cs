using System.Collections.Generic;

namespace FlatBufferLite.SourceGen.IR;

internal struct TypeRef
{
	public SchemaBaseType Base;
	public SchemaBaseType ElementBase;
	public string? ReferencedName;

	public readonly bool IsVector => Base == SchemaBaseType.Vector;
	public readonly bool IsString => Base == SchemaBaseType.String;
	public readonly bool IsObject => Base == SchemaBaseType.Obj;
	public readonly bool IsUnion => Base == SchemaBaseType.Union;
}

internal interface ISchemaDef { }

internal sealed class FieldDef
{
	public string Name = "";
	public TypeRef Type;
	public string? DefaultValue;
	public bool Deprecated;
	public int VTableOffset;
	public int InlineOffset;
	public int UnionDataInlineOffset;
}

internal sealed class TableDef : ISchemaDef
{
	public string Name = "";
	public List<FieldDef> Fields = new();
	public int InlineSize;
	public int InlineAlign;
	public int SlotCount;
}

internal sealed class StructFieldDef
{
	public string Name = "";
	public TypeRef Type;
	public int Offset;
	public int Size;
}

internal sealed class StructDef : ISchemaDef
{
	public string Name = "";
	public List<StructFieldDef> Fields = new();
	public int Size;
	public int Alignment;
}

internal sealed class EnumValueDef
{
	public string Name = "";
	public long Value;
}

internal sealed class EnumDef : ISchemaDef
{
	public string Name = "";
	public SchemaBaseType Underlying = SchemaBaseType.Int;
	public List<EnumValueDef> Values = new();
	public bool IsBitFlags;
}

internal sealed class UnionMember
{
	public string Name = "";
	public string TypeName = "";
	public byte Tag;
}

internal sealed class UnionDef : ISchemaDef
{
	public string Name = "";
	public List<UnionMember> Members = new();
}

internal sealed class Schema
{
	public string? RootTable;
	public string? FileIdentifier;
	public string? FileExtension;
	public string? Namespace;
	public List<TableDef> Tables = new();
	public List<StructDef> Structs = new();
	public List<EnumDef> Enums = new();
	public List<UnionDef> Unions = new();

	public Dictionary<string, ISchemaDef> ByName = new();

	public void Index()
	{
		ByName.Clear();
		foreach (var t in Tables)
			ByName[t.Name] = t;
		foreach (var s in Structs)
			ByName[s.Name] = s;
		foreach (var e in Enums)
			ByName[e.Name] = e;
		foreach (var u in Unions)
			ByName[u.Name] = u;
	}
}