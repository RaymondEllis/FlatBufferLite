using FlatBufferLite.SourceGen.IR;

namespace FlatBufferLite.SourceGen.Emit;

public sealed partial class CodeEmitter
{
	void EmitTableField(FieldDef f)
	{
		string propName = ToPascalCase(f.Name);
		int vto = f.VTableOffset;
		int absInline = f.InlineOffset + 4;

		if (f.Type.Base.IsScalar())
		{
			string cs = ScalarCSharpType(f.Type, out string defaultLiteral);
			string def = !string.IsNullOrEmpty(f.DefaultValue) ? FormatDefault(f.Type, f.DefaultValue!) : defaultLiteral;
			EmitScalarProperty(cs, cs, propName, vto, absInline, def, castUnderlying: null);
			return;
		}
		if (f.Type.IsString)
		{
			_sb.Append("\tpublic FlatString ").Append(propName).Append(" => new FlatString(_buf, Vtable.ReadIndirect(_buf, _pos, ").Append(vto).AppendLine("));");
			return;
		}
		if (f.Type.IsObject)
		{
			var name = f.Type.ReferencedName!;
			if (_schema.ByName.TryGetValue(name, out var def))
			{
				if (def is TableDef)
				{
					_sb.Append("\tpublic ").Append(name).Append("Ref").Append(' ').Append(propName).Append(" => new ").Append(name).Append("Ref").Append("(_buf, Vtable.ReadIndirect(_buf, _pos, ").Append(vto).AppendLine("));");
					return;
				}
				if (def is StructDef)
				{
					EmitStructRefProperty(name, propName, vto, absInline);
					return;
				}
				if (def is EnumDef ed)
				{
					string underlying = ed.Underlying.ToCSharpKeyword();
					string defLit = !string.IsNullOrEmpty(f.DefaultValue)
						? FormatEnumDefault(ed, f.DefaultValue!)
						: (ed.Underlying == SchemaBaseType.Long || ed.Underlying == SchemaBaseType.ULong ? "0L" : "0");
					EmitScalarProperty(name, underlying, propName, vto, absInline, defLit, castUnderlying: underlying);
					return;
				}
			}
			else
			{
				EmitStructRefProperty(name, propName, vto, absInline);
			}
			return;
		}
		if (f.Type.IsVector)
		{
			EmitVectorReader(f, propName, vto);
			return;
		}
		if (f.Type.IsUnion)
		{
			var unionName = f.Type.ReferencedName!;
			int typeVto = vto;
			int dataVto = vto + 2;
			int typeAbsInline = f.InlineOffset + 4;
			EmitScalarProperty(unionName + "Kind", "byte", propName + "Type", typeVto, typeAbsInline, "0", castUnderlying: "byte");
			if (_refUnions.Contains(unionName))
			{
				_sb.Append("\tpublic ").Append(unionName).Append(' ').Append(propName)
					.Append(" => new ").Append(unionName).Append("(_buf, Vtable.ReadIndirect(_buf, _pos, ").Append(dataVto)
					.Append("), Vtable.Read<byte>(_buf, _pos, ").Append(typeVto).AppendLine(", 0));");
			}
			else
			{
				int dataAbsInline = f.UnionDataInlineOffset + 4;
				EmitStructRefProperty(unionName, propName, dataVto, dataAbsInline);
			}
		}
	}

	void EmitStructRefProperty(string typeName, string propName, int vto, int absInline)
	{
		_sb.Append("\tpublic ").Append(typeName).Append(' ').Append(propName)
			.Append(" { get => Vtable.StructOffset(_buf, _pos, ").Append(vto).Append(") is var o && o == 0 ? default : FlatBufferReader.ReadUnaligned<").Append(typeName).Append(">(_buf, o);")
			.Append(" set => Vtable.WriteForced<").Append(typeName).Append(">(_buf, _pos, ").Append(vto).Append(", ").Append(absInline).AppendLine(", in value); }");
	}

	void EmitScalarProperty(string publicType, string underlying, string propName, int vto, int absInline, string def, string? castUnderlying)
	{
		_sb.Append("\tpublic ").Append(publicType).Append(' ').Append(propName).Append(" { get => ");
		if (castUnderlying != null)
			_sb.Append('(').Append(publicType).Append(")(");
		_sb.Append("Vtable.Read<").Append(underlying).Append(">(_buf, _pos, ").Append(vto).Append(", ").Append(def).Append(')');
		if (castUnderlying != null)
			_sb.Append(')');
		_sb.Append("; set => Vtable.Write<").Append(underlying).Append(">(_buf, _pos, ").Append(vto).Append(", ").Append(absInline).Append(", ");
		if (castUnderlying != null)
			_sb.Append('(').Append(underlying).Append(')');
		_sb.Append("value, ").Append(def).AppendLine("); }");
	}

	void EmitVectorReader(FieldDef f, string propName, int vto)
	{
		var elt = f.Type.ElementBase;
		if (elt.IsScalar())
		{
			string cs = elt.ToCSharpKeyword();
			_sb.Append("\tpublic FlatVector<").Append(cs).Append("> ").Append(propName).Append(" => new FlatVector<").Append(cs).Append(">(_buf, Vtable.ReadIndirect(_buf, _pos, ").Append(vto).AppendLine("));");
			if (elt == SchemaBaseType.UByte && !string.IsNullOrEmpty(f.NestedFlatBufferType) && IsValidCSharpIdentifier(f.NestedFlatBufferType!))
				_sb.Append("\tpublic ").Append(f.NestedFlatBufferType).Append("Ref ").Append(propName).Append("Nested => ").Append(f.NestedFlatBufferType).Append("Ref.GetRootAs(").Append(propName).AppendLine(".AsSpan);");
			return;
		}
		if (elt == SchemaBaseType.String)
		{
			_sb.Append("\tpublic FlatStringVector ").Append(propName).Append(" => new FlatStringVector(_buf, Vtable.ReadIndirect(_buf, _pos, ").Append(vto).AppendLine("));");
			return;
		}
		if (elt == SchemaBaseType.Obj && f.Type.ReferencedName != null)
		{
			var name = f.Type.ReferencedName;
			if (_schema.ByName.TryGetValue(name, out var def))
			{
				if (def is StructDef)
				{
					_sb.Append("\tpublic FlatVector<").Append(name).Append("> ").Append(propName).Append(" => new FlatVector<").Append(name).Append(">(_buf, Vtable.ReadIndirect(_buf, _pos, ").Append(vto).AppendLine("));");
					return;
				}
				if (def is TableDef)
				{
					_sb.Append("\tpublic ").Append(name).Append("Ref").Append("Vector ").Append(propName).Append(" => new ").Append(name).Append("Ref").Append("Vector(_buf, Vtable.ReadIndirect(_buf, _pos, ").Append(vto).AppendLine("));");
					return;
				}
				if (def is EnumDef ed)
				{
					string cs = ed.Underlying.ToCSharpKeyword();
					_sb.Append("\tpublic FlatVector<").Append(cs).Append("> ").Append(propName).Append(" => new FlatVector<").Append(cs).Append(">(_buf, Vtable.ReadIndirect(_buf, _pos, ").Append(vto).AppendLine("));");
					return;
				}
			}
		}
	}
}
