using FlatBufferLite.SourceGen.IR;

namespace FlatBufferLite.SourceGen.Emit;

public sealed partial class CodeEmitter
{
	static string PlainName(TableDef table) => table.Name;

	void EmitPlainStruct(TableDef table)
	{
		_sb.AppendLine();
		_sb.Append("public partial struct ").AppendLine(PlainName(table));
		_sb.AppendLine("{");
		foreach (var field in table.Fields)
		{
			if (field.Deprecated)
				continue;
			var typeName = PlainFieldType(field);
			if (typeName == null)
				continue;
			_sb.Append("\tpublic ").Append(typeName).Append(' ').Append(ToPascalCase(field.Name)).AppendLine(";");
		}
		EmitPlainCollectionStaticConstructor(table);

		_sb.AppendLine();
		_sb.Append("\tpublic static void Deserialize(Span<byte> buffer, ref ").Append(PlainName(table)).Append(" value) => ").Append(table.Name).Append("Ref").AppendLine(".GetRootAs(buffer).ToPlain(ref value);");
		_sb.AppendLine();
		_sb.Append("\tpublic static ").Append(table.Name).Append("Ref").Append(" Serialize(ref FlatBufferBuilder builder, in ").Append(PlainName(table)).AppendLine(" value)");
		_sb.AppendLine("\t{");
		EmitPlainCreateLocals(table, "\t\t");
		_sb.Append("\t\treturn ").Append(table.Name).Append("Ref").Append(".Create(ref builder");
		foreach (var field in table.Fields)
		{
			if (field.Deprecated || PlainFieldType(field) == null)
				continue;
			EmitPlainCreateArgument(field);
		}
		_sb.AppendLine(");");
		_sb.AppendLine("\t}");
		_sb.AppendLine("}");
	}

	void EmitPlainTableMethods(TableDef table)
	{
		_sb.AppendLine();
		_sb.Append("\tpublic void ToPlain(ref ").Append(PlainName(table)).AppendLine(" value)");
		_sb.AppendLine("\t{");
		foreach (var field in table.Fields)
		{
			if (field.Deprecated || PlainFieldType(field) == null)
				continue;
			EmitPlainReadAssign(field, "\t\t");
		}
		_sb.AppendLine("\t}");
	}

	void EmitPlainCollectionStaticConstructor(TableDef table)
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
				else if (def is TableDef nestedTable && nestedTable.PlainStruct)
					Add(listTypes, PlainName(nestedTable));
			}
		}
		if (collectionTypes.Count == 0 && listTypes.Count == 0)
			return;
		_sb.AppendLine();
		_sb.Append("\tstatic ").Append(PlainName(table)).AppendLine("()");
		_sb.AppendLine("\t{");
		foreach (var type in collectionTypes)
			_sb.Append("\t\tFlatBufferCollections.EnsureCreate<").Append(type).AppendLine(">();");
		foreach (var type in listTypes)
			_sb.Append("\t\tFlatBufferPlainVectors.EnsureCreate<").Append(type).AppendLine(">();");
		_sb.AppendLine("\t}");
	}

	string? PlainFieldType(FieldDef field) => PlainFieldType(field.Type, field.CustomCollection);

	string? PlainFieldType(TypeRef type, bool customCollection = false)
	{
		if (type.Base.IsScalar())
			return ScalarCSharpType(type, out _);
		if (type.IsString)
			return customCollection ? "IFlatBufferCollection<byte>?" : "byte[]?";
		if (type.IsUnion)
		{
			if (type.ReferencedName != null && _schema.ByName.TryGetValue(type.ReferencedName, out var unionObjectDef) && unionObjectDef is UnionDef union)
				return PlainUnionName(union);
			return null;
		}
		if (type.IsObject && type.ReferencedName != null && _schema.ByName.TryGetValue(type.ReferencedName, out var def))
		{
			if (def is TableDef table)
				return table.PlainStruct ? PlainName(table) + "?" : null;
			if (def is StructDef || def is EnumDef)
				return type.ReferencedName;
		}
		if (type.IsVector)
			return PlainVectorFieldType(type, customCollection);
		return null;
	}

	string? PlainVectorFieldType(TypeRef type, bool customCollection)
	{
		if (!customCollection)
		{
			var elementType = PlainVectorElementType(type);
			return elementType == null ? null : elementType + "[]?";
		}
		if (type.ElementBase.IsScalar())
			return "IFlatBufferCollection<" + type.ElementBase.ToCSharpKeyword() + ">?";
		if (type.ElementBase == SchemaBaseType.String)
			return "IFlatBufferPlainVector<IFlatBufferCollection<byte>>?";
		if (type.ElementBase == SchemaBaseType.Obj && type.ReferencedName != null && _schema.ByName.TryGetValue(type.ReferencedName, out var def))
		{
			if (def is StructDef || def is EnumDef)
				return "IFlatBufferCollection<" + type.ReferencedName + ">?";
			if (def is TableDef table)
				return table.PlainStruct ? "IFlatBufferPlainVector<" + PlainName(table) + ">?" : null;
		}
		return null;
	}

	string? PlainVectorElementType(TypeRef type)
	{
		if (type.ElementBase.IsScalar())
			return type.ElementBase.ToCSharpKeyword();
		if (type.ElementBase == SchemaBaseType.String)
			return "byte[]";
		if (type.ElementBase == SchemaBaseType.Obj && type.ReferencedName != null && _schema.ByName.TryGetValue(type.ReferencedName, out var def))
		{
			if (def is TableDef table)
				return table.PlainStruct ? PlainName(table) : null;
			if (def is StructDef || def is EnumDef)
				return type.ReferencedName;
		}
		return null;
	}

	void EmitPlainReadAssign(FieldDef field, string indent)
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
				EmitPlainStringCollectionReadAssign(fieldName, indent);
			else
				_sb.Append(indent).Append("value.").Append(fieldName).Append(" = ").Append(fieldName).Append(".IsValid ? ").Append(fieldName).AppendLine(".AsBytes.ToArray() : null;");
			return;
		}
		if (field.Type.IsUnion)
		{
			EmitPlainUnionReadAssign(field, indent);
			return;
		}
		if (field.Type.IsObject && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var def) && def is TableDef table && table.PlainStruct)
		{
			_sb.Append(indent).Append("var ").Append(local).Append(" = ").Append(fieldName).AppendLine(";");
			_sb.Append(indent).Append("if (").Append(local).AppendLine(".IsValid)");
			_sb.Append(indent).AppendLine("{");
			_sb.Append(indent).Append("\tvar __item = value.").Append(fieldName).AppendLine(".GetValueOrDefault();");
			_sb.Append(indent).Append("\t").Append(local).AppendLine(".ToPlain(ref __item);");
			_sb.Append(indent).Append("\tvalue.").Append(fieldName).AppendLine(" = __item;");
			_sb.Append(indent).AppendLine("}");
			_sb.Append(indent).AppendLine("else");
			_sb.Append(indent).AppendLine("{");
			_sb.Append(indent).Append("\tvalue.").Append(fieldName).AppendLine(" = null;");
			_sb.Append(indent).AppendLine("}");
			return;
		}
		if (field.Type.IsVector)
			EmitPlainVectorReadAssign(field, indent);
	}

	void EmitPlainStringCollectionReadAssign(string fieldName, string indent)
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

	void EmitPlainVectorReadAssign(FieldDef field, string indent)
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
				_sb.Append(indent).Append("\t\t").Append(target).Append(" = FlatBufferPlainVectors.Create<IFlatBufferCollection<byte>>(").Append(local).AppendLine(".Length);");
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
			else if (elementDef is TableDef table && table.PlainStruct)
			{
				if (field.CustomCollection)
				{
					_sb.Append(indent).Append("\tvar ").Append(target).Append(" = value.").Append(fieldName).AppendLine(";");
					_sb.Append(indent).Append("\tif (").Append(target).AppendLine(" == null)");
					_sb.Append(indent).AppendLine("\t{");
					_sb.Append(indent).Append("\t\t").Append(target).Append(" = FlatBufferPlainVectors.Create<").Append(PlainName(table)).Append(">(").Append(local).AppendLine(".Length);");
					_sb.Append(indent).Append("\t\tvalue.").Append(fieldName).Append(" = ").Append(target).AppendLine(";");
					_sb.Append(indent).AppendLine("\t}");
					_sb.Append(indent).Append("\t").Append(target).Append(".Resize(").Append(local).AppendLine(".Length);");
					_sb.Append(indent).Append("\t").Append(local).Append(".CopyTo(").Append(target).AppendLine(".AsSpan());");
				}
				else
				{
					_sb.Append(indent).Append("\tvar __items = new ").Append(PlainName(table)).Append('[').Append(local).AppendLine(".Length];");
					_sb.Append(indent).Append("\tfor (int i = 0; i < __items.Length; i++) ").Append(local).AppendLine("[i].ToPlain(ref __items[i]);");
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

	void EmitPlainCreateLocals(TableDef table, string indent)
	{
		foreach (var field in table.Fields)
		{
			if (field.Deprecated || PlainFieldType(field) == null)
				continue;
			EmitPlainCreateLocal(field, indent);
		}
	}

	void EmitPlainCreateLocal(FieldDef field, string indent)
	{
		string fieldName = ToPascalCase(field.Name);
		string local = "__" + fieldName;
		if (field.Type.IsString)
		{
			_sb.Append(indent).Append("StringOffset ").Append(local).Append(" = value.").Append(fieldName).Append(" == null ? default : builder.CreateString(value.").Append(fieldName).Append(field.CustomCollection ? ".AsReadOnlySpan()" : "").AppendLine(");");
			return;
		}
		if (field.Type.IsObject && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var def) && def is TableDef table && table.PlainStruct)
		{
			_sb.Append(indent).Append("Offset<").Append(field.Type.ReferencedName).Append("Ref> ").Append(local).Append(" = value.").Append(fieldName).Append(".HasValue ? ").Append(PlainName(table)).Append(".Serialize(ref builder, in value.").Append(fieldName).Append(".GetValueOrDefault()).AsOffset : default;").AppendLine();
			return;
		}
		if (field.Type.IsUnion)
		{
			EmitPlainUnionCreateLocal(field, indent);
			return;
		}
		if (field.Type.IsVector)
			EmitPlainVectorCreateLocal(field, indent);
	}

	void EmitPlainUnionReadAssign(FieldDef field, string indent)
	{
		if (field.Type.ReferencedName == null || !_schema.ByName.TryGetValue(field.Type.ReferencedName, out var def) || def is not UnionDef union)
			return;

		string fieldName = ToPascalCase(field.Name);
		string source = "__" + fieldName + "Source";
		string previous = "__" + fieldName + "Previous";
		string target = "__" + fieldName + "Target";
		bool refUnion = _refUnions.Contains(union.Name);
		_sb.Append(indent).Append("var ").Append(source).Append(" = ").Append(fieldName).AppendLine(";");
		_sb.Append(indent).Append("var ").Append(previous).Append(" = value.").Append(fieldName).AppendLine(";");
		_sb.Append(indent).Append("var ").Append(target).Append(" = default(").Append(PlainUnionName(union)).AppendLine(");");
		_sb.Append(indent).Append("if (").Append(source).AppendLine(".HasValue)");
		_sb.Append(indent).AppendLine("{");
		_sb.Append(indent).Append("\tswitch (").Append(fieldName).AppendLine("Type)");
		_sb.Append(indent).AppendLine("\t{");
		foreach (var member in union.Members)
		{
			if (!TryGetPlainUnionMemberType(member, out string memberType, out var memberKind))
				continue;
			string memberName = member.Name;
			string refLocal = "__" + fieldName + memberName + "Ref";
			string valueLocal = "__" + fieldName + memberName;
			_sb.Append(indent).Append("\tcase ").Append(union.Name).Append("Kind.").Append(memberName).AppendLine(":");
			if (memberKind == PlainUnionMemberKind.PlainTable)
			{
				_sb.Append(indent).Append("\t\tif (").Append(source).Append(".TryGetAs").Append(memberName).Append("(out var ").Append(refLocal).AppendLine("))");
				_sb.Append(indent).AppendLine("\t\t{");
				_sb.Append(indent).Append("\t\t\tvar ").Append(valueLocal).Append(" = ").Append(previous).Append('.').Append(memberName).AppendLine(".GetValueOrDefault();");
				_sb.Append(indent).Append("\t\t\t").Append(refLocal).Append(".ToPlain(ref ").Append(valueLocal).AppendLine(");");
				_sb.Append(indent).Append("\t\t\t").Append(target).Append(".Kind = ").Append(union.Name).Append("Kind.").Append(memberName).AppendLine(";");
				_sb.Append(indent).Append("\t\t\t").Append(target).Append('.').Append(memberName).Append(" = ").Append(valueLocal).AppendLine(";");
				_sb.Append(indent).AppendLine("\t\t}");
			}
			else if (memberKind == PlainUnionMemberKind.RefTable)
			{
				_sb.Append(indent).Append("\t\tif (").Append(source).Append(".TryGetAs").Append(memberName).Append("(out var ").Append(refLocal).AppendLine("))");
				_sb.Append(indent).AppendLine("\t\t{");
				_sb.Append(indent).Append("\t\t\t").Append(target).Append(".Kind = ").Append(union.Name).Append("Kind.").Append(memberName).AppendLine(";");
				_sb.Append(indent).Append("\t\t\t").Append(target).Append('.').Append(memberName).Append(" = ").Append(refLocal).AppendLine(".AsOffset;");
				_sb.Append(indent).AppendLine("\t\t}");
			}
			else if (!refUnion)
			{
				_sb.Append(indent).Append("\t\tif (").Append(source).Append(".TryGetValue(out ").Append(memberType).Append(' ').Append(valueLocal).AppendLine("))");
				_sb.Append(indent).AppendLine("\t\t{");
				_sb.Append(indent).Append("\t\t\t").Append(target).Append(".Kind = ").Append(union.Name).Append("Kind.").Append(memberName).AppendLine(";");
				_sb.Append(indent).Append("\t\t\t").Append(target).Append('.').Append(memberName).Append(" = ").Append(valueLocal).AppendLine(";");
				_sb.Append(indent).AppendLine("\t\t}");
			}
			_sb.Append(indent).AppendLine("\t\tbreak;");
		}
		_sb.Append(indent).AppendLine("\t}");
		_sb.Append(indent).AppendLine("}");
		_sb.Append(indent).Append("value.").Append(fieldName).Append(" = ").Append(target).AppendLine(";");
	}

	void EmitPlainUnionCreateLocal(FieldDef field, string indent)
	{
		if (field.Type.ReferencedName == null || !_schema.ByName.TryGetValue(field.Type.ReferencedName, out var def) || def is not UnionDef union)
			return;

		string fieldName = ToPascalCase(field.Name);
		string local = "__" + fieldName;
		string typeLocal = local + "Type";
		string source = "value." + fieldName;
		_sb.Append(indent).Append(union.Name).Append("Kind ").Append(typeLocal).Append(" = ").Append(source).AppendLine(".Kind;");
		_sb.Append(indent).Append("int ").Append(local).AppendLine(" = 0;");
		_sb.Append(indent).Append("switch (").Append(typeLocal).AppendLine(")");
		_sb.Append(indent).AppendLine("{");
		foreach (var member in union.Members)
		{
			if (!TryGetPlainUnionMemberType(member, out _, out var memberKind))
				continue;
			string memberName = member.Name;
			string valueLocal = local + memberName;
			_sb.Append(indent).Append("case ").Append(union.Name).Append("Kind.").Append(memberName).AppendLine(":");
			if (memberKind == PlainUnionMemberKind.PlainTable)
			{
				_sb.Append(indent).Append("\tif (").Append(source).Append('.').Append(memberName).AppendLine(".HasValue)");
				_sb.Append(indent).AppendLine("\t{");
				_sb.Append(indent).Append("\t\tvar ").Append(valueLocal).Append(" = ").Append(source).Append('.').Append(memberName).AppendLine(".GetValueOrDefault();");
				_sb.Append(indent).Append("\t\t").Append(local).Append(" = ").Append(member.TypeName).Append(".Serialize(ref builder, in ").Append(valueLocal).AppendLine(").BufferPos;");
				_sb.Append(indent).AppendLine("\t}");
				_sb.Append(indent).AppendLine("\telse");
				_sb.Append(indent).AppendLine("\t{");
				_sb.Append(indent).Append("\t\t").Append(typeLocal).AppendLine(" = default;");
				_sb.Append(indent).AppendLine("\t}");
			}
			else if (memberKind == PlainUnionMemberKind.RefTable)
			{
				_sb.Append(indent).Append("\tif (").Append(source).Append('.').Append(memberName).AppendLine(".HasValue)");
				_sb.Append(indent).AppendLine("\t{");
				_sb.Append(indent).Append("\t\t").Append(local).Append(" = ").Append(source).Append('.').Append(memberName).AppendLine(".GetValueOrDefault().Value;");
				_sb.Append(indent).AppendLine("\t}");
				_sb.Append(indent).AppendLine("\telse");
				_sb.Append(indent).AppendLine("\t{");
				_sb.Append(indent).Append("\t\t").Append(typeLocal).AppendLine(" = default;");
				_sb.Append(indent).AppendLine("\t}");
			}
			else
			{
				_sb.Append(indent).Append("\t").Append(typeLocal).AppendLine(" = default;");
			}
			_sb.Append(indent).AppendLine("\tbreak;");
		}
		_sb.Append(indent).AppendLine("default:");
		_sb.Append(indent).Append("\t").Append(typeLocal).AppendLine(" = default;");
		_sb.Append(indent).AppendLine("\tbreak;");
		_sb.Append(indent).AppendLine("}");
	}

	void EmitPlainVectorCreateLocal(FieldDef field, string indent)
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
			if (elementDef is TableDef table && table.PlainStruct)
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
					_sb.Append(indent).Append("\t\t__offsets[i] = ").Append(PlainName(table)).AppendLine(".Serialize(ref builder, in __item).BufferPos;");
					_sb.Append(indent).AppendLine("\t}");
				}
				else
				{
					_sb.Append(indent).Append("\tfor (int i = 0; i < __offsets.Length; i++) __offsets[i] = ").Append(PlainName(table)).Append(".Serialize(ref builder, in ").Append(source).AppendLine("[i]).BufferPos;");
				}
				_sb.Append(indent).Append("\t").Append(local).AppendLine(" = builder.CreateVectorOfOffsets(__offsets);");
				_sb.Append(indent).AppendLine("}");
			}
		}
	}

	string PlainCreateArgument(FieldDef field)
	{
		if (field.Type.IsString || field.Type.IsVector || (field.Type.IsObject && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var def) && def is TableDef table && table.PlainStruct))
			return "__" + ToPascalCase(field.Name);
		return "value." + ToPascalCase(field.Name);
	}

	void EmitPlainCreateArgument(FieldDef field)
	{
		string fieldName = ToPascalCase(field.Name);
		string parameterName = ToCamelCase(field.Name);
		if (field.Type.IsUnion)
		{
			_sb.Append(", ").Append(parameterName).Append("Type: __").Append(fieldName).Append("Type");
			_sb.Append(", ").Append(parameterName).Append(": __").Append(fieldName);
			return;
		}

		_sb.Append(", ").Append(parameterName).Append(": ").Append(PlainCreateArgument(field));
	}


	string RefUnionName(UnionDef union) => RefUnionName(union.Name);

	string RefUnionName(string unionName) => unionName + "Ref";

	string PlainUnionName(UnionDef union) => _refUnions.Contains(union.Name) ? union.Name : union.Name + "Plain";

	enum PlainUnionMemberKind
	{
		None,
		Struct,
		Enum,
		PlainTable,
		RefTable,
	}

	bool CanEmitPlainUnion(UnionDef union)
	{
		foreach (var member in union.Members)
			if (!TryGetPlainUnionMemberType(member, out _))
				return false;
		return true;
	}

	bool TryGetPlainUnionMemberType(UnionMember member, out string typeName)
		=> TryGetPlainUnionMemberType(member, out typeName, out _);

	bool TryGetPlainUnionMemberType(UnionMember member, out string typeName, out PlainUnionMemberKind kind)
	{
		typeName = "";
		kind = PlainUnionMemberKind.None;
		if (!_schema.ByName.TryGetValue(member.TypeName, out var def))
			return false;
		if (def is StructDef)
		{
			typeName = member.TypeName;
			kind = PlainUnionMemberKind.Struct;
			return true;
		}
		if (def is EnumDef)
		{
			typeName = member.TypeName;
			kind = PlainUnionMemberKind.Enum;
			return true;
		}
		if (def is not TableDef table)
			return false;
		if (table.PlainStruct)
		{
			typeName = PlainName(table);
			kind = PlainUnionMemberKind.PlainTable;
			return true;
		}
		typeName = "Offset<" + member.TypeName + "Ref>";
		kind = PlainUnionMemberKind.RefTable;
		return true;
	}
}
