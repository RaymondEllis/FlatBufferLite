using FlatBufferLite.SourceGen.IR;

namespace FlatBufferLite.SourceGen.Emit;

public sealed partial class CodeEmitter
{
	void EmitTableVector(TableDef t)
	{
		_sb.AppendLine();
		_sb.Append("public readonly ref struct ").Append(t.Name).AppendLine("Vector");
		_sb.AppendLine("{");
		_sb.AppendLine("\treadonly Span<byte> _buf;");
		_sb.AppendLine("\treadonly int _pos;");
		_sb.Append("\tpublic ").Append(t.Name).AppendLine("Vector(Span<byte> buffer, int position) { _buf = buffer; _pos = position; }");
		_sb.AppendLine("\tpublic bool IsValid => _pos > 0;");
		_sb.AppendLine("\tpublic int Length => _pos <= 0 ? 0 : (int)FlatBufferReader.ReadUnaligned<uint>(_buf, _pos);");
		_sb.Append("\tpublic ").Append(t.Name).AppendLine(" this[int index]");
		_sb.AppendLine("\t{");
		_sb.AppendLine("\t\tget");
		_sb.AppendLine("\t\t{");
		_sb.AppendLine("\t\t\tint elt = _pos + 4 + index * 4;");
		_sb.Append("\t\t\treturn new ").Append(t.Name).AppendLine("(_buf, elt + (int)FlatBufferReader.ReadUnaligned<uint>(_buf, elt));");
		_sb.AppendLine("\t\t}");
		_sb.AppendLine("\t}");

		FieldDef? keyField = null;
		foreach (var f in t.Fields)
		{
			if (!f.Deprecated && f.IsKey)
			{
				keyField = f;
				break;
			}
		}

		if (keyField != null)
			EmitLookupByKey(t, keyField);

		_sb.AppendLine("}");
	}

	void EmitLookupByKey(TableDef t, FieldDef keyField)
	{
		string propName = ToPascalCase(keyField.Name);
		bool isString = keyField.Type.IsString;
		string keyType = isString ? "ReadOnlySpan<byte>" : ScalarCSharpType(keyField.Type, out _);

		_sb.AppendLine();
		_sb.Append("\tpublic ").Append(t.Name).Append(" LookupByKey(").Append(keyType).AppendLine(" key)");
		_sb.AppendLine("\t{");
		_sb.AppendLine("\t\tint lo = 0, hi = Length - 1;");
		_sb.AppendLine("\t\twhile (lo <= hi)");
		_sb.AppendLine("\t\t{");
		_sb.AppendLine("\t\t\tint mid = (lo + hi) >> 1;");
		_sb.AppendLine("\t\t\tvar entry = this[mid];");
		if (isString)
		{
			_sb.Append("\t\t\tint cmp = entry.").Append(propName).AppendLine(".AsBytes.SequenceCompareTo(key);");
			_sb.AppendLine("\t\t\tif (cmp == 0) return entry;");
			_sb.AppendLine("\t\t\tif (cmp < 0) lo = mid + 1; else hi = mid - 1;");
		}
		else
		{
			_sb.Append("\t\t\tvar k = entry.").Append(propName).AppendLine(";");
			_sb.AppendLine("\t\t\tif (k == key) return entry;");
			_sb.AppendLine("\t\t\tif (k < key) lo = mid + 1; else hi = mid - 1;");
		}
		_sb.AppendLine("\t\t}");
		_sb.AppendLine("\t\treturn default;");
		_sb.AppendLine("\t}");
	}
}
