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
			var typeName = NativeFieldType(field);
			if (typeName == null)
				continue;
			_sb.Append("\tpublic ").Append(typeName).Append(' ').Append(ToPascalCase(field.Name)).AppendLine(";");
		}
		EmitNativeCollectionStaticConstructor(table);

		_sb.AppendLine();
		_sb.Append("\tpublic static void Deserialize(Span<byte> buffer, ref ").Append(NativeName(table)).Append(" value) => ").Append(table.Name).AppendLine(".GetRootAs(buffer).ToNative(ref value);");
		_sb.AppendLine();
		_sb.Append("\tpublic static ").Append(table.Name).Append(" Serialize(ref FlatBufferBuilder builder, in ").Append(NativeName(table)).AppendLine(" value)");
		_sb.AppendLine("\t{");
		EmitNativeCreateLocals(table, "\t\t");
		_sb.Append("\t\treturn ").Append(table.Name).Append(".Create(ref builder");
		foreach (var field in table.Fields)
		{
			if (field.Deprecated || NativeFieldType(field) == null)
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
		_sb.Append("\tpublic void ToNative(ref ").Append(NativeName(table)).AppendLine(" value)");
		_sb.AppendLine("\t{");
		foreach (var field in table.Fields)
		{
			if (field.Deprecated || NativeFieldType(field) == null)
				continue;
			EmitNativeReadAssign(field, "\t\t");
		}
		_sb.AppendLine("\t}");
	}

	void EmitNativeCollectionStaticConstructor(TableDef table)
	{
		var collectionTypes = new System.Collections.Generic.List<string>();
		var listTypes = new System.Collections.Generic.List<string>();
		void Add(System.Collections.Generic.List<string> list, string type)
		{
			if (!list.Contains(type))
				list.Add(type);
		}
		foreach (var field in table.Fields)
		{
			if (!field.CustomCollection || field.Deprecated)
				continue;
			if (field.Type.IsString || (field.Type.IsVector && field.Type.ElementBase == SchemaBaseType.String))
			{
				Add(collectionTypes, "byte");
				if (field.Type.IsVector)
					Add(listTypes, "IFlatBufferCollection<byte>");
				continue;
			}
			if (!field.Type.IsVector)
				continue;
			if (field.Type.ElementBase.IsScalar())
			{
				Add(collectionTypes, field.Type.ElementBase.ToCSharpKeyword());
				continue;
			}
			if (field.Type.ElementBase == SchemaBaseType.Obj && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var def))
			{
				if (def is StructDef || def is EnumDef)
					Add(collectionTypes, field.Type.ReferencedName);
				else if (def is TableDef nestedTable && nestedTable.NativeStruct)
					Add(listTypes, NativeName(nestedTable));
			}
		}
		if (collectionTypes.Count == 0 && listTypes.Count == 0)
			return;
		_sb.AppendLine();
		_sb.Append("\tstatic ").Append(NativeName(table)).AppendLine("()");
		_sb.AppendLine("\t{");
		foreach (var type in collectionTypes)
			_sb.Append("\t\tFlatBufferCollections.EnsureCreate<").Append(type).AppendLine(">();");
		foreach (var type in listTypes)
			_sb.Append("\t\tFlatBufferNativeVectors.EnsureCreate<").Append(type).AppendLine(">();");
		_sb.AppendLine("\t}");
	}

	string? NativeFieldType(FieldDef field) => NativeFieldType(field.Type, field.CustomCollection);

	string? NativeFieldType(TypeRef type, bool customCollection = false)
	{
		if (type.Base.IsScalar())
			return ScalarCSharpType(type, out _);
		if (type.IsString)
			return customCollection ? "IFlatBufferCollection<byte>?" : "byte[]?";
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
			return NativeVectorFieldType(type, customCollection);
		return null;
	}

	string? NativeVectorFieldType(TypeRef type, bool customCollection)
	{
		if (!customCollection)
		{
			var elementType = NativeVectorElementType(type);
			return elementType == null ? null : elementType + "[]?";
		}
		if (type.ElementBase.IsScalar())
			return "IFlatBufferCollection<" + type.ElementBase.ToCSharpKeyword() + ">?";
		if (type.ElementBase == SchemaBaseType.String)
			return "IFlatBufferNativeVector<IFlatBufferCollection<byte>>?";
		if (type.ElementBase == SchemaBaseType.Obj && type.ReferencedName != null && _schema.ByName.TryGetValue(type.ReferencedName, out var def))
		{
			if (def is StructDef || def is EnumDef)
				return "IFlatBufferCollection<" + type.ReferencedName + ">?";
			if (def is TableDef table)
				return table.NativeStruct ? "IFlatBufferNativeVector<" + NativeName(table) + ">?" : null;
		}
		return null;
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
			_sb.Append(indent).Append("value.").Append(fieldName).Append(" = ").Append(fieldName).AppendLine(";");
			return;
		}
		if (field.Type.IsString)
		{
			if (field.CustomCollection)
				EmitNativeStringCollectionReadAssign(fieldName, indent);
			else
				_sb.Append(indent).Append("value.").Append(fieldName).Append(" = ").Append(fieldName).Append(".IsValid ? ").Append(fieldName).AppendLine(".AsBytes.ToArray() : null;");
			return;
		}
		if (field.Type.IsObject && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var def) && def is TableDef table && table.NativeStruct)
		{
			_sb.Append(indent).Append("var ").Append(local).Append(" = ").Append(fieldName).AppendLine(";");
			_sb.Append(indent).Append("if (").Append(local).AppendLine(".IsValid)");
			_sb.Append(indent).AppendLine("{");
			_sb.Append(indent).Append("\tvar __item = value.").Append(fieldName).AppendLine(".GetValueOrDefault();");
			_sb.Append(indent).Append("\t").Append(local).AppendLine(".ToNative(ref __item);");
			_sb.Append(indent).Append("\tvalue.").Append(fieldName).AppendLine(" = __item;");
			_sb.Append(indent).AppendLine("}");
			_sb.Append(indent).AppendLine("else");
			_sb.Append(indent).AppendLine("{");
			_sb.Append(indent).Append("\tvalue.").Append(fieldName).AppendLine(" = null;");
			_sb.Append(indent).AppendLine("}");
			return;
		}
		if (field.Type.IsVector)
			EmitNativeVectorReadAssign(field, indent);
	}

	void EmitNativeStringCollectionReadAssign(string fieldName, string indent)
	{
		string target = "__" + fieldName + "Target";
		string source = "__" + fieldName + "Source";
		string vector = "__" + fieldName + "Vector";
		_sb.Append(indent).Append("var ").Append(source).Append(" = ").Append(fieldName).AppendLine(";");
		_sb.Append(indent).Append("if (").Append(source).AppendLine(".IsValid)");
		_sb.Append(indent).AppendLine("{");
		_sb.Append(indent).Append("\tvar ").Append(target).Append(" = value.").Append(fieldName).AppendLine(";");
		_sb.Append(indent).Append("\tif (").Append(target).AppendLine(" == null)");
		_sb.Append(indent).AppendLine("\t{");
		_sb.Append(indent).Append("\t\t").Append(target).Append(" = FlatBufferCollections.Create<byte>(").Append(source).AppendLine(".Length);");
		_sb.Append(indent).Append("\t\tvalue.").Append(fieldName).Append(" = ").Append(target).AppendLine(";");
		_sb.Append(indent).AppendLine("\t}");
		_sb.Append(indent).Append("\tvar ").Append(vector).Append(" = new FlatVector<byte>(_buf, ").Append(source).AppendLine(".Position);");
		_sb.Append(indent).Append("\t").Append(target).Append(".ReplaceRange(ref ").Append(vector).AppendLine(");");
		_sb.Append(indent).AppendLine("}");
		_sb.Append(indent).AppendLine("else");
		_sb.Append(indent).AppendLine("{");
		_sb.Append(indent).Append("\tvalue.").Append(fieldName).AppendLine("?.Clear();");
		_sb.Append(indent).AppendLine("}");
	}

	void EmitNativeVectorReadAssign(FieldDef field, string indent)
	{
		string fieldName = ToPascalCase(field.Name);
		string local = "__" + fieldName;
		string target = "__" + fieldName + "Target";
		string item = "__" + fieldName + "Item";
		string items = "__" + fieldName + "Items";
		string sourceItem = "__" + fieldName + "Source";
		string vector = "__" + fieldName + "Vector";
		_sb.Append(indent).Append("var ").Append(local).Append(" = ").Append(fieldName).AppendLine(";");
		_sb.Append(indent).Append("if (").Append(local).AppendLine(".IsValid)");
		_sb.Append(indent).AppendLine("{");
		if (field.Type.ElementBase.IsScalar() || (field.Type.ElementBase == SchemaBaseType.Obj && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var def) && def is StructDef))
		{
			if (field.CustomCollection)
			{
				string elementType = field.Type.ElementBase.IsScalar() ? field.Type.ElementBase.ToCSharpKeyword() : field.Type.ReferencedName!;
				_sb.Append(indent).Append("\tvar ").Append(target).Append(" = value.").Append(fieldName).AppendLine(";");
				_sb.Append(indent).Append("\tif (").Append(target).AppendLine(" == null)");
				_sb.Append(indent).AppendLine("\t{");
				_sb.Append(indent).Append("\t\t").Append(target).Append(" = FlatBufferCollections.Create<").Append(elementType).Append(">(").Append(local).AppendLine(".Length);");
				_sb.Append(indent).Append("\t\tvalue.").Append(fieldName).Append(" = ").Append(target).AppendLine(";");
				_sb.Append(indent).AppendLine("\t}");
				_sb.Append(indent).Append("\t").Append(target).Append(".ReplaceRange(ref ").Append(local).AppendLine(");");
			}
			else
			{
				_sb.Append(indent).Append("\tvalue.").Append(fieldName).Append(" = ").Append(local).AppendLine(".AsSpan.ToArray();");
			}
		}
		else if (field.Type.ElementBase == SchemaBaseType.String)
		{
			if (field.CustomCollection)
			{
				_sb.Append(indent).Append("\tvar ").Append(target).Append(" = value.").Append(fieldName).AppendLine(";");
				_sb.Append(indent).Append("\tif (").Append(target).AppendLine(" == null)");
				_sb.Append(indent).AppendLine("\t{");
				_sb.Append(indent).Append("\t\t").Append(target).Append(" = FlatBufferNativeVectors.Create<IFlatBufferCollection<byte>>(").Append(local).AppendLine(".Length);");
				_sb.Append(indent).Append("\t\tvalue.").Append(fieldName).Append(" = ").Append(target).AppendLine(";");
				_sb.Append(indent).AppendLine("\t}");
				_sb.Append(indent).Append("\t").Append(target).Append(".Resize(").Append(local).AppendLine(".Length);");
				_sb.Append(indent).Append("\tvar ").Append(items).Append(" = ").Append(target).AppendLine(".AsSpan();");
				_sb.Append(indent).Append("\tfor (int i = 0; i < ").Append(items).AppendLine(".Length; i++)");
				_sb.Append(indent).AppendLine("\t{");
				_sb.Append(indent).Append("\t\tvar ").Append(item).Append(" = ").Append(items).AppendLine("[i];");
				_sb.Append(indent).Append("\t\tvar ").Append(sourceItem).Append(" = ").Append(local).AppendLine("[i];");
				_sb.Append(indent).Append("\t\tif (").Append(item).AppendLine(" == null)");
				_sb.Append(indent).AppendLine("\t\t{");
				_sb.Append(indent).Append("\t\t\t").Append(item).Append(" = FlatBufferCollections.Create<byte>(").Append(sourceItem).AppendLine(".Length);");
				_sb.Append(indent).Append("\t\t\t").Append(items).Append("[i] = ").Append(item).AppendLine(";");
				_sb.Append(indent).AppendLine("\t\t}");
				_sb.Append(indent).Append("\t\tvar ").Append(vector).Append(" = new FlatVector<byte>(_buf, ").Append(sourceItem).AppendLine(".Position);");
				_sb.Append(indent).Append("\t\t").Append(item).Append(".ReplaceRange(ref ").Append(vector).AppendLine(");");
				_sb.Append(indent).AppendLine("\t}");
			}
			else
			{
				_sb.Append(indent).Append("\tvar __items = new byte[").Append(local).AppendLine(".Length][];");
				_sb.Append(indent).Append("\tfor (int i = 0; i < __items.Length; i++) __items[i] = ").Append(local).AppendLine("[i].AsBytes.ToArray();");
				_sb.Append(indent).Append("\tvalue.").Append(fieldName).AppendLine(" = __items;");
			}
		}
		else if (field.Type.ElementBase == SchemaBaseType.Obj && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var elementDef))
		{
			if (elementDef is EnumDef)
			{
				if (field.CustomCollection)
				{
					_sb.Append(indent).Append("\tvar ").Append(target).Append(" = value.").Append(fieldName).AppendLine(";");
					_sb.Append(indent).Append("\tif (").Append(target).AppendLine(" == null)");
					_sb.Append(indent).AppendLine("\t{");
					_sb.Append(indent).Append("\t\t").Append(target).Append(" = FlatBufferCollections.Create<").Append(field.Type.ReferencedName).Append(">(").Append(local).AppendLine(".Length);");
					_sb.Append(indent).Append("\t\tvalue.").Append(fieldName).Append(" = ").Append(target).AppendLine(";");
					_sb.Append(indent).AppendLine("\t}");
					_sb.Append(indent).Append("\tvar ").Append(vector).Append(" = new FlatVector<").Append(field.Type.ReferencedName).Append(">(_buf, ").Append(local).AppendLine(".Position);");
					_sb.Append(indent).Append("\t").Append(target).Append(".ReplaceRange(ref ").Append(vector).AppendLine(");");
				}
				else
				{
					_sb.Append(indent).Append("\tvar __items = new ").Append(field.Type.ReferencedName).Append('[').Append(local).AppendLine(".Length];");
					_sb.Append(indent).Append("\tvar ").Append(vector).Append(" = new FlatVector<").Append(field.Type.ReferencedName).Append(">(_buf, ").Append(local).AppendLine(".Position);");
					_sb.Append(indent).Append("\t").Append(vector).AppendLine(".AsSpan.CopyTo(__items);");
					_sb.Append(indent).Append("\tvalue.").Append(fieldName).AppendLine(" = __items;");
				}
			}
			else if (elementDef is TableDef table && table.NativeStruct)
			{
				if (field.CustomCollection)
				{
					_sb.Append(indent).Append("\tvar ").Append(target).Append(" = value.").Append(fieldName).AppendLine(";");
					_sb.Append(indent).Append("\tif (").Append(target).AppendLine(" == null)");
					_sb.Append(indent).AppendLine("\t{");
					_sb.Append(indent).Append("\t\t").Append(target).Append(" = FlatBufferNativeVectors.Create<").Append(NativeName(table)).Append(">(").Append(local).AppendLine(".Length);");
					_sb.Append(indent).Append("\t\tvalue.").Append(fieldName).Append(" = ").Append(target).AppendLine(";");
					_sb.Append(indent).AppendLine("\t}");
					_sb.Append(indent).Append("\t").Append(target).Append(".Resize(").Append(local).AppendLine(".Length);");
					_sb.Append(indent).Append("\t").Append(local).Append(".CopyTo(").Append(target).AppendLine(".AsSpan());");
				}
				else
				{
					_sb.Append(indent).Append("\tvar __items = new ").Append(NativeName(table)).Append('[').Append(local).AppendLine(".Length];");
					_sb.Append(indent).Append("\tfor (int i = 0; i < __items.Length; i++) ").Append(local).AppendLine("[i].ToNative(ref __items[i]);");
					_sb.Append(indent).Append("\tvalue.").Append(fieldName).AppendLine(" = __items;");
				}
			}
		}
		_sb.Append(indent).AppendLine("}");
		_sb.Append(indent).AppendLine("else");
		_sb.Append(indent).AppendLine("{");
		if (field.CustomCollection)
			_sb.Append(indent).Append("\tvalue.").Append(fieldName).AppendLine("?.Clear();");
		else
			_sb.Append(indent).Append("\tvalue.").Append(fieldName).AppendLine(" = null;");
		_sb.Append(indent).AppendLine("}");
	}

	void EmitNativeCreateLocals(TableDef table, string indent)
	{
		foreach (var field in table.Fields)
		{
			if (field.Deprecated || NativeFieldType(field) == null)
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
			_sb.Append(indent).Append("StringOffset ").Append(local).Append(" = value.").Append(fieldName).Append(" == null ? default : builder.CreateString(value.").Append(fieldName).Append(field.CustomCollection ? ".AsReadOnlySpan()" : "").AppendLine(");");
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
			_sb.Append(indent).Append("VectorOffset ").Append(local).Append(" = ").Append(source).Append(" == null ? default : builder.CreateVector(").Append(source).Append(field.CustomCollection ? ".AsReadOnlySpan()" : "").AppendLine(");");
			return;
		}
		if (field.Type.ElementBase == SchemaBaseType.String)
		{
			_sb.Append(indent).Append("VectorOffset ").Append(local).AppendLine(" = default;");
			_sb.Append(indent).Append("if (").Append(source).AppendLine(" != null)");
			_sb.Append(indent).AppendLine("{");
			_sb.Append(indent).Append("\tSpan<int> __offsets = stackalloc int[").Append(source).Append(field.CustomCollection ? ".Count" : ".Length").AppendLine("];");
			_sb.Append(indent).Append("\tfor (int i = 0; i < __offsets.Length; i++) __offsets[i] = builder.CreateString(").Append(source).Append("[i]").Append(field.CustomCollection ? ".AsReadOnlySpan()" : "").AppendLine(");");
			_sb.Append(indent).Append("\t").Append(local).AppendLine(" = builder.CreateVectorOfOffsets(__offsets);");
			_sb.Append(indent).AppendLine("}");
			return;
		}
		if (field.Type.ElementBase == SchemaBaseType.Obj && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var elementDef))
		{
			if (elementDef is EnumDef)
			{
				_sb.Append(indent).Append("VectorOffset ").Append(local).Append(" = ").Append(source).Append(" == null ? default : builder.CreateVector(").Append(source).Append(field.CustomCollection ? ".AsReadOnlySpan()" : "").AppendLine(");");
				return;
			}
			if (elementDef is TableDef table && table.NativeStruct)
			{
				_sb.Append(indent).Append("VectorOffset ").Append(local).AppendLine(" = default;");
				_sb.Append(indent).Append("if (").Append(source).AppendLine(" != null)");
				_sb.Append(indent).AppendLine("{");
				_sb.Append(indent).Append("\tSpan<int> __offsets = stackalloc int[").Append(source).Append(field.CustomCollection ? ".Count" : ".Length").AppendLine("];");
				if (field.CustomCollection)
				{
					_sb.Append(indent).AppendLine("\tfor (int i = 0; i < __offsets.Length; i++)");
					_sb.Append(indent).AppendLine("\t{");
					_sb.Append(indent).Append("\t\tvar __item = ").Append(source).AppendLine("[i];");
					_sb.Append(indent).Append("\t\t__offsets[i] = ").Append(NativeName(table)).AppendLine(".Serialize(ref builder, in __item).BufferPos;");
					_sb.Append(indent).AppendLine("\t}");
				}
				else
				{
					_sb.Append(indent).Append("\tfor (int i = 0; i < __offsets.Length; i++) __offsets[i] = ").Append(NativeName(table)).Append(".Serialize(ref builder, in ").Append(source).AppendLine("[i]).BufferPos;");
				}
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