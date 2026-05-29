using FlatBufferLite.SourceGen.IR;

namespace FlatBufferLite.SourceGen.Emit;

public sealed partial class CodeEmitter
{
	void EmitTableField(FieldDef f)
	{
		string propName = ToPascalCase(f.Name);
		string comment = SchemaComment("field", f.Name, propName, f.Location);
		int vto = f.VTableOffset;
		int absInline = f.InlineOffset + 4;

		if (f.Type.Base.IsScalar())
		{
			string cs = ScalarCSharpType(f.Type, out string defaultLiteral);
			string def = f.DefaultValue is { Length: > 0 } defaultValue ? FormatDefault(f.Type, defaultValue) : defaultLiteral;
			EmitScalarProperty(cs, cs, propName, vto, absInline, def, castUnderlying: null, comment);
			return;
		}
		if (f.Type.IsString)
		{
			EmitIndirectNewProperty("FlatString", propName, vto, comment);
			return;
		}
		if (f.Type.IsObject)
		{
			if (f.Type.ReferencedName == null)
				return;
			var name = f.Type.ReferencedName;
			if (_schema.ByName.TryGetValue(name, out var def))
			{
				if (def is TableDef)
				{
					EmitIndirectNewProperty(name + "Ref", propName, vto, comment);
					return;
				}
				if (def is StructDef)
				{
					EmitStructRefProperty(name, propName, vto, absInline, comment);
					return;
				}
				if (def is EnumDef ed)
				{
					string underlying = ed.Underlying.ToCSharpKeyword();
					string defLit = f.DefaultValue is { Length: > 0 } defaultValue
					? FormatEnumDefault(ed, defaultValue)
					: (ed.Underlying == SchemaBaseType.Long || ed.Underlying == SchemaBaseType.ULong ? "0L" : "0");
					EmitScalarProperty(name, underlying, propName, vto, absInline, defLit, castUnderlying: underlying, comment);
					return;
				}
			}
			else
			{
				EmitStructRefProperty(name, propName, vto, absInline, comment);
			}
			return;
		}
		if (f.Type.IsVector)
		{
			EmitVectorReader(f, propName, vto, comment);
			return;
		}
		if (f.Type.IsUnion)
		{
			if (f.Type.ReferencedName == null)
				return;
			var unionName = f.Type.ReferencedName;
			var unionTypeName = _refUnions.Contains(unionName) ? RefUnionName(unionName) : unionName;
			int typeVto = vto;
			int dataVto = vto + 2;
			int typeAbsInline = f.InlineOffset + 4;
			EmitCodeComment(SchemaComment("field", f.Name, propName + "Type, " + propName, f.Location));
			EmitScalarProperty(unionName + "Kind", "byte", propName + "Type", typeVto, typeAbsInline, "0", castUnderlying: "byte");
			if (_refUnions.Contains(unionName))
			{
				_w.Append("public ").Append(unionTypeName).Append(' ').Append(propName)
				.Append(" => new ").Append(unionTypeName).Append("(_buf, Vtable.ReadIndirect(_buf, _pos, ").Append(dataVto)
				.Append("), Vtable.Read<byte>(_buf, _pos, ").Append(typeVto).AppendLine(", 0));");
			}
			else
			{
				int dataAbsInline = f.UnionDataInlineOffset + 4;
				EmitStructRefProperty(unionName, propName, dataVto, dataAbsInline);
			}
		}
	}

	void EmitStructRefProperty(string typeName, string propName, int vto, int absInline, string? comment = null)
	{
		EmitCodeComment(comment);
		_w.Append("public ").Append(typeName).Append(' ').Append(propName)
		.Append(" { get => Vtable.StructOffset(_buf, _pos, ").Append(vto).Append(") is var o && o == 0 ? default : FlatBufferReader.ReadUnaligned<").Append(typeName).Append(">(_buf, o);")
		.Append(" set => Vtable.WriteForced<").Append(typeName).Append(">(_buf, _pos, ").Append(vto).Append(", ").Append(absInline).AppendLine(", in value); }");
	}

	void EmitScalarProperty(string publicType, string underlying, string propName, int vto, int absInline, string def, string? castUnderlying, string? comment = null)
	{
		EmitCodeComment(comment);
		_w.Append("public ").Append(publicType).Append(' ').Append(propName).Append(" { get => ");
		if (castUnderlying != null)
			_w.Append('(').Append(publicType).Append(")(");
		_w.Append("Vtable.Read<").Append(underlying).Append(">(_buf, _pos, ").Append(vto).Append(", ").Append(def).Append(')');
		if (castUnderlying != null)
			_w.Append(')');
		_w.Append("; set => Vtable.Write<").Append(underlying).Append(">(_buf, _pos, ").Append(vto).Append(", ").Append(absInline).Append(", ");
		if (castUnderlying != null)
			_w.Append('(').Append(underlying).Append(')');
		_w.Append("value, ").Append(def).AppendLine("); }");
	}

	void EmitVectorReader(FieldDef f, string propName, int vto, string? comment)
	{
		var elt = f.Type.ElementBase;
		if (elt.IsScalar())
		{
			string cs = elt.ToCSharpKeyword();
			EmitIndirectNewPropertyGeneric("FlatVector", cs, propName, vto, comment);
			if (elt == SchemaBaseType.UByte && f.IsFlexBuffer)
				_w.Append("public FlexBuffer ").Append(propName).Append("FlexBuffer => FlexBuffer.GetRoot(").Append(propName).AppendLine(".AsSpan);");
			if (elt == SchemaBaseType.UByte && f.NestedFlatBufferType is { Length: > 0 } nestedType && IsValidCSharpIdentifier(nestedType))
			{
				_w.Append("public ").Append(nestedType).Append("Ref ").Append(propName).Append("Nested => ").Append(nestedType).Append("Ref.GetRootAs(").Append(propName).AppendLine(".AsSpan);");
			}
			return;
		}
		if (elt == SchemaBaseType.String)
		{
			EmitIndirectNewProperty("FlatStringVector", propName, vto, comment);
			return;
		}
		if (elt == SchemaBaseType.Obj && f.Type.ReferencedName != null)
		{
			var name = f.Type.ReferencedName;
			if (_schema.ByName.TryGetValue(name, out var def))
			{
				if (def is StructDef)
				{
					EmitIndirectNewPropertyGeneric("FlatVector", name, propName, vto, comment);
					return;
				}
				if (def is TableDef)
				{
					EmitIndirectNewProperty(name + "RefVector", propName, vto, comment);
					return;
				}
				if (def is EnumDef ed)
				{
					string cs = ed.Underlying.ToCSharpKeyword();
					EmitIndirectNewPropertyGeneric("FlatVector", cs, propName, vto, comment);
					return;
				}
			}
		}
	}
}
