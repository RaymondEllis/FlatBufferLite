using FlatBufferLite.SourceGen.IR;

namespace FlatBufferLite.SourceGen.Emit;

public sealed partial class CodeEmitter
{
	void EmitReserveConstructor(TableDef t)
	{
		_sb.Append("\tpublic static ").Append(t.Name).Append("Ref").AppendLine(" Create(ref FlatBufferBuilder builder)");
		_sb.AppendLine("\t{");
		_sb.Append("\t\tint __pos = builder.StartTable(").Append(t.SlotCount).Append(", ").Append(t.InlineSize).Append(", ").Append(t.InlineAlign).AppendLine(");");
		_sb.AppendLine("\t\tvar __buf = builder.Buffer;");
		foreach (var f in t.Fields)
		{
			if (f.Deprecated)
				continue;
			EmitFieldAssign(f, forced: true);
		}
		_sb.AppendLine("\t\tbuilder.MarkRoot(__pos);");
		_sb.Append("\t\treturn new ").Append(t.Name).Append("Ref").AppendLine("(__buf, __pos);");
		_sb.AppendLine("\t}");
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
				_sb.Append("\t\tVtable.WriteForced<byte>(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).AppendLine(", 0);");
			}
			else
			{
				int dataVto = vto + 2;
				int dataAbsInline = f.UnionDataInlineOffset + 4;
				_sb.Append("\t\tVtable.Write<byte>(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).Append(", (byte)").Append(pname).Append("Type, 0);");
				_sb.AppendLine();
				_sb.Append("\t\tif (").Append(pname).Append(" != 0) Vtable.WriteOffset(__buf, __pos, ").Append(dataVto).Append(", ").Append(dataAbsInline).Append(", ").Append(pname).AppendLine(");");
			}
			return;
		}
		if (f.Type.Base.IsScalar())
		{
			string cs = ScalarCSharpType(f.Type, out string defLit);
			string schemaDefault = !string.IsNullOrEmpty(f.DefaultValue) ? FormatDefault(f.Type, f.DefaultValue!) : defLit;
			if (forced)
				_sb.Append("\t\tVtable.WriteForced<").Append(cs).Append(">(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).Append(", ").Append(schemaDefault).AppendLine(");");
			else
				_sb.Append("\t\tVtable.Write<").Append(cs).Append(">(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).Append(", ").Append(pname).Append(", ").Append(schemaDefault).AppendLine(");");
			return;
		}
		if (f.Type.IsString || f.Type.IsVector)
		{
			if (!forced)
			{
				if (f.Required)
					_sb.Append("\t\tVtable.WriteOffset(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).Append(", ").Append(pname).AppendLine(");");
				else
					_sb.Append("\t\tif (").Append(pname).Append(" != 0) Vtable.WriteOffset(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).Append(", ").Append(pname).AppendLine(");");
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
						_sb.Append("\t\tVtable.WriteOffset(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).Append(", ").Append(pname).AppendLine(");");
					else
						_sb.Append("\t\tif (").Append(pname).Append(" != 0) Vtable.WriteOffset(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).Append(", ").Append(pname).AppendLine(");");
				}
				return;
			}
			if (def is StructDef)
			{
				if (forced)
					_sb.Append("\t\t{ var __v = default(").Append(f.Type.ReferencedName).Append("); Vtable.WriteForced<").Append(f.Type.ReferencedName).Append(">(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).AppendLine(", in __v); }");
				else
					_sb.Append("\t\t{ var __v = ").Append(pname).Append("; Vtable.WriteForced<").Append(f.Type.ReferencedName).Append(">(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).AppendLine(", in __v); }");
				return;
			}
			if (def is EnumDef ed)
			{
				string under = ed.Underlying.ToCSharpKeyword();
				string defValue = !string.IsNullOrEmpty(f.DefaultValue)
					? FormatEnumDefault(ed, f.DefaultValue!)
					: (ed.Underlying == SchemaBaseType.Long || ed.Underlying == SchemaBaseType.ULong ? "0L" : "0");
				if (forced)
					_sb.Append("\t\tVtable.WriteForced<").Append(under).Append(">(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).Append(", ").Append(defValue).AppendLine(");");
				else
					_sb.Append("\t\tVtable.Write<").Append(under).Append(">(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).Append(", (").Append(under).Append(')').Append(pname).Append(", ").Append(defValue).AppendLine(");");
				return;
			}
		}
		else if (f.Type.IsObject && f.Type.ReferencedName != null)
		{
			if (forced)
				_sb.Append("\t\t{ var __v = default(").Append(f.Type.ReferencedName).Append("); Vtable.WriteForced<").Append(f.Type.ReferencedName).Append(">(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).AppendLine(", in __v); }");
			else
				_sb.Append("\t\t{ var __v = ").Append(pname).Append("; Vtable.WriteForced<").Append(f.Type.ReferencedName).Append(">(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).AppendLine(", in __v); }");
		}
	}

	void EmitBuildConstructor(TableDef t)
	{
		_sb.Append("\tpublic static ").Append(t.Name).Append("Ref").Append(" Create(ref FlatBufferBuilder builder");
		foreach (var f in t.Fields)
		{
			if (f.Deprecated)
				continue;
			if (f.Type.IsUnion)
			{
				_sb.Append(", ").Append(f.Type.ReferencedName).Append("Kind ").Append(ToCamelCase(f.Name)).Append("Type = default");
				_sb.Append(", int ").Append(ToCamelCase(f.Name)).Append(" = 0");
				continue;
			}
			_sb.Append(", ").Append(BuildParamType(f)).Append(' ').Append(ToCamelCase(f.Name)).Append(" = ").Append(BuildParamDefault(f));
		}
		_sb.AppendLine(")");
		_sb.AppendLine("\t{");
		_sb.Append("\t\tint __pos = builder.StartTable(").Append(t.SlotCount).Append(", ").Append(t.InlineSize).Append(", ").Append(t.InlineAlign).AppendLine(");");
		_sb.AppendLine("\t\tvar __buf = builder.Buffer;");
		foreach (var f in t.Fields)
		{
			if (f.Deprecated)
				continue;
			EmitFieldAssign(f, forced: false);
		}
		_sb.AppendLine("\t\tbuilder.MarkRoot(__pos);");
		_sb.Append("\t\treturn new ").Append(t.Name).Append("Ref").AppendLine("(__buf, __pos);");
		_sb.AppendLine("\t}");
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
			return !string.IsNullOrEmpty(f.DefaultValue) ? FormatDefault(f.Type, f.DefaultValue!) : defLit;
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
				if (!string.IsNullOrEmpty(f.DefaultValue))
				{
					if (f.DefaultValue!.Length > 0 && (char.IsDigit(f.DefaultValue[0]) || f.DefaultValue[0] == '-'))
						return "(" + ed2.Name + ")" + f.DefaultValue;
					return ed2.Name + "." + f.DefaultValue;
				}
				return "default";
			}
		}
		return f.Type.IsObject && f.Type.ReferencedName != null ? "default" : "0";
	}
}
