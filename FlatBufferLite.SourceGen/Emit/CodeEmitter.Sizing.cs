using FlatBufferLite.SourceGen.IR;
using System.Collections.Generic;
using System.Globalization;

namespace FlatBufferLite.SourceGen.Emit;

public sealed partial class CodeEmitter
{
	void EmitGetMaxSize(TableDef table)
	{
		var parameters = new List<string>();
		var terms = new List<string>();
		int constantSize = FixedTableSize(table);
		foreach (var field in table.Fields)
		{
			if (field.Deprecated)
				continue;
			string parameterPrefix = ToCamelCase(field.Name);
			if (field.Type.IsString)
			{
				string byteCountParameter = parameterPrefix + "ByteCount";
				parameters.Add(byteCountParameter);
				terms.Add(byteCountParameter + " + 8");
			}
			else if (field.Type.IsUnion)
			{
				if (field.Type.ReferencedName != null && _refUnions.Contains(field.Type.ReferencedName))
				{
					if (_schema.ByName.TryGetValue(field.Type.ReferencedName, out var unionDef) && unionDef is UnionDef union && TryGetKnownRefUnionPayloadSize(union, new HashSet<string>()) is int knownPayloadSize)
						constantSize += knownPayloadSize;
					else
					{
						string maxSizeParameter = parameterPrefix + "MaxSize";
						parameters.Add(maxSizeParameter);
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
						parameters.Add(maxSizeParameter);
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
					parameters.Add(countParameter);
					terms.Add(VectorSizeTerm(countParameter, elementType.InlineSize()));
				}
				else if (elementType == SchemaBaseType.String)
				{
					string byteCountParameter = parameterPrefix + "ByteCount";
					parameters.Add(countParameter);
					parameters.Add(byteCountParameter);
					terms.Add(OffsetVectorSizeTerm(countParameter));
					terms.Add(byteCountParameter + " + " + countParameter + " * 8");
				}
				else if (elementType == SchemaBaseType.Union)
				{
					if (field.Type.ReferencedName != null && _schema.ByName.TryGetValue(field.Type.ReferencedName, out var unionDef) && unionDef is UnionDef union && TryGetKnownRefUnionPayloadSize(union, new HashSet<string>()) is int knownPayloadSize)
					{
						parameters.Add(countParameter);
						terms.Add(OffsetVectorSizeTerm(countParameter));
						terms.Add(countParameter + " * " + knownPayloadSize.ToString(CultureInfo.InvariantCulture));
					}
					else
					{
						string maxSizeParameter = parameterPrefix + "MaxSize";
						parameters.Add(countParameter);
						parameters.Add(maxSizeParameter);
						terms.Add(OffsetVectorSizeTerm(countParameter));
						terms.Add(maxSizeParameter);
					}
				}
				else if (elementType == SchemaBaseType.Obj && field.Type.ReferencedName != null)
				{
					if (_schema.ByName.TryGetValue(field.Type.ReferencedName, out var elementDef))
					{
						if (elementDef is StructDef structDef)
						{
							parameters.Add(countParameter);
							terms.Add(VectorSizeTerm(countParameter, structDef.Size));
						}
						else if (elementDef is EnumDef enumDef)
						{
							parameters.Add(countParameter);
							terms.Add(VectorSizeTerm(countParameter, enumDef.Underlying.InlineSize()));
						}
						else if (elementDef is TableDef nestedTable)
						{
							parameters.Add(countParameter);
							terms.Add(OffsetVectorSizeTerm(countParameter));
							if (TryGetKnownTableSize(nestedTable, new HashSet<string>()) is int knownPayloadSize)
								terms.Add(countParameter + " * " + knownPayloadSize.ToString(CultureInfo.InvariantCulture));
							else
							{
								string maxSizeParameter = parameterPrefix + "MaxSize";
								parameters.Add(maxSizeParameter);
								terms.Add(maxSizeParameter);
							}
						}
						else if (elementDef is UnionDef union)
						{
							parameters.Add(countParameter);
							terms.Add(OffsetVectorSizeTerm(countParameter));
							if (TryGetKnownRefUnionPayloadSize(union, new HashSet<string>()) is int knownPayloadSize)
								terms.Add(countParameter + " * " + knownPayloadSize.ToString(CultureInfo.InvariantCulture));
							else
							{
								string maxSizeParameter = parameterPrefix + "MaxSize";
								parameters.Add(maxSizeParameter);
								terms.Add(maxSizeParameter);
							}
						}
						else
						{
							string maxSizeParameter = parameterPrefix + "MaxSize";
							parameters.Add(countParameter);
							parameters.Add(maxSizeParameter);
							terms.Add(OffsetVectorSizeTerm(countParameter));
							terms.Add(maxSizeParameter);
						}
					}
				}
			}
		}

		_sb.Append("\tpublic static int GetMaxSize(");
		for (int i = 0; i < parameters.Count; i++)
		{
			if (i > 0)
				_sb.Append(", ");
			_sb.Append("int ").Append(parameters[i]).Append(" = 0");
		}
		_sb.Append(") => ").Append(constantSize);
		foreach (var term in terms)
			_sb.Append(" + ").Append(term);
		_sb.AppendLine(";");
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