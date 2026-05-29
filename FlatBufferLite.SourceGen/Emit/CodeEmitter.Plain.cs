using FlatBufferLite.SourceGen.IR;

namespace FlatBufferLite.SourceGen.Emit;

public sealed partial class CodeEmitter
{
	static string PlainName(TableDef table) => table.Name;

	void EmitPlainStruct(TableDef table)
	{
		_w.AppendLine();
		EmitSchemaComment("table", table.Name, PlainName(table) + " plain struct", table.Location);
		_w.AppendLine("public partial struct " + PlainName(table));
		_w.OpenBlock();
		foreach (var field in table.Fields)
		{
			if (field.Deprecated)
				continue;
			var typeName = PlainFieldType(field);
			if (typeName == null)
				continue;
			string fieldName = ToPascalCase(field.Name);
			EmitPublicField(typeName, fieldName, SchemaComment("field", field.Name, fieldName, field.Location));
		}
		EmitPlainCollectionStaticConstructor(table);

		_w.AppendLine();
		_w.Append("public static void Deserialize(Span<byte> buffer, ref ").Append(PlainName(table)).Append(" value) => ").Append(table.Name).Append("Ref").AppendLine(".GetRootAs(buffer).ToPlain(ref value);");
		_w.AppendLine();
		_w.Append("public static ").Append(table.Name).Append("Ref").Append(" Serialize(ref FlatBufferBuilder builder, in ").Append(PlainName(table)).AppendLine(" value)");
		_w.OpenBlock();
		EmitPlainCreateLocals(table);
		_w.Append("return ").Append(table.Name).Append("Ref").Append(".Create(ref builder");
		foreach (var field in table.Fields)
		{
			if (field.Deprecated || PlainFieldType(field) == null)
				continue;
			EmitPlainCreateArgument(field);
		}
		_w.AppendLine(");");
		_w.CloseBlock();
		_w.CloseBlock();
	}

	void EmitPlainTableMethods(TableDef table)
	{
		_w.AppendLine();
		_w.Append("public void ToPlain(ref ").Append(PlainName(table)).AppendLine(" value)");
		_w.OpenBlock();
		foreach (var field in table.Fields)
		{
			if (field.Deprecated || PlainFieldType(field) == null)
				continue;
			EmitPlainReadAssign(field);
		}
		_w.CloseBlock();
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
		_w.AppendLine();
		_w.Append("static ").Append(PlainName(table)).AppendLine("()");
		_w.OpenBlock();
		foreach (var type in collectionTypes)
			_w.Append("FlatBufferCollections.EnsureCreate<").Append(type).Append(">()").EndStatement();
		foreach (var type in listTypes)
			_w.Append("FlatBufferPlainVectors.EnsureCreate<").Append(type).Append(">()").EndStatement();
		_w.CloseBlock();
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
				return union.Name;
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

	void EmitPlainReadAssign(FieldDef field)
	{
		string fieldName = ToPascalCase(field.Name);
		string local = "__" + fieldName;
		if (field.Type.Base.IsScalar() || (field.Type.IsObject && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var objectDef) && (objectDef is StructDef || objectDef is EnumDef)))
		{
			EmitValueFieldAssignment(fieldName, fieldName);
			return;
		}
		if (field.Type.IsString)
		{
			if (field.CustomCollection)
				EmitPlainStringCollectionReadAssign(fieldName);
			else
				_w.Append("value.").Append(fieldName).Append(" = ").Append(fieldName).Append(".IsValid ? ").Append(fieldName).AppendLine(".AsBytes.ToArray() : null;");
			return;
		}
		if (field.Type.IsUnion)
		{
			if (field.Type.ReferencedName == null || !_refUnions.Contains(field.Type.ReferencedName))
			{
				EmitValueFieldAssignment(fieldName, fieldName);
				return;
			}
			EmitPlainUnionReadAssign(field);
			return;
		}
		if (field.Type.IsObject && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var def) && def is TableDef table && table.PlainStruct)
		{
			EmitVar(local, fieldName);
			OpenIf(local, ".IsValid");
			_w.Append("var __item = value.").Append(fieldName).AppendLine(".GetValueOrDefault();");
			_w.Append(local).AppendLine(".ToPlain(ref __item);");
			EmitValueFieldAssignment(fieldName, "__item");
			_w.CloseBlock();
			OpenElseBlock();
			EmitValueFieldAssignment(fieldName, "null");
			_w.CloseBlock();
			return;
		}
		if (field.Type.IsVector)
			EmitPlainVectorReadAssign(field);
	}

	void EmitPlainStringCollectionReadAssign(string fieldName)
	{
		string target = "__" + fieldName + "Target";
		string source = "__" + fieldName + "Source";
		string vector = "__" + fieldName + "Vector";
		EmitVar(source, fieldName);
		OpenIf(source, ".IsValid");
		EmitVar(target, "value." + fieldName);
		OpenIf(target, " == null");
		_w.Append(target).Append(" = FlatBufferCollections.Create<byte>(").Append(source).AppendLine(".Length);");
		EmitValueFieldAssignment(fieldName, target);
		_w.CloseBlock();
		_w.Append("var ").Append(vector).Append(" = new FlatVector<byte>(_buf, ").Append(source).AppendLine(".Position);");
		_w.Append(target).Append(".ReplaceRange(ref ").Append(vector).AppendLine(");");
		_w.CloseBlock();
		OpenElseBlock();
		_w.Append("value.").Append(fieldName).AppendLine("?.Clear();");
		_w.CloseBlock();
	}

	void EmitPlainVectorReadAssign(FieldDef field)
	{
		string fieldName = ToPascalCase(field.Name);
		string local = "__" + fieldName;
		string target = "__" + fieldName + "Target";
		string item = "__" + fieldName + "Item";
		string items = "__" + fieldName + "Items";
		string sourceItem = "__" + fieldName + "Source";
		string vector = "__" + fieldName + "Vector";
		EmitVar(local, fieldName);
		OpenIf(local, ".IsValid");
		if (field.Type.ElementBase.IsScalar() || (field.Type.ElementBase == SchemaBaseType.Obj && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var def) && def is StructDef))
		{
			if (field.CustomCollection)
			{
				string elementType;
				if (field.Type.ElementBase.IsScalar())
					elementType = field.Type.ElementBase.ToCSharpKeyword();
				else if (field.Type.ReferencedName != null)
					elementType = field.Type.ReferencedName;
				else
					return;
				EmitVar(target, "value." + fieldName);
				OpenIf(target, " == null");
				_w.Append(target).Append(" = FlatBufferCollections.Create<").Append(elementType).Append(">(").Append(local).AppendLine(".Length);");
				EmitValueFieldAssignment(fieldName, target);
				_w.CloseBlock();
				_w.Append(target).Append(".ReplaceRange(ref ").Append(local).AppendLine(");");
			}
			else
			{
				EmitValueFieldAssignment(fieldName, local + ".AsSpan.ToArray()");
			}
		}
		else if (field.Type.ElementBase == SchemaBaseType.String)
		{
			if (field.CustomCollection)
			{
				EmitVar(target, "value." + fieldName);
				OpenIf(target, " == null");
				_w.Append(target).Append(" = FlatBufferPlainVectors.Create<IFlatBufferCollection<byte>>(").Append(local).AppendLine(".Length);");
				EmitValueFieldAssignment(fieldName, target);
				_w.CloseBlock();
				_w.Append(target).Append(".Resize(").Append(local).AppendLine(".Length);");
				_w.Append("var ").Append(items).Append(" = ").Append(target).AppendLine(".AsSpan();");
				_w.Append("for (int i = 0; i < ").Append(items).AppendLine(".Length; i++)");
				_w.OpenBlock();
				_w.Append("var ").Append(item).Append(" = ").Append(items).AppendLine("[i];");
				_w.Append("var ").Append(sourceItem).Append(" = ").Append(local).AppendLine("[i];");
				_w.Append("if (").Append(item).AppendLine(" == null)");
				_w.OpenBlock();
				_w.Append(item).Append(" = FlatBufferCollections.Create<byte>(").Append(sourceItem).AppendLine(".Length);");
				_w.Append(items).Append("[i] = ").Append(item).AppendLine(";");
				_w.CloseBlock();
				_w.Append("var ").Append(vector).Append(" = new FlatVector<byte>(_buf, ").Append(sourceItem).AppendLine(".Position);");
				_w.Append(item).Append(".ReplaceRange(ref ").Append(vector).AppendLine(");");
				_w.CloseBlock();
			}
			else
			{
				_w.Append("var __items = new byte[").Append(local).AppendLine(".Length][];");
				_w.Append("for (int i = 0; i < __items.Length; i++) __items[i] = ").Append(local).AppendLine("[i].AsBytes.ToArray();");
				_w.Append("value.").Append(fieldName).AppendLine(" = __items;");
			}
		}
		else if (field.Type.ElementBase == SchemaBaseType.Obj && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var elementDef))
		{
			if (elementDef is EnumDef)
			{
				if (field.CustomCollection)
				{
					EmitVar(target, "value." + fieldName);
					OpenIf(target, " == null");
					_w.Append(target).Append(" = FlatBufferCollections.Create<").Append(field.Type.ReferencedName).Append(">(").Append(local).AppendLine(".Length);");
					EmitValueFieldAssignment(fieldName, target);
					_w.CloseBlock();
					_w.Append("var ").Append(vector).Append(" = new FlatVector<").Append(field.Type.ReferencedName).Append(">(_buf, ").Append(local).AppendLine(".Position);");
					_w.Append(target).Append(".ReplaceRange(ref ").Append(vector).AppendLine(");");
				}
				else
				{
					_w.Append("var __items = new ").Append(field.Type.ReferencedName).Append('[').Append(local).AppendLine(".Length];");
					_w.Append("var ").Append(vector).Append(" = new FlatVector<").Append(field.Type.ReferencedName).Append(">(_buf, ").Append(local).AppendLine(".Position);");
					_w.Append(vector).AppendLine(".AsSpan.CopyTo(__items);");
					_w.Append("value.").Append(fieldName).AppendLine(" = __items;");
				}
			}
			else if (elementDef is TableDef table && table.PlainStruct)
			{
				if (field.CustomCollection)
				{
					EmitVar(target, "value." + fieldName);
					OpenIf(target, " == null");
					_w.Append(target).Append(" = FlatBufferPlainVectors.Create<").Append(PlainName(table)).Append(">(").Append(local).AppendLine(".Length);");
					EmitValueFieldAssignment(fieldName, target);
					_w.CloseBlock();
					_w.Append(target).Append(".Resize(").Append(local).AppendLine(".Length);");
					_w.Append(local).Append(".CopyTo(").Append(target).AppendLine(".AsSpan());");
				}
				else
				{
					_w.Append("var __items = new ").Append(PlainName(table)).Append('[').Append(local).AppendLine(".Length];");
					_w.Append("for (int i = 0; i < __items.Length; i++) ").Append(local).AppendLine("[i].ToPlain(ref __items[i]);");
					_w.Append("value.").Append(fieldName).AppendLine(" = __items;");
				}
			}
		}
		_w.CloseBlock();
		OpenElseBlock();
		if (field.CustomCollection)
			_w.Append("value.").Append(fieldName).AppendLine("?.Clear();");
		else
			EmitValueFieldAssignment(fieldName, "null");
		_w.CloseBlock();
	}

	void EmitPlainCreateLocals(TableDef table)
	{
		foreach (var field in table.Fields)
		{
			if (field.Deprecated || PlainFieldType(field) == null)
				continue;
			EmitPlainCreateLocal(field);
		}
	}

	void EmitPlainCreateLocal(FieldDef field)
	{
		string fieldName = ToPascalCase(field.Name);
		string local = "__" + fieldName;
		if (field.Type.IsString)
		{
			_w.Append("StringOffset ").Append(local).Append(" = value.").Append(fieldName).Append(" == null ? default : builder.CreateString(value.").Append(fieldName).Append(field.CustomCollection ? ".AsReadOnlySpan()" : "").AppendLine(");");
			return;
		}
		if (field.Type.IsObject && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var def) && def is TableDef table && table.PlainStruct)
		{
			_w.Append("Offset<").Append(field.Type.ReferencedName).Append("Ref> ").Append(local).Append(" = value.").Append(fieldName).Append(".HasValue ? ").Append(PlainName(table)).Append(".Serialize(ref builder, in value.").Append(fieldName).Append(".GetValueOrDefault()).AsOffset : default;").AppendLine();
			return;
		}
		if (field.Type.IsUnion)
		{
			EmitPlainUnionCreateLocal(field);
			return;
		}
		if (field.Type.IsVector)
			EmitPlainVectorCreateLocal(field);
	}

	void EmitPlainUnionReadAssign(FieldDef field)
	{
		if (field.Type.ReferencedName == null || !_schema.ByName.TryGetValue(field.Type.ReferencedName, out var def) || def is not UnionDef union)
			return;

		string fieldName = ToPascalCase(field.Name);
		string source = "__" + fieldName + "Source";
		string previous = "__" + fieldName + "Previous";
		string target = "__" + fieldName + "Target";
		bool refUnion = _refUnions.Contains(union.Name);
		_w.Append("var ").Append(source).Append(" = ").Append(fieldName).AppendLine(";");
		_w.Append("var ").Append(previous).Append(" = value.").Append(fieldName).AppendLine(";");
		_w.Append("var ").Append(target).Append(" = default(").Append(union.Name).AppendLine(");");
		_w.Append("if (").Append(source).AppendLine(".HasValue)");
		_w.OpenBlock();
		_w.Append("switch (").Append(fieldName).AppendLine("Type)");
		_w.AppendLine("{");
		foreach (var member in union.Members)
		{
			if (!TryGetPlainUnionMemberType(member, out string memberType, out var memberKind))
				continue;
			string memberName = member.Name;
			string refLocal = "__" + fieldName + memberName + "Ref";
			string valueLocal = "__" + fieldName + memberName;
			_w.Append("case ").Append(union.Name).Append("Kind.").Append(memberName).AppendLine(":");
			_w.IncreaseIndent();
			if (memberKind == PlainUnionMemberKind.PlainTable)
			{
				_w.Append("if (").Append(source).Append(".TryGetAs").Append(memberName).Append("(out var ").Append(refLocal).AppendLine("))");
				_w.OpenBlock();
				_w.Append("var ").Append(valueLocal).Append(" = ").Append(previous).Append('.').Append(memberName).AppendLine(".GetValueOrDefault();");
				_w.Append(refLocal).Append(".ToPlain(ref ").Append(valueLocal).AppendLine(");");
				_w.Append(target).Append(" = new(in ").Append(valueLocal).AppendLine(");");
				_w.CloseBlock();
			}
			else if (memberKind == PlainUnionMemberKind.AutoPlain)
			{
				_w.Append("if (").Append(source).Append(".TryGetAs").Append(memberName).Append("(out var ").Append(refLocal).AppendLine("))");
				_w.OpenBlock();
				_w.Append("var ").Append(valueLocal).Append(" = default(").Append(memberType).AppendLine(");");
				_w.Append(refLocal).Append(".ToPlain(ref ").Append(valueLocal).AppendLine(");");
				if (IsBlittablePlainUnion(union))
					_w.Append(target).Append(" = new(").Append(valueLocal).AppendLine(");");
				else
				{
					_w.Append(target).Append(".Kind = ").Append(union.Name).Append("Kind.").Append(memberName).AppendLine(";");
					_w.Append(target).Append('.').Append(memberName).Append(" = ").Append(valueLocal).AppendLine(";");
				}
				_w.CloseBlock();
			}
			else if (!refUnion)
			{
				_w.Append("if (").Append(source).Append(".TryGetValue(out ").Append(memberType).Append(' ').Append(valueLocal).AppendLine("))");
				_w.OpenBlock();
				_w.Append(target).Append(".Kind = ").Append(union.Name).Append("Kind.").Append(memberName).AppendLine(";");
				_w.Append(target).Append('.').Append(memberName).Append(" = ").Append(valueLocal).AppendLine(";");
				_w.CloseBlock();
			}
			_w.DecreaseIndent();
			_w.AppendLine("break;");
		}
		_w.AppendLine("}");
		_w.CloseBlock();
		_w.Append("value.").Append(fieldName).Append(" = ").Append(target).AppendLine(";");
	}

	void EmitPlainUnionCreateLocal(FieldDef field)
	{
		if (field.Type.ReferencedName == null || !_schema.ByName.TryGetValue(field.Type.ReferencedName, out var def) || def is not UnionDef union)
			return;

		string fieldName = ToPascalCase(field.Name);
		string local = "__" + fieldName;
		string typeLocal = local + "Type";
		string source = "value." + fieldName;
		_w.Append(union.Name).Append("Kind ").Append(typeLocal).Append(" = ").Append(source).AppendLine(".Kind;");
		_w.Append("int ").Append(local).AppendLine(" = 0;");
		_w.Append("switch (").Append(typeLocal).AppendLine(")");
		_w.AppendLine("{");
		foreach (var member in union.Members)
		{
			if (!TryGetPlainUnionMemberType(member, out _, out var memberKind))
				continue;
			string memberName = member.Name;
			string valueLocal = local + memberName;
			_w.Append("case ").Append(union.Name).Append("Kind.").Append(memberName).AppendLine(":");
			_w.IncreaseIndent();
			if (memberKind == PlainUnionMemberKind.PlainTable)
			{
				_w.Append("if (").Append(source).Append('.').Append(memberName).AppendLine(".HasValue)");
				_w.OpenBlock();
				_w.Append("var ").Append(valueLocal).Append(" = ").Append(source).Append('.').Append(memberName).AppendLine(".GetValueOrDefault();");
				_w.Append(local).Append(" = ").Append(member.TypeName).Append(".Serialize(ref builder, in ").Append(valueLocal).AppendLine(").BufferPos;");
				_w.CloseBlock();
				_w.AppendLine("else");
				_w.OpenBlock();
				_w.Append(typeLocal).AppendLine(" = default;");
				_w.CloseBlock();
			}
			else if (memberKind == PlainUnionMemberKind.AutoPlain)
			{
				if (IsBlittablePlainUnion(union))
					_w.Append(local).Append(" = ").Append(member.TypeName).Append(".Serialize(ref builder, in ").Append(source).Append('.').Append(memberName).AppendLine(").BufferPos;");
				else
				{
					_w.Append("if (").Append(source).Append('.').Append(memberName).AppendLine(".HasValue)");
					_w.OpenBlock();
					_w.Append("var ").Append(valueLocal).Append(" = ").Append(source).Append('.').Append(memberName).AppendLine(".GetValueOrDefault();");
					_w.Append(local).Append(" = ").Append(member.TypeName).Append(".Serialize(ref builder, in ").Append(valueLocal).AppendLine(").BufferPos;");
					_w.CloseBlock();
					_w.AppendLine("else");
					_w.OpenBlock();
					_w.Append(typeLocal).AppendLine(" = default;");
					_w.CloseBlock();
				}
			}
			else
			{
				_w.Append(typeLocal).AppendLine(" = default;");
			}
			_w.DecreaseIndent();
			_w.AppendLine("break;");
		}
		_w.AppendLine("default:");
		_w.IncreaseIndent();
		_w.Append(typeLocal).AppendLine(" = default;");
		_w.DecreaseIndent();
		_w.AppendLine("break;");
		_w.AppendLine("}");
	}

	void EmitPlainVectorCreateLocal(FieldDef field)
	{
		string fieldName = ToPascalCase(field.Name);
		string local = "__" + fieldName;
		string source = "value." + fieldName;
		if (field.Type.ElementBase.IsScalar() || (field.Type.ElementBase == SchemaBaseType.Obj && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var def) && def is StructDef))
		{
			_w.Append("VectorOffset ").Append(local).Append(" = ").Append(source).Append(" == null ? default : builder.CreateVector(").Append(source).Append(field.CustomCollection ? ".AsReadOnlySpan()" : "").AppendLine(");");
			return;
		}
		if (field.Type.ElementBase == SchemaBaseType.String)
		{
			_w.Append("VectorOffset ").Append(local).AppendLine(" = default;");
			_w.Append("if (").Append(source).AppendLine(" != null)");
			_w.OpenBlock();
			_w.Append("Span<int> __offsets = stackalloc int[").Append(source).Append(field.CustomCollection ? ".Count" : ".Length").AppendLine("];");
			_w.Append("for (int i = 0; i < __offsets.Length; i++) __offsets[i] = builder.CreateString(").Append(source).Append("[i]").Append(field.CustomCollection ? ".AsReadOnlySpan()" : "").AppendLine(");");
			_w.Append(local).AppendLine(" = builder.CreateVectorOfOffsets(__offsets);");
			_w.CloseBlock();
			return;
		}
		if (field.Type.ElementBase == SchemaBaseType.Obj && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var elementDef))
		{
			if (elementDef is EnumDef)
			{
				_w.Append("VectorOffset ").Append(local).Append(" = ").Append(source).Append(" == null ? default : builder.CreateVector(").Append(source).Append(field.CustomCollection ? ".AsReadOnlySpan()" : "").AppendLine(");");
				return;
			}
			if (elementDef is TableDef table && table.PlainStruct)
			{
				_w.Append("VectorOffset ").Append(local).AppendLine(" = default;");
				_w.Append("if (").Append(source).AppendLine(" != null)");
				_w.OpenBlock();
				_w.Append("Span<int> __offsets = stackalloc int[").Append(source).Append(field.CustomCollection ? ".Count" : ".Length").AppendLine("];");
				if (field.CustomCollection)
				{
					_w.AppendLine("for (int i = 0; i < __offsets.Length; i++)");
					_w.OpenBlock();
					_w.Append("var __item = ").Append(source).AppendLine("[i];");
					_w.Append("__offsets[i] = ").Append(PlainName(table)).AppendLine(".Serialize(ref builder, in __item).BufferPos;");
					_w.CloseBlock();
				}
				else
				{
					_w.Append("for (int i = 0; i < __offsets.Length; i++) __offsets[i] = ").Append(PlainName(table)).Append(".Serialize(ref builder, in ").Append(source).AppendLine("[i]).BufferPos;");
				}
				_w.Append(local).AppendLine(" = builder.CreateVectorOfOffsets(__offsets);");
				_w.CloseBlock();
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
			_w.Append(", ").Append(parameterName).Append("Type: __").Append(fieldName).Append("Type");
			_w.Append(", ").Append(parameterName).Append(": __").Append(fieldName);
			return;
		}

		_w.Append(", ").Append(parameterName).Append(": ").Append(PlainCreateArgument(field));
	}

	string RefUnionName(UnionDef union) => RefUnionName(union.Name);

	string RefUnionName(string unionName) => unionName + "Ref";

	enum PlainUnionMemberKind
	{
		None,
		Struct,
		Enum,
		AutoPlain,
		PlainTable,
	}

	bool CanEmitPlainUnion(UnionDef union)
	{
		foreach (var member in union.Members)
			if (!TryGetPlainUnionMemberType(member, out _))
				return false;
		return true;
	}

	bool IsBlittablePlainUnion(UnionDef union)
	{
		foreach (var member in union.Members)
		{
			if (!TryGetPlainUnionMemberType(member, out _, out var kind))
				return false;
			if (kind == PlainUnionMemberKind.PlainTable)
				return false;
		}
		return true;
	}

	bool IsFixedSizeTable(TableDef table)
	{
		foreach (var field in table.Fields)
		{
			if (field.Deprecated)
				continue;
			if (field.Type.Base == SchemaBaseType.String ||
			field.Type.Base == SchemaBaseType.Vector ||
			field.Type.Base == SchemaBaseType.Union)
				return false;
			if (field.Type.Base == SchemaBaseType.Obj &&
			field.Type.ReferencedName != null &&
			_schema.ByName.TryGetValue(field.Type.ReferencedName, out var def) &&
			def is TableDef)
				return false;
		}
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
		if (_autoPlainStructs.Contains(table.Name))
		{
			typeName = PlainName(table);
			kind = PlainUnionMemberKind.AutoPlain;
			return true;
		}
		return false;
	}
}
