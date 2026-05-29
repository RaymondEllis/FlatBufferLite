using FlatBufferLite.SourceGen.IR;
using System.Text;

namespace FlatBufferLite.SourceGen.Emit;

public sealed partial class CodeEmitter
{
	static string ScalarCSharpType(TypeRef type, out string defaultLiteral)
	{
		defaultLiteral = "0";
		string cs = type.Base.ToCSharpKeyword();
		if (cs == "bool")
			defaultLiteral = "false";
		else if (cs == "float")
			defaultLiteral = "0f";
		else if (cs == "double")
			defaultLiteral = "0d";
		else if (cs == "long")
			defaultLiteral = "0L";
		else if (cs == "ulong")
			defaultLiteral = "0UL";
		return cs;
	}

	static string FormatDefault(TypeRef type, string raw)
	{
		if (type.Base == SchemaBaseType.Bool)
			return raw == "true" ? "true" : "false";
		if (type.Base == SchemaBaseType.Float)
		{
			if (raw == "inf" || raw == "infinity")
				return "float.PositiveInfinity";
			if (raw == "-inf" || raw == "-infinity")
				return "float.NegativeInfinity";
			if (raw == "nan")
				return "float.NaN";
			return raw + "f";
		}
		if (type.Base == SchemaBaseType.Double)
		{
			if (raw == "inf" || raw == "infinity")
				return "double.PositiveInfinity";
			if (raw == "-inf" || raw == "-infinity")
				return "double.NegativeInfinity";
			if (raw == "nan")
				return "double.NaN";
			return raw + "d";
		}
		if (type.Base == SchemaBaseType.Long)
			return raw + "L";
		if (type.Base == SchemaBaseType.ULong)
			return raw + "UL";
		return raw;
	}

	string ResolveTypeName(TypeRef type)
	{
		if (type.Base.IsScalar())
			return type.Base.ToCSharpKeyword();
		if (type.Base == SchemaBaseType.Obj && type.ReferencedName != null)
			return type.ReferencedName;
		return "object";
	}

	void EmitStructLayoutExplicit(int size)
	{
		_w.Append("[StructLayout(LayoutKind.Explicit, Size = ").Append(size).AppendLine(")]");
	}

	void EmitPublicField(string typeName, string fieldName)
	{
		_w.Append("public ").Append(typeName).Append(' ').Append(fieldName).EndStatement();
	}

	void EmitPublicReadonlyField(string typeName, string fieldName)
	{
		_w.Append("public readonly ").Append(typeName).Append(' ').Append(fieldName).EndStatement();
	}

	void EmitReadonlyField(string typeName, string fieldName)
	{
		_w.Append("readonly ").Append(typeName).Append(' ').Append(fieldName).EndStatement();
	}

	void EmitFieldOffsetField(int offset, string typeName, string fieldName)
	{
		EmitFieldOffsetField(offset, "public", typeName, fieldName);
	}

	void EmitFieldOffsetField(int offset, string modifiers, string typeName, string fieldName)
	{
		_w.Append("[FieldOffset(").Append(offset).Append(")] ").Append(modifiers).Append(' ').Append(typeName).Append(' ').Append(fieldName).EndStatement();
	}

	void EmitPublicConst(string typeName, string name, string value)
	{
		_w.Append("public const ").Append(typeName).Append(' ').Append(name).Append(" = ").Append(value).EndStatement();
	}

	void EmitVar(string name, string value)
	{
		_w.Append("var ").Append(name).Append(" = ").Append(value).EndStatement();
	}

	void EmitValueFieldAssignment(string fieldName, string value)
	{
		_w.Append("value.").Append(fieldName).Append(" = ").Append(value).EndStatement();
	}

	void OpenIf(string value, string condition)
	{
		_w.Append("if (").Append(value).Append(condition).AppendLine(")");
		_w.OpenBlock();
	}

	void OpenElseBlock()
	{
		_w.AppendLine("else");
		_w.OpenBlock();
	}

	void EmitIndirectNewProperty(string typeName, string propertyName, int vto)
	{
		EmitIndirectNewProperty(typeName, propertyName, typeName, vto);
	}

	void EmitIndirectNewProperty(string typeName, string propertyName, string newTypeName, int vto)
	{
		_w.Append("public ").Append(typeName).Append(' ').Append(propertyName).Append(" => new ").Append(newTypeName).Append("(_buf, Vtable.ReadIndirect(_buf, _pos, ").Append(vto).AppendLine("));");
	}

	void EmitIndirectNewPropertyGeneric(string typeName, string typeArg, string propertyName, int vto)
	{
		_w.Append("public ").Append(typeName).Append('<').Append(typeArg).Append("> ").Append(propertyName)
		.Append(" => new ").Append(typeName).Append('<').Append(typeArg).Append(">(_buf, Vtable.ReadIndirect(_buf, _pos, ").Append(vto).AppendLine("));");
	}

	void EmitStartTable(TableDef table)
	{
		_w.Append("int __pos = builder.StartTable(").Append(table.SlotCount).Append(", ").Append(table.InlineSize).Append(", ").Append(table.InlineAlign).AppendLine(");");
		_w.AppendLine("var __buf = builder.Buffer;");
	}

	void EmitMarkRootAndReturnTableRef(TableDef table)
	{
		_w.AppendLine("builder.MarkRoot(__pos);");
		EmitReturnTableRef(table, "__buf", "__pos");
	}

	void EmitReturnTableRef(TableDef table, string bufferExpression, string positionExpression)
	{
		_w.Append("return new ").Append(table.Name).Append("Ref(").Append(bufferExpression).Append(", ").Append(positionExpression).AppendLine(");");
	}

	void EmitWriteForced(string typeName, int vto, int absInline, string value)
	{
		_w.Append("Vtable.WriteForced<").Append(typeName).Append(">(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).Append(", ").Append(value).AppendLine(");");
	}

	void EmitWrite(string typeName, int vto, int absInline, string value, string defaultValue)
	{
		_w.Append("Vtable.Write<").Append(typeName).Append(">(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).Append(", ").Append(value).Append(", ").Append(defaultValue).AppendLine(");");
	}

	void EmitWriteOffset(int vto, int absInline, string value)
	{
		_w.Append("Vtable.WriteOffset(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).Append(", ").Append(value).AppendLine(");");
	}

	void EmitWriteOffsetIfPresent(int vto, int absInline, string value)
	{
		_w.Append("if (").Append(value).Append(" != 0) ");
		EmitWriteOffset(vto, absInline, value);
	}

	void EmitWriteStruct(string typeName, int vto, int absInline, string value)
	{
		_w.Append("{ var __v = ").Append(value).Append("; Vtable.WriteForced<").Append(typeName).Append(">(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).AppendLine(", in __v); }");
	}

	void EmitWriteDefaultStruct(string typeName, int vto, int absInline)
	{
		_w.Append("{ var __v = default(").Append(typeName).Append("); Vtable.WriteForced<").Append(typeName).Append(">(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).AppendLine(", in __v); }");
	}

	static string ToPascalCase(string s)
	{
		if (string.IsNullOrEmpty(s))
			return s;
		var sb = new StringBuilder(s.Length);
		bool upper = true;
		foreach (var c in s)
		{
			if (c == '_')
			{
				upper = true;
				continue;
			}
			sb.Append(upper ? char.ToUpperInvariant(c) : c);
			upper = false;
		}
		return sb.ToString();
	}

	static string ToCamelCase(string s)
	{
		string pascal = ToPascalCase(s);
		if (string.IsNullOrEmpty(pascal))
			return pascal;
		return char.ToLowerInvariant(pascal[0]) + pascal.Substring(1);
	}

	static int AlignUp(int value, int alignment)
	{
		if (alignment <= 1)
			return value;
		return (value + alignment - 1) & ~(alignment - 1);
	}

	static string FormatEnumDefault(EnumDef ed, string raw)
	{
		string under = ed.Underlying.ToCSharpKeyword();
		if (raw.Length > 0 && (char.IsDigit(raw[0]) || raw[0] == '-'))
			return "(" + under + ")" + raw;
		return "(" + under + ")" + ed.Name + "." + raw;
	}

	static bool IsValidCSharpIdentifier(string s)
	{
		if (string.IsNullOrEmpty(s))
			return false;
		if (s[0] == '.' || s[s.Length - 1] == '.')
			return false;
		for (int i = 0; i < s.Length; i++)
		{
			char c = s[i];
			if (c == '.')
				continue;
			if (c == '_')
				continue;
			if (i == 0 || (i > 0 && s[i - 1] == '.'))
			{
				if (!char.IsLetter(c) && c != '_')
					return false;
			}
			else if (!char.IsLetterOrDigit(c))
				return false;
		}
		return true;
	}
}
