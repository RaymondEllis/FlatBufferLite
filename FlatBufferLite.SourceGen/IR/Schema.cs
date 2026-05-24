using System.Collections.Generic;

namespace FlatBufferLite.SourceGen.IR;

public struct TypeRef
{
	public SchemaBaseType Base;
	public SchemaBaseType ElementBase;
	public string? ReferencedName;

	public readonly bool IsVector => Base == SchemaBaseType.Vector;
	public readonly bool IsString => Base == SchemaBaseType.String;
	public readonly bool IsObject => Base == SchemaBaseType.Obj;
	public readonly bool IsUnion => Base == SchemaBaseType.Union;
}

public interface ISchemaDef { }

public sealed class FieldDef
{
	public string Name = "";
	public TypeRef Type;
	public string? DefaultValue;
	public bool Deprecated;
	public int VTableOffset;
	public int InlineOffset;
	public int UnionDataInlineOffset;
}

public sealed class TableDef : ISchemaDef
{
	public string Name = "";
	public List<FieldDef> Fields = new();
	public int InlineSize;
	public int InlineAlign;
	public int SlotCount;
}

public sealed class StructFieldDef
{
	public string Name = "";
	public TypeRef Type;
	public int Offset;
	public int Size;
}

public sealed class StructDef : ISchemaDef
{
	public string Name = "";
	public List<StructFieldDef> Fields = new();
	public int Size;
	public int Alignment;
}

public sealed class EnumValueDef
{
	public string Name = "";
	public long Value;
}

public sealed class EnumDef : ISchemaDef
{
	public string Name = "";
	public SchemaBaseType Underlying = SchemaBaseType.Int;
	public List<EnumValueDef> Values = new();
	public bool IsBitFlags;
}

public sealed class UnionMember
{
	public string Name = "";
	public string TypeName = "";
	public byte Tag;
}

public sealed class UnionDef : ISchemaDef
{
	public string Name = "";
	public List<UnionMember> Members = new();
}

public sealed class Schema
{
	public List<string> RootTypes = new();
	public string? RootTable => RootTypes.Count > 0 ? RootTypes[0] : null;
	public string? FileIdentifier;
	public string? FileExtension;
	public string? Namespace;
	public List<string> Includes = new();
	public List<TableDef> Tables = new();
	public List<StructDef> Structs = new();
	public List<EnumDef> Enums = new();
	public List<UnionDef> Unions = new();
	public int LocalTableCount;
	public int LocalStructCount;
	public int LocalEnumCount;
	public int LocalUnionCount;

	public Dictionary<string, ISchemaDef> ByName = new();

	public void AddRootType(string name)
	{
		if (!RootTypes.Contains(name))
			RootTypes.Add(name);
	}

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

	public void MarkLocalCounts()
	{
		LocalTableCount = Tables.Count;
		LocalStructCount = Structs.Count;
		LocalEnumCount = Enums.Count;
		LocalUnionCount = Unions.Count;
	}

	public void MergeFrom(Schema other)
	{
		var existing = new HashSet<string>();
		foreach (var t in Tables) existing.Add(t.Name);
		foreach (var s in Structs) existing.Add(s.Name);
		foreach (var e in Enums) existing.Add(e.Name);
		foreach (var u in Unions) existing.Add(u.Name);

		foreach (var t in other.Tables)
			if (existing.Add(t.Name))
				Tables.Add(t);
		foreach (var s in other.Structs)
			if (existing.Add(s.Name))
				Structs.Add(s);
		foreach (var e in other.Enums)
			if (existing.Add(e.Name))
				Enums.Add(e);
		foreach (var u in other.Unions)
			if (existing.Add(u.Name))
				Unions.Add(u);
	}
}