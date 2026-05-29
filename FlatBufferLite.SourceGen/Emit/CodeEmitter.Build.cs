using FlatBufferLite.SourceGen.IR;

namespace FlatBufferLite.SourceGen.Emit;

public sealed partial class CodeEmitter
{
	void EmitReserveConstructor(TableDef t)
	{
		_w.Append("public static ").Append(t.Name).Append("Ref").AppendLine(" Create(ref FlatBufferBuilder builder)");
		_w.OpenBlock();
		EmitStartTable(t);
		foreach (var f in t.Fields)
		{
			if (f.Deprecated)
				continue;
			EmitFieldAssign(f, forced: true);
		}
		EmitMarkRootAndReturnTableRef(t);
		_w.CloseBlock();
	}

	void EmitFieldAssign(FieldDef f, bool forced)
	{
		int absInline = f.InlineOffset + 4;
		int vto = f.VTableOffset;
		string pname = ToCamelCase(f.Name);
		if (f.Type.IsUnion)
		{
			if (forced)
			{
				EmitWriteForced("byte", vto, absInline, "0");
			}
			else
			{
				int dataVto = vto + 2;
				int dataAbsInline = f.UnionDataInlineOffset + 4;
				EmitWrite("byte", vto, absInline, "(byte)" + pname + "Type", "0");
				_w.AppendLine();
				EmitWriteOffsetIfPresent(dataVto, dataAbsInline, pname);
			}
			return;
		}
		if (f.Type.Base.IsScalar())
		{
			string cs = ScalarCSharpType(f.Type, out string defLit);
			string schemaDefault = f.DefaultValue is { Length: > 0 } defaultValue ? FormatDefault(f.Type, defaultValue) : defLit;
			if (forced)
				EmitWriteForced(cs, vto, absInline, schemaDefault);
			else
				EmitWrite(cs, vto, absInline, pname, schemaDefault);
			return;
		}
		if (f.Type.IsString || f.Type.IsVector)
		{
			if (!forced)
			{
				if (f.Required)
					EmitWriteOffset(vto, absInline, pname);
				else
					EmitWriteOffsetIfPresent(vto, absInline, pname);
			}
			return;
		}
		if (f.Type.IsObject && f.Type.ReferencedName != null && _schema.ByName.TryGetValue(f.Type.ReferencedName, out var def))
		{
			if (def is TableDef)
			{
				if (!forced)
				{
					if (f.Required)
						EmitWriteOffset(vto, absInline, pname);
					else
						EmitWriteOffsetIfPresent(vto, absInline, pname);
				}
				return;
			}
			if (def is StructDef)
			{
				if (forced)
					EmitWriteDefaultStruct(f.Type.ReferencedName, vto, absInline);
				else
					EmitWriteStruct(f.Type.ReferencedName, vto, absInline, pname);
				return;
			}
			if (def is EnumDef ed)
			{
				string under = ed.Underlying.ToCSharpKeyword();
				string defValue = f.DefaultValue is { Length: > 0 } defaultValue
				? FormatEnumDefault(ed, defaultValue)
				: (ed.Underlying == SchemaBaseType.Long || ed.Underlying == SchemaBaseType.ULong ? "0L" : "0");
				if (forced)
					EmitWriteForced(under, vto, absInline, defValue);
				else
					EmitWrite(under, vto, absInline, "(" + under + ")" + pname, defValue);
				return;
			}
		}
		else if (f.Type.IsObject && f.Type.ReferencedName != null)
		{
			if (forced)
				EmitWriteDefaultStruct(f.Type.ReferencedName, vto, absInline);
			else
				EmitWriteStruct(f.Type.ReferencedName, vto, absInline, pname);
		}
	}

	void EmitBuildConstructor(TableDef t)
	{
		_w.Append("public static ").Append(t.Name).Append("Ref").Append(" Create(ref FlatBufferBuilder builder");
		foreach (var f in t.Fields)
		{
			if (f.Deprecated)
				continue;
			if (f.Type.IsUnion)
			{
				if (f.Type.ReferencedName == null)
					continue;
				_w.Append(", ").Append(f.Type.ReferencedName).Append("Kind ").Append(ToCamelCase(f.Name)).Append("Type = default");
				_w.Append(", int ").Append(ToCamelCase(f.Name)).Append(" = 0");
				continue;
			}
			_w.Append(", ").Append(BuildParamType(f)).Append(' ').Append(ToCamelCase(f.Name)).Append(" = ").Append(BuildParamDefault(f));
		}
		_w.AppendLine(")");
		_w.OpenBlock();
		EmitStartTable(t);
		foreach (var f in t.Fields)
		{
			if (f.Deprecated)
				continue;
			EmitFieldAssign(f, forced: false);
		}
		EmitMarkRootAndReturnTableRef(t);
		_w.CloseBlock();
	}

	string BuildParamType(FieldDef f)
	{
		if (f.Type.Base.IsScalar())
			return ScalarCSharpType(f.Type, out _);
		if (f.Type.IsString)
			return "StringOffset";
		if (f.Type.IsVector)
			return "VectorOffset";
		if (f.Type.IsObject && f.Type.ReferencedName != null && _schema.ByName.TryGetValue(f.Type.ReferencedName, out var def))
		{
			if (def is TableDef)
				return "Offset<" + f.Type.ReferencedName + "Ref>";
			if (def is StructDef)
				return f.Type.ReferencedName;
			if (def is EnumDef)
				return f.Type.ReferencedName;
		}
		return f.Type.ReferencedName ?? "int";
	}

	string BuildParamDefault(FieldDef f)
	{
		if (f.Type.Base.IsScalar())
		{
			ScalarCSharpType(f.Type, out string defLit);
			return f.DefaultValue is { Length: > 0 } defaultValue ? FormatDefault(f.Type, defaultValue) : defLit;
		}
		if (f.Type.IsString || f.Type.IsVector)
			return "default";
		if (f.Type.IsObject && f.Type.ReferencedName != null && _schema.ByName.TryGetValue(f.Type.ReferencedName, out var def))
		{
			if (def is TableDef)
				return "default";
			if (def is StructDef)
				return "default";
			if (def is EnumDef ed2)
			{
				if (f.DefaultValue is { Length: > 0 } defaultValue)
				{
					if (char.IsDigit(defaultValue[0]) || defaultValue[0] == '-')
						return "(" + ed2.Name + ")" + defaultValue;
					return ed2.Name + "." + defaultValue;
				}
				return "default";
			}
		}
		return f.Type.IsObject && f.Type.ReferencedName != null ? "default" : "0";
	}
}
