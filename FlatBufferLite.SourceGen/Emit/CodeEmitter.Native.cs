using FlatBufferLite.SourceGen.IR;

namespace FlatBufferLite.SourceGen.Emit;

public sealed partial class CodeEmitter
{
	static string NativeName(TableDef table) => table.Name + "Native";

	void EmitNativeStruct(TableDef table)
	{
		_sb.AppendLine();
		_sb.Append("public partial struct ").AppendLine(NativeName(table));
		_sb.AppendLine("{");
		foreach (var field in table.Fields)
		{
			if (field.Deprecated)
				continue;
			var typeName = NativeFieldType(field.Type);
			if (typeName == null)
				continue;
			_sb.Append("\tpublic ").Append(typeName).Append(' ').Append(ToPascalCase(field.Name)).AppendLine(";");
		}

		_sb.AppendLine();
		_sb.Append("\tpublic static ").Append(NativeName(table)).Append(" Deserialize(Span<byte> buffer) => ").Append(table.Name).AppendLine(".GetRootAs(buffer).ToNative();");
		_sb.AppendLine();
		_sb.Append("\tpublic static ").Append(table.Name).Append(" Serialize(ref FlatBufferBuilder builder, in ").Append(NativeName(table)).AppendLine(" value)");
		_sb.AppendLine("\t{");
		EmitNativeCreateLocals(table, "\t\t");
		_sb.Append("\t\treturn ").Append(table.Name).Append(".Create(ref builder");
		foreach (var field in table.Fields)
		{
			if (field.Deprecated || NativeFieldType(field.Type) == null)
				continue;
			_sb.Append(", ").Append(ToCamelCase(field.Name)).Append(": ").Append(NativeCreateArgument(field));
		}
		_sb.AppendLine(");");
		_sb.AppendLine("\t}");
		_sb.AppendLine("}");
	}

	void EmitNativeTableMethods(TableDef table)
	{
		_sb.AppendLine();
		_sb.Append("\tpublic ").Append(NativeName(table)).AppendLine(" ToNative()");
		_sb.AppendLine("\t{");
		_sb.Append("\t\tvar __value = new ").Append(NativeName(table)).AppendLine("();");
		foreach (var field in table.Fields)
		{
			if (field.Deprecated || NativeFieldType(field.Type) == null)
				continue;
			EmitNativeReadAssign(field, "\t\t");
		}
		_sb.AppendLine("\t\treturn __value;");
		_sb.AppendLine("\t}");
	}

	string? NativeFieldType(TypeRef type)
	{
		if (type.Base.IsScalar())
			return ScalarCSharpType(type, out _);
		if (type.IsString)
			return "byte[]?";
		if (type.IsUnion)
			return null;
		if (type.IsObject && type.ReferencedName != null && _schema.ByName.TryGetValue(type.ReferencedName, out var def))
		{
			if (def is TableDef table)
				return table.NativeStruct ? NativeName(table) + "?" : null;
			if (def is StructDef || def is EnumDef)
				return type.ReferencedName;
		}
		if (type.IsVector)
			return NativeVectorFieldType(type);
		return null;
	}

	string? NativeVectorFieldType(TypeRef type)
	{
		var elementType = NativeVectorElementType(type);
		return elementType == null ? null : elementType + "[]?";
	}

	string? NativeVectorElementType(TypeRef type)
	{
		if (type.ElementBase.IsScalar())
			return type.ElementBase.ToCSharpKeyword();
		if (type.ElementBase == SchemaBaseType.String)
			return "byte[]";
		if (type.ElementBase == SchemaBaseType.Obj && type.ReferencedName != null && _schema.ByName.TryGetValue(type.ReferencedName, out var def))
		{
			if (def is TableDef table)
				return table.NativeStruct ? NativeName(table) : null;
			if (def is StructDef || def is EnumDef)
				return type.ReferencedName;
		}
		return null;
	}

	void EmitNativeReadAssign(FieldDef field, string indent)
	{
		string fieldName = ToPascalCase(field.Name);
		string local = "__" + fieldName;
		if (field.Type.Base.IsScalar() || (field.Type.IsObject && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var objectDef) && (objectDef is StructDef || objectDef is EnumDef)))
		{
			_sb.Append(indent).Append("__value.").Append(fieldName).Append(" = ").Append(fieldName).AppendLine(";");
			return;
		}
		if (field.Type.IsString)
		{
			_sb.Append(indent).Append("__value.").Append(fieldName).Append(" = ").Append(fieldName).Append(".IsValid ? ").Append(fieldName).AppendLine(".AsBytes.ToArray() : null;");
			return;
		}
		if (field.Type.IsObject && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var def) && def is TableDef table && table.NativeStruct)
		{
			_sb.Append(indent).Append("var ").Append(local).Append(" = ").Append(fieldName).AppendLine(";");
			_sb.Append(indent).Append("__value.").Append(fieldName).Append(" = ").Append(local).Append(".IsValid ? ").Append(local).AppendLine(".ToNative() : null;");
			return;
		}
		if (field.Type.IsVector)
			EmitNativeVectorReadAssign(field, indent);
	}

	void EmitNativeVectorReadAssign(FieldDef field, string indent)
	{
		string fieldName = ToPascalCase(field.Name);
		string local = "__" + fieldName;
		_sb.Append(indent).Append("var ").Append(local).Append(" = ").Append(fieldName).AppendLine(";");
		_sb.Append(indent).Append("if (").Append(local).AppendLine(".IsValid)");
		_sb.Append(indent).AppendLine("{");
		if (field.Type.ElementBase.IsScalar() || (field.Type.ElementBase == SchemaBaseType.Obj && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var def) && def is StructDef))
		{
			_sb.Append(indent).Append("\t__value.").Append(fieldName).Append(" = ").Append(local).AppendLine(".AsSpan.ToArray();");
		}
		else if (field.Type.ElementBase == SchemaBaseType.String)
		{
			_sb.Append(indent).Append("\tvar __items = new byte[").Append(local).AppendLine(".Length][];");
			_sb.Append(indent).Append("\tfor (int i = 0; i < __items.Length; i++) __items[i] = ").Append(local).AppendLine("[i].AsBytes.ToArray();");
			_sb.Append(indent).Append("\t__value.").Append(fieldName).AppendLine(" = __items;");
		}
		else if (field.Type.ElementBase == SchemaBaseType.Obj && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var elementDef))
		{
			if (elementDef is EnumDef enumDef)
			{
				_sb.Append(indent).Append("\tvar __items = new ").Append(field.Type.ReferencedName).Append('[').Append(local).AppendLine(".Length];");
				_sb.Append(indent).Append("\tfor (int i = 0; i < __items.Length; i++) __items[i] = (").Append(field.Type.ReferencedName).Append(')').Append(local).AppendLine("[i];");
				_sb.Append(indent).Append("\t__value.").Append(fieldName).AppendLine(" = __items;");
			}
			else if (elementDef is TableDef table && table.NativeStruct)
			{
				_sb.Append(indent).Append("\tvar __items = new ").Append(NativeName(table)).Append('[').Append(local).AppendLine(".Length];");
				_sb.Append(indent).Append("\tfor (int i = 0; i < __items.Length; i++) __items[i] = ").Append(local).AppendLine("[i].ToNative();");
				_sb.Append(indent).Append("\t__value.").Append(fieldName).AppendLine(" = __items;");
			}
		}
		_sb.Append(indent).AppendLine("}");
	}

	void EmitNativeCreateLocals(TableDef table, string indent)
	{
		foreach (var field in table.Fields)
		{
			if (field.Deprecated || NativeFieldType(field.Type) == null)
				continue;
			EmitNativeCreateLocal(field, indent);
		}
	}

	void EmitNativeCreateLocal(FieldDef field, string indent)
	{
		string fieldName = ToPascalCase(field.Name);
		string local = "__" + fieldName;
		if (field.Type.IsString)
		{
			_sb.Append(indent).Append("StringOffset ").Append(local).Append(" = value.").Append(fieldName).AppendLine(" == null ? default : builder.CreateString(value." + fieldName + ");");
			return;
		}
		if (field.Type.IsObject && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var def) && def is TableDef table && table.NativeStruct)
		{
			_sb.Append(indent).Append("Offset<").Append(field.Type.ReferencedName).Append("> ").Append(local).Append(" = value.").Append(fieldName).Append(".HasValue ? ").Append(NativeName(table)).Append(".Serialize(ref builder, in value.").Append(fieldName).Append(".GetValueOrDefault()).AsOffset : default;").AppendLine();
			return;
		}
		if (field.Type.IsVector)
			EmitNativeVectorCreateLocal(field, indent);
	}

	void EmitNativeVectorCreateLocal(FieldDef field, string indent)
	{
		string fieldName = ToPascalCase(field.Name);
		string local = "__" + fieldName;
		string source = "value." + fieldName;
		if (field.Type.ElementBase.IsScalar() || (field.Type.ElementBase == SchemaBaseType.Obj && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var def) && def is StructDef))
		{
			_sb.Append(indent).Append("VectorOffset ").Append(local).Append(" = ").Append(source).Append(" == null ? default : builder.CreateVector(").Append(source).AppendLine(");");
			return;
		}
		if (field.Type.ElementBase == SchemaBaseType.String)
		{
			_sb.Append(indent).Append("VectorOffset ").Append(local).AppendLine(" = default;");
			_sb.Append(indent).Append("if (").Append(source).AppendLine(" != null)");
			_sb.Append(indent).AppendLine("{");
			_sb.Append(indent).Append("\tSpan<int> __offsets = stackalloc int[").Append(source).AppendLine(".Length];");
			_sb.Append(indent).Append("\tfor (int i = 0; i < __offsets.Length; i++) __offsets[i] = builder.CreateString(").Append(source).AppendLine("[i]);");
			_sb.Append(indent).Append("\t").Append(local).AppendLine(" = builder.CreateVectorOfOffsets(__offsets);");
			_sb.Append(indent).AppendLine("}");
			return;
		}
		if (field.Type.ElementBase == SchemaBaseType.Obj && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var elementDef))
		{
			if (elementDef is EnumDef enumDef)
			{
				_sb.Append(indent).Append("VectorOffset ").Append(local).Append(" = ").Append(source).Append(" == null ? default : builder.CreateVector(").Append(source).AppendLine(");");
				return;
			}
			if (elementDef is TableDef table && table.NativeStruct)
			{
				_sb.Append(indent).Append("VectorOffset ").Append(local).AppendLine(" = default;");
				_sb.Append(indent).Append("if (").Append(source).AppendLine(" != null)");
				_sb.Append(indent).AppendLine("{");
				_sb.Append(indent).Append("\tSpan<int> __offsets = stackalloc int[").Append(source).AppendLine(".Length];");
				_sb.Append(indent).Append("\tfor (int i = 0; i < __offsets.Length; i++) __offsets[i] = ").Append(NativeName(table)).Append(".Serialize(ref builder, in ").Append(source).AppendLine("[i]).BufferPos;");
				_sb.Append(indent).Append("\t").Append(local).AppendLine(" = builder.CreateVectorOfOffsets(__offsets);");
				_sb.Append(indent).AppendLine("}");
			}
		}
	}

	string NativeCreateArgument(FieldDef field)
	{
		if (field.Type.IsString || field.Type.IsVector || (field.Type.IsObject && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var def) && def is TableDef table && table.NativeStruct))
			return "__" + ToPascalCase(field.Name);
		return "value." + ToPascalCase(field.Name);
	}
}
