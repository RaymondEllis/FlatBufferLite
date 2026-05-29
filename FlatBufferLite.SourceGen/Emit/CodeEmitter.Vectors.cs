using FlatBufferLite.SourceGen.IR;

namespace FlatBufferLite.SourceGen.Emit;

public sealed partial class CodeEmitter
{
	void EmitTableVector(TableDef t)
	{
		_w.AppendLine();
		EmitSchemaComment("table", t.Name, t.Name + "RefVector", t.Location);
		_w.AppendLine("public readonly ref struct " + t.Name + "RefVector");
		_w.OpenBlock();
		EmitReadonlyField("Span<byte>", "_buf");
		EmitReadonlyField("int", "_pos");
		_w.Append("public ").Append(t.Name).Append("Ref").AppendLine("Vector(Span<byte> buffer, int position) { _buf = buffer; _pos = position; }");
		_w.AppendLine("public bool IsValid => _pos > 0;");
		_w.AppendLine("public int Length => _pos <= 0 ? 0 : (int)FlatBufferReader.ReadUnaligned<uint>(_buf, _pos);");
		_w.Append("public ").Append(t.Name).Append("Ref").AppendLine(" this[int index]");
		_w.OpenBlock();
		_w.AppendLine("get");
		_w.OpenBlock();
		_w.AppendLine("int elt = _pos + 4 + index * 4;");
		EmitReturnTableRef(t, "_buf", "elt + (int)FlatBufferReader.ReadUnaligned<uint>(_buf, elt)");
		_w.CloseBlock();
		_w.CloseBlock();

		if (t.PlainStruct)
		{
			_w.AppendLine();
			_w.Append("public void CopyTo(Span<").Append(t.Name).AppendLine("> destination)");
			_w.OpenBlock();
			_w.AppendLine("int length = Length;");
			_w.AppendLine("System.Diagnostics.Debug.Assert(destination.Length >= length);");
			_w.AppendLine("for (int i = 0; i < length; i++)");
			_w.OpenBlock();
			_w.AppendLine("this[i].ToPlain(ref destination[i]);");
			_w.CloseBlock();
			_w.CloseBlock();
		}

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

		_w.CloseBlock();
	}

	void EmitLookupByKey(TableDef t, FieldDef keyField)
	{
		string propName = ToPascalCase(keyField.Name);
		bool isString = keyField.Type.IsString;
		string keyType = isString ? "ReadOnlySpan<byte>" : ScalarCSharpType(keyField.Type, out _);

		_w.AppendLine();
		_w.Append("public ").Append(t.Name).Append("Ref").Append(" LookupByKey(").Append(keyType).AppendLine(" key)");
		_w.OpenBlock();
		_w.AppendLine("int lo = 0, hi = Length - 1;");
		_w.AppendLine("while (lo <= hi)");
		_w.OpenBlock();
		_w.AppendLine("int mid = (lo + hi) >> 1;");
		_w.AppendLine("var entry = this[mid];");
		if (isString)
		{
			_w.Append("int cmp = entry.").Append(propName).AppendLine(".AsBytes.SequenceCompareTo(key);");
			_w.AppendLine("if (cmp == 0) return entry;");
			_w.AppendLine("if (cmp < 0) lo = mid + 1; else hi = mid - 1;");
		}
		else
		{
			_w.Append("var k = entry.").Append(propName).AppendLine(";");
			_w.AppendLine("if (k == key) return entry;");
			_w.AppendLine("if (k < key) lo = mid + 1; else hi = mid - 1;");
		}
		_w.CloseBlock();
		_w.AppendLine("return default;");
		_w.CloseBlock();
	}
}
