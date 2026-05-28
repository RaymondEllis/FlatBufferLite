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
		foreach (var m in u.Members)
		{
			bool isManaged = _schema.ByName.TryGetValue(m.TypeName, out var def)
				&& (def is StructDef || def is EnumDef);
			if (!isManaged)
			{
				_refUnions.Add(u.Name);
				return;
			}
		}
	}

	void EmitUnion(UnionDef u)
	{
		_sb.AppendLine();
		_sb.Append("public enum ").Append(u.Name).AppendLine("Kind : byte");
		_sb.AppendLine("{");
		_sb.AppendLine("\tNONE = 0,");
		foreach (var m in u.Members)
			_sb.Append('\t').Append(m.Name).Append(" = ").Append(m.Tag).AppendLine(",");
		_sb.AppendLine("}");

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

		_sb.AppendLine();
		_sb.AppendLine("[Union]");
		_sb.Append("[StructLayout(LayoutKind.Explicit, Size = ").Append(totalSize).AppendLine(")]");
		_sb.Append("public struct ").Append(u.Name).AppendLine(" : IUnion");
		_sb.AppendLine("{");
		_sb.Append("\t[FieldOffset(").Append(tagOffset).AppendLine(")] public byte Tag;");
		foreach (var m in u.Members)
			_sb.Append("\t[FieldOffset(0)] public ").Append(m.TypeName).Append(' ').Append(m.Name).AppendLine(";");
		_sb.AppendLine();
		_sb.AppendLine("\tpublic object? Value => throw new NotImplementedException(\"Boxing not supported. Use TryGetValue.\");");
		_sb.AppendLine("\tpublic readonly bool HasValue => Tag != 0;");
		foreach (var m in u.Members)
		{
			_sb.AppendLine();
			_sb.Append("\tpublic ").Append(u.Name).Append('(').Append(m.TypeName).AppendLine(" value)");
			_sb.AppendLine("\t{");
			_sb.Append("\t\tvar u = default(").Append(u.Name).AppendLine(");");
			_sb.Append("\t\tu.").Append(m.Name).AppendLine(" = value;");
			_sb.Append("\t\tu.Tag = ").Append(m.Tag).AppendLine(";");
			_sb.AppendLine("\t\tthis = u;");
			_sb.AppendLine("\t}");

			_sb.AppendLine();
			_sb.Append("\tpublic static ").Append(u.Name).Append(" From").Append(m.Name).Append('(').Append(m.TypeName).AppendLine(" value)");
			_sb.AppendLine("\t{");
			_sb.Append("\t\tvar u = default(").Append(u.Name).AppendLine(");");
			_sb.Append("\t\tu.").Append(m.Name).AppendLine(" = value;");
			_sb.Append("\t\tu.Tag = ").Append(m.Tag).AppendLine(";");
			_sb.AppendLine("\t\treturn u;");
			_sb.AppendLine("\t}");

			_sb.AppendLine();
			_sb.Append("\tpublic readonly bool TryGetValue(out ").Append(m.TypeName).AppendLine(" value)");
			_sb.AppendLine("\t{");
			_sb.Append("\t\tif (Tag == ").Append(m.Tag).Append(") { value = ").Append(m.Name).AppendLine("; return true; }");
			_sb.AppendLine("\t\tvalue = default;");
			_sb.AppendLine("\t\treturn false;");
			_sb.AppendLine("\t}");
		}
		_sb.AppendLine("}");
	}

	void EmitRefUnion(UnionDef u, bool allTables)
	{
		_sb.AppendLine();
		_sb.AppendLine("[Union]");
		_sb.Append("public readonly ref struct ").Append(u.Name).AppendLine(" : IUnion");
		_sb.AppendLine("{");
		_sb.AppendLine("\treadonly Span<byte> _buf;");
		_sb.AppendLine("\treadonly int _pos;");
		_sb.AppendLine("\tpublic readonly byte Tag;");
		_sb.Append("\tpublic ").Append(u.Name).AppendLine("(Span<byte> buffer, int position, byte tag) { _buf = buffer; _pos = position; Tag = tag; }");
		if (allTables)
			foreach (var m in u.Members)
				_sb.Append("\tpublic ").Append(u.Name).Append('(').Append(m.TypeName).Append(" value) { _buf = value.Buffer; _pos = value.BufferPos; Tag = ").Append(m.Tag).AppendLine("; }");
		_sb.Append("\tpublic object? Value => throw new NotImplementedException(\"Boxing not supported").Append(allTables ? ". Use TryGetAs." : ".").AppendLine("\");");
		_sb.AppendLine("\tpublic bool HasValue => Tag != 0 && _pos > 0;");
		if (allTables)
			foreach (var m in u.Members)
			{
				_sb.AppendLine();
				_sb.Append("\tpublic bool TryGetAs").Append(m.Name).Append("(out ").Append(m.TypeName).AppendLine(" value)");
				_sb.AppendLine("\t{");
				_sb.Append("\t\tif (Tag != ").Append(m.Tag).AppendLine(") { value = default; return false; }");
				_sb.Append("\t\tvalue = new ").Append(m.TypeName).AppendLine("(_buf, _pos);");
				_sb.AppendLine("\t\treturn true;");
				_sb.AppendLine("\t}");
			}
		_sb.AppendLine("}");
	}
}