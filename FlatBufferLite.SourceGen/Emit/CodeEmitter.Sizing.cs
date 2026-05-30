using FlatBufferLite.SourceGen.IR;
using System.Collections.Generic;
using System.Globalization;

namespace FlatBufferLite.SourceGen.Emit;

public sealed partial class CodeEmitter
{
	void EmitSizeStruct(TableDef table)
	{
		EmitSizeStructHeader(SizeTypeName(table));
		_w.CloseBlock();
	}

	void EmitSizeStruct(UnionDef union)
	{
		string sizeTypeName = SizeTypeName(union);
		EmitSizeStructHeader(sizeTypeName);
		foreach (var member in union.Members)
		{
			if (!_schema.ByName.TryGetValue(member.TypeName, out var memberDef) || memberDef is not TableDef table || IsRootTable(table))
				continue;
			_w.Append("public static implicit operator ").Append(sizeTypeName).Append('(').Append(SizeTypeName(table)).AppendLine(" size) => new(size.Value);");
		}
		_w.CloseBlock();
	}

	void EmitSizeStructHeader(string sizeTypeName)
	{
		_w.AppendLine();
		_w.Append("public readonly struct ").AppendLine(sizeTypeName);
		_w.OpenBlock();
		_w.AppendLine("public readonly int Value;");
		_w.Append("public ").Append(sizeTypeName).AppendLine("(int value) => Value = value;");
		_w.Append("public static implicit operator int(").Append(sizeTypeName).AppendLine(" size) => size.Value;");
	}

	void EmitGetMaxSize(TableDef table)
	{
		var parameters = new List<string>();
		var paramDocs = new List<(string Name, string? Description)>();
		var terms = new List<string>();
		bool isRootTable = IsRootTable(table);
		int constantSize = FixedTableSize(table);

		void AddParam(string param, string name, string? description = null)
		{
			parameters.Add(param);
			paramDocs.Add((name, description));
		}

		foreach (var field in table.Fields)
		{
			if (field.Deprecated)
				continue;
			string parameterPrefix = ToCamelCase(field.Name);
			if (field.Type.IsString)
			{
				string byteCountParameter = parameterPrefix + "ByteCount";
				AddParam(IntParameter(byteCountParameter), byteCountParameter, "UTF-8 byte length of the " + field.Name + " string content.");
				terms.Add(byteCountParameter + " + 8");
			}
			else if (field.Type.IsUnion)
			{
				if (field.Type.ReferencedName != null && _refUnions.Contains(field.Type.ReferencedName) && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var unionDef) && unionDef is UnionDef union)
				{
					if (TryGetKnownRefUnionPayloadSize(union, new HashSet<string>()) is int knownPayloadSize)
						constantSize += knownPayloadSize;
					else
					{
						string maxSizeParameter = parameterPrefix + "MaxSize";
						AddParam(SizeParameter(maxSizeParameter, union), maxSizeParameter, "Size budget for " + field.Name + ". Use " + union.Name + "Size implicit conversion from member GetMaxSize(...) result.");
						terms.Add(maxSizeParameter);
					}
				}
			}
			else if (field.Type.IsObject && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var fieldDef))
			{
				if (fieldDef is TableDef nestedTable)
				{
					if (TryGetKnownTableSize(nestedTable, new HashSet<string>()) is int knownPayloadSize)
						constantSize += knownPayloadSize;
					else
					{
						string maxSizeParameter = parameterPrefix + "MaxSize";
						AddParam(SizeParameter(maxSizeParameter, nestedTable), maxSizeParameter, "Size budget for " + field.Name + ". Use " + nestedTable.Name + "Ref.GetMaxSize(...) to calculate.");
						terms.Add(maxSizeParameter);
					}
				}
			}
			else if (field.Type.IsVector)
			{
				string countParameter = parameterPrefix + "Count";
				var elementType = field.Type.ElementBase;
				if (elementType.IsScalar())
				{
					AddParam(IntParameter(countParameter), countParameter);
					terms.Add(VectorSizeTerm(countParameter, elementType.InlineSize()));
				}
				else if (elementType == SchemaBaseType.String)
				{
					string byteCountParameter = parameterPrefix + "ByteCount";
					AddParam(IntParameter(countParameter), countParameter);
					AddParam(IntParameter(byteCountParameter), byteCountParameter, "Total UTF-8 byte length of all " + field.Name + " strings combined.");
					terms.Add(OffsetVectorSizeTerm(countParameter));
					terms.Add(byteCountParameter + " + " + countParameter + " * 8");
				}
				else if (elementType == SchemaBaseType.Union)
				{
					if (field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var unionDef) && unionDef is UnionDef union)
					{
						if (TryGetKnownRefUnionPayloadSize(union, new HashSet<string>()) is int knownPayloadSize)
						{
							AddParam(IntParameter(countParameter), countParameter);
							terms.Add(OffsetVectorSizeTerm(countParameter));
							terms.Add(countParameter + " * " + knownPayloadSize.ToString(CultureInfo.InvariantCulture));
						}
						else
						{
							string maxSizeParameter = parameterPrefix + "MaxSize";
							AddParam(IntParameter(countParameter), countParameter);
							AddParam(SizeParameter(maxSizeParameter, union), maxSizeParameter, "Total size budget for all " + field.Name + " entries combined. Sum member GetMaxSize(...) for each element and convert to " + union.Name + "Size.");
							terms.Add(OffsetVectorSizeTerm(countParameter));
							terms.Add(maxSizeParameter);
						}
					}
				}
				else if (elementType == SchemaBaseType.Obj && field.Type.ReferencedName != null)
				{
					if (_schema.ByName.TryGetValue(field.Type.ReferencedName, out var elementDef))
					{
						if (elementDef is StructDef structDef)
						{
							AddParam(IntParameter(countParameter), countParameter);
							terms.Add(VectorSizeTerm(countParameter, structDef.Size));
						}
						else if (elementDef is EnumDef enumDef)
						{
							AddParam(IntParameter(countParameter), countParameter);
							terms.Add(VectorSizeTerm(countParameter, enumDef.Underlying.InlineSize()));
						}
						else if (elementDef is TableDef nestedTable)
						{
							AddParam(IntParameter(countParameter), countParameter);
							terms.Add(OffsetVectorSizeTerm(countParameter));
							if (TryGetKnownTableSize(nestedTable, new HashSet<string>()) is int knownPayloadSize)
								terms.Add(countParameter + " * " + knownPayloadSize.ToString(CultureInfo.InvariantCulture));
							else
							{
								string maxSizeParameter = parameterPrefix + "MaxSize";
								AddParam(SizeParameter(maxSizeParameter, nestedTable), maxSizeParameter, "Total size budget for all " + field.Name + " entries combined. Sum " + nestedTable.Name + "Ref.GetMaxSize(...) for each element individually.");
								terms.Add(maxSizeParameter);
							}
						}
						else if (elementDef is UnionDef union)
						{
							AddParam(IntParameter(countParameter), countParameter);
							terms.Add(OffsetVectorSizeTerm(countParameter));
							if (TryGetKnownRefUnionPayloadSize(union, new HashSet<string>()) is int knownPayloadSize)
								terms.Add(countParameter + " * " + knownPayloadSize.ToString(CultureInfo.InvariantCulture));
							else
							{
								string maxSizeParameter = parameterPrefix + "MaxSize";
								AddParam(SizeParameter(maxSizeParameter, union), maxSizeParameter, "Total size budget for all " + field.Name + " entries combined. Sum member GetMaxSize(...) for each element and convert to " + union.Name + "Size.");
								terms.Add(maxSizeParameter);
							}
						}
						else
						{
							string maxSizeParameter = parameterPrefix + "MaxSize";
							AddParam(IntParameter(countParameter), countParameter);
							AddParam(IntParameter(maxSizeParameter), maxSizeParameter, "Total size budget for all " + field.Name + " entries combined.");
							terms.Add(OffsetVectorSizeTerm(countParameter));
							terms.Add(maxSizeParameter);
						}
					}
				}
			}
		}

		_w.AppendLine("/// <summary>Returns the maximum buffer size needed to serialize this table.</summary>");
		foreach (var (name, description) in paramDocs)
		{
			if (description != null)
				_w.Append("/// <param name=\"").Append(name).Append("\">").Append(description).AppendLine("</param>");
		}
		_w.Append("public static ").Append(isRootTable ? "int" : SizeTypeName(table)).Append(" GetMaxSize(");
		for (int i = 0; i < parameters.Count; i++)
		{
			if (i > 0)
				_w.Append(", ");
			_w.Append(parameters[i]);
		}
		_w.Append(") => ");
		if (!isRootTable)
			_w.Append("new ").Append(SizeTypeName(table)).Append("(");
		_w.Append(constantSize);
		foreach (var term in terms)
			_w.Append(" + ").Append(term);
		if (!isRootTable)
			_w.Append(")");
		_w.AppendLine(";");
	}

	string IntParameter(string name) => "int " + name;

	string SizeParameter(string name, TableDef table)
	{
		if (IsRootTable(table))
			return IntParameter(name);
		return SizeTypeName(table) + " " + name;
	}

	string SizeParameter(string name, UnionDef union)
	=> SizeTypeName(union) + " " + name;

	string SizeTypeName(TableDef table) => table.Name + "Size";

	string SizeTypeName(UnionDef union) => union.Name + "Size";

	bool NeedsSizeStruct(UnionDef union)
	=> _refUnions.Contains(union.Name) && !TryGetKnownRefUnionPayloadSize(union, new HashSet<string>()).HasValue;

	bool IsRootTable(TableDef table)
	{
		for (int i = 0; i < _schema.RootTypes.Count; i++)
		{
			string root = _schema.RootTypes[i];
			if (root == table.Name)
				return true;
			if (table.Namespace is { Length: > 0 } ns && root == ns + "." + table.Name)
				return true;
		}
		return false;
	}

	int? TryGetKnownTableSize(TableDef table, HashSet<string> visiting)
	{
		if (!visiting.Add(table.Name))
			return null;

		int size = FixedTableSize(table);
		foreach (var field in table.Fields)
		{
			if (field.Deprecated)
				continue;
			if (field.Type.IsString || field.Type.IsVector)
				return null;
			if (field.Type.IsUnion)
			{
				if (field.Type.ReferencedName == null || !_refUnions.Contains(field.Type.ReferencedName))
					continue;
				if (!_schema.ByName.TryGetValue(field.Type.ReferencedName, out var unionDef) || unionDef is not UnionDef union)
					return null;
				int? unionPayloadSize = TryGetKnownRefUnionPayloadSize(union, visiting);
				if (!unionPayloadSize.HasValue)
					return null;
				size += unionPayloadSize.Value;
				continue;
			}
			if (field.Type.IsObject && field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var fieldDef) && fieldDef is TableDef nestedTable)
			{
				int? nestedSize = TryGetKnownTableSize(nestedTable, visiting);
				if (!nestedSize.HasValue)
					return null;
				size += nestedSize.Value;
			}
		}

		visiting.Remove(table.Name);
		return size;
	}

	int? TryGetKnownRefUnionPayloadSize(UnionDef union, HashSet<string> visiting)
	{
		if (!_refUnions.Contains(union.Name))
			return 0;

		int maxSize = 0;
		foreach (var member in union.Members)
		{
			if (!_schema.ByName.TryGetValue(member.TypeName, out var memberDef) || memberDef is not TableDef table)
				return null;
			int? memberSize = TryGetKnownTableSize(table, visiting);
			if (!memberSize.HasValue)
				return null;
			if (memberSize.Value > maxSize)
				maxSize = memberSize.Value;
		}
		return maxSize;
	}

	static int FixedTableSize(TableDef table)
	{
		int effectiveAlign = table.InlineAlign < 4 ? 4 : table.InlineAlign;
		return 2 * effectiveAlign + 2 * table.SlotCount + table.InlineSize + 10;
	}

	static string VectorSizeTerm(string countParameter, int elementSize)
	{
		int alignment = elementSize < 4 ? 4 : elementSize;
		return countParameter + " * " + elementSize + " + " + (4 + alignment - 1).ToString(CultureInfo.InvariantCulture);
	}

	static string OffsetVectorSizeTerm(string countParameter)
	=> countParameter + " * 4 + 7";
}
