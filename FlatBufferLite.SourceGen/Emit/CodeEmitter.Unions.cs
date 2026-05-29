using FlatBufferLite.SourceGen.IR;

namespace FlatBufferLite.SourceGen.Emit;

public sealed partial class CodeEmitter
{
	void PreclassifyUnion(UnionDef u)
	{
		if (u.Members.Count == 0)
		{
			_refUnions.Add(u.Name);
			return;
		}
		bool isRef = false;
		foreach (var m in u.Members)
		{
			if (!(_schema.ByName.TryGetValue(m.TypeName, out var def) && (def is StructDef || def is EnumDef)))
				isRef = true;
		}
		if (!isRef)
			return;
		_refUnions.Add(u.Name);
		foreach (var m in u.Members)
		{
			if (_schema.ByName.TryGetValue(m.TypeName, out var def) &&
			def is TableDef table && !table.PlainStruct && IsFixedSizeTable(table))
				_autoPlainStructs.Add(table.Name);
		}
	}

	void EmitUnion(UnionDef u)
	{
		_w.AppendLine();
		EmitSchemaComment("union", u.Name, u.Name + "Kind", u.Location);
		_w.OpenBlock("public enum " + u.Name + "Kind : byte");
		_w.AppendLine("NONE = 0,");
		foreach (var m in u.Members)
			_w.Append(m.Name).Append(" = ").Append(m.Tag).AppendLine(",");
		_w.CloseBlock();

		bool allUnmanaged = u.Members.Count > 0;
		bool allTables = u.Members.Count > 0;
		int maxSize = 0;
		int maxAlign = 1;
		foreach (var m in u.Members)
		{
			if (_schema.ByName.TryGetValue(m.TypeName, out var def))
			{
				if (def is StructDef sd)
				{
					allTables = false;
					if (sd.Size > maxSize)
						maxSize = sd.Size;
					if (sd.Alignment > maxAlign)
						maxAlign = sd.Alignment;
				}
				else if (def is TableDef)
				{
					allUnmanaged = false;
				}
				else if (def is EnumDef ed)
				{
					allTables = false;
					int sz = ed.Underlying.InlineSize();
					if (sz > maxSize)
						maxSize = sz;
					if (sz > maxAlign)
						maxAlign = sz;
				}
				else
				{
					allUnmanaged = false;
					allTables = false;
				}
			}
			else
			{
				allUnmanaged = false;
			}
		}

		if (allUnmanaged)
			EmitContiguousUnion(u, maxSize, maxAlign);
		else
		{
			_refUnions.Add(u.Name);
			EmitRefUnion(u, allTables);
		}
	}

	void EmitContiguousUnion(UnionDef u, int maxSize, int maxAlign)
	{
		int tagOffset = maxSize;
		int totalSize = AlignUp(tagOffset + 1, maxAlign > 0 ? maxAlign : 1);

		_w.AppendLine();
		EmitSchemaComment("union", u.Name, u.Name, u.Location);
		_w.AppendLine("[Union]");
		EmitStructLayoutExplicit(totalSize);
		_w.AppendLine("public struct " + u.Name + " : IUnion");
		_w.OpenBlock();
		EmitFieldOffsetField(tagOffset, "byte", "Tag");
		foreach (var m in u.Members)
			EmitFieldOffsetField(0, m.TypeName, m.Name);
		_w.AppendLine();
		_w.AppendLine("public object? Value => throw new NotImplementedException(\"No boxing allowed.\");");
		_w.OpenBlock("public readonly bool HasValue => Tag switch");
		foreach (var m in u.Members)
			_w.Append(m.Tag).AppendLine(" => true,");
		_w.AppendLine("_ => false,");
		_w.CloseBlock(";");
		foreach (var m in u.Members)
		{
			_w.AppendLine();
			_w.Append("public ").Append(u.Name).Append('(').Append(m.TypeName).AppendLine(" value)");
			_w.OpenBlock();
			_w.Append("var u = default(").Append(u.Name).AppendLine(");");
			_w.Append("u.").Append(m.Name).AppendLine(" = value;");
			_w.Append("u.Tag = ").Append(m.Tag).AppendLine(";");
			_w.AppendLine("this = u;");
			_w.CloseBlock();
			_w.AppendLine();
			_w.Append("public static implicit operator ").Append(u.Name).Append('(').Append(m.TypeName).AppendLine(" value) => new(value);");
			_w.AppendLine();
			_w.Append("public readonly bool TryGetValue(out ").Append(m.TypeName).AppendLine(" value)");
			_w.OpenBlock();
			_w.Append("if (Tag == ").Append(m.Tag).Append(") { value = ").Append(m.Name).AppendLine("; return true; }");
			_w.AppendLine("value = default;");
			_w.AppendLine("return false;");
			_w.CloseBlock();
		}
		_w.CloseBlock();
	}

	void EmitRefUnion(UnionDef u, bool allTables)
	{
		string refUnionName = RefUnionName(u);
		_w.AppendLine();
		EmitSchemaComment("union", u.Name, refUnionName, u.Location);
		_w.AppendLine("[Union]");
		_w.AppendLine("public readonly ref struct " + refUnionName + " : IUnion");
		_w.OpenBlock();
		EmitReadonlyField("Span<byte>", "_buf");
		EmitReadonlyField("int", "_pos");
		EmitPublicReadonlyField("byte", "Tag");
		_w.Append("public ").Append(refUnionName).AppendLine("(Span<byte> buffer, int position, byte tag) { _buf = buffer; _pos = position; Tag = tag; }");
		foreach (var m in u.Members)
		{
			if (!_schema.ByName.TryGetValue(m.TypeName, out var memberDef) || memberDef is not TableDef)
				continue;
			if (allTables)
			{
				_w.Append("public ").Append(refUnionName).Append('(').Append(m.TypeName).Append("Ref").Append(" value) { _buf = value.Buffer; _pos = value.BufferPos; Tag = ").Append(m.Tag).AppendLine("; }");
				_w.Append("public static implicit operator ").Append(refUnionName).Append('(').Append(m.TypeName).Append("Ref value) => new(value);").AppendLine();
			}
		}
		_w.AppendLine("public object? Value => throw new NotImplementedException(\"No boxing allowed.\");");
		_w.AppendLine("public bool HasValue => Tag != 0 && _pos > 0;");
		foreach (var m in u.Members)
		{
			if (!_schema.ByName.TryGetValue(m.TypeName, out var memberDef) || memberDef is not TableDef)
				continue;

			_w.AppendLine();
			_w.Append("public bool TryGetAs").Append(m.Name).Append("(out ").Append(m.TypeName).AppendLine("Ref value)");
			_w.OpenBlock();
			_w.Append("if (Tag != ").Append(m.Tag).AppendLine(") { value = default; return false; }");
			_w.Append("value = new ").Append(m.TypeName).AppendLine("Ref(_buf, _pos);");
			_w.AppendLine("return true;");
			_w.CloseBlock();
		}
		_w.CloseBlock();
	}

	void EmitPlainUnion(UnionDef u)
	{
		if (!_refUnions.Contains(u.Name))
			return;
		if (!CanEmitPlainUnion(u))
		{
			foreach (var m in u.Members)
				if (!TryGetPlainUnionMemberType(m, out _, out _))
					_schema.Warnings.Add($"Union '{u.Name}' member '{m.Name}' ('{m.TypeName}') is not a fixed-size table; plain union '{u.Name}' will not be generated.");
			return;
		}

		bool allBlittable = true;
		int maxSize = 0, maxAlign = 1;
		foreach (var m in u.Members)
		{
			if (!TryGetPlainUnionMemberType(m, out _, out var kind))
				continue;
			switch (kind)
			{
				case PlainUnionMemberKind.Struct when _schema.ByName[m.TypeName] is StructDef sd:
					if (sd.Size > maxSize)
						maxSize = sd.Size;
					if (sd.Alignment > maxAlign)
						maxAlign = sd.Alignment;
					break;
				case PlainUnionMemberKind.Enum when _schema.ByName[m.TypeName] is EnumDef ed:
					int sz = ed.Underlying.InlineSize();
					if (sz > maxSize)
						maxSize = sz;
					if (sz > maxAlign)
						maxAlign = sz;
					break;
				case PlainUnionMemberKind.AutoPlain when _schema.ByName[m.TypeName] is TableDef td:
					if (td.InlineSize > maxSize)
						maxSize = td.InlineSize;
					if (td.InlineAlign > maxAlign)
						maxAlign = td.InlineAlign;
					break;
				default:
					allBlittable = false;
					break;
			}
		}

		_w.AppendLine();
		EmitSchemaComment("union", u.Name, u.Name + " plain struct", u.Location);
		_w.AppendLine("[Union]");

		if (allBlittable)
		{
			int kindOffset = maxSize;
			int totalSize = AlignUp(kindOffset + 1, maxAlign > 0 ? maxAlign : 1);
			EmitStructLayoutExplicit(totalSize);
			_w.AppendLine("public readonly partial struct " + u.Name + " : IUnion");
			_w.OpenBlock();
			EmitFieldOffsetField(kindOffset, "public readonly", u.Name + "Kind", "Kind");
			foreach (var m in u.Members)
			{
				if (!TryGetPlainUnionMemberType(m, out string memberType, out _))
					continue;
				EmitFieldOffsetField(0, "public readonly", memberType, m.Name);
			}
			_w.AppendLine();
			_w.AppendLine("public object? Value => throw new NotImplementedException(\"No boxing allowed.\");");
			_w.Append("public readonly bool HasValue => Kind != ").Append(u.Name).AppendLine("Kind.NONE;");
			foreach (var m in u.Members)
			{
				if (!TryGetPlainUnionMemberType(m, out string memberType, out _))
					continue;
				_w.AppendLine();
				_w.Append("public ").Append(u.Name).Append('(').Append(memberType).AppendLine(" value)");
				_w.OpenBlock();
				_w.AppendLine("this = default;");
				_w.Append(m.Name).AppendLine(" = value;");
				_w.Append("Kind = ").Append(u.Name).Append("Kind.").Append(m.Name).AppendLine(";");
				_w.CloseBlock();
				_w.AppendLine();
				_w.Append("public static implicit operator ").Append(u.Name).Append('(').Append(memberType).AppendLine(" value) => new(value);");
				_w.AppendLine();
				_w.Append("public readonly bool TryGetValue(out ").Append(memberType).AppendLine(" value)");
				_w.OpenBlock();
				_w.Append("if (Kind == ").Append(u.Name).Append("Kind.").Append(m.Name).Append(") { value = ").Append(m.Name).AppendLine("; return true; }");
				_w.AppendLine("value = default;");
				_w.AppendLine("return false;");
				_w.CloseBlock();
			}
			_w.CloseBlock();
		}
		else
		{
			_w.AppendLine("public readonly partial struct " + u.Name + " : IUnion");
			_w.OpenBlock();
			EmitPublicReadonlyField(u.Name + "Kind", "Kind");
			foreach (var m in u.Members)
			{
				if (!TryGetPlainUnionMemberType(m, out string memberType, out _))
					continue;
				EmitPublicReadonlyField(memberType + "?", m.Name);
			}
			_w.AppendLine();
			_w.AppendLine("public object? Value => throw new NotImplementedException(\"No boxing allowed.\");");
			_w.OpenBlock("public readonly bool HasValue => Kind switch");
			foreach (var m in u.Members)
				if (TryGetPlainUnionMemberType(m, out _, out _))
					_w.Append(u.Name).Append("Kind.").Append(m.Name).Append(" => ").Append(m.Name).AppendLine(".HasValue,");
			_w.AppendLine("_ => false,");
			_w.CloseBlock(";");
			foreach (var m in u.Members)
			{
				if (!TryGetPlainUnionMemberType(m, out string memberType, out _))
					continue;
				_w.AppendLine();
				_w.Append("public ").Append(u.Name).Append("(in ").Append(memberType).AppendLine(" value)");
				_w.OpenBlock();
				_w.AppendLine("this = default;");
				_w.Append(m.Name).AppendLine(" = value;");
				_w.Append("Kind = ").Append(u.Name).Append("Kind.").Append(m.Name).AppendLine(";");
				_w.CloseBlock();
				_w.AppendLine();
				_w.Append("public static implicit operator ").Append(u.Name).Append('(').Append(memberType).AppendLine(" value) => new(in value);");
				_w.AppendLine();
				_w.Append("public readonly bool TryGetValue(out ").Append(memberType).AppendLine(" value)");
				_w.OpenBlock();
				_w.Append("if (Kind == ").Append(u.Name).Append("Kind.").Append(m.Name).Append(" && ").Append(m.Name).AppendLine(".HasValue)");
				_w.OpenBlock();
				_w.Append("value = ").Append(m.Name).AppendLine(".GetValueOrDefault();");
				_w.AppendLine("return true;");
				_w.CloseBlock();
				_w.AppendLine("value = default;");
				_w.AppendLine("return false;");
				_w.CloseBlock();
			}
			_w.CloseBlock();
		}
	}
}
