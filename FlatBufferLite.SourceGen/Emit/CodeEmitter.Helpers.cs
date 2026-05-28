using FlatBufferLite.SourceGen.IR;
using System.Text;

namespace FlatBufferLite.SourceGen.Emit;

public sealed partial class CodeEmitter
{
	static string ScalarCSharpType(TypeRef type, out string defaultLiteral)
	{
		defaultLiteral = "0";
		string cs = type.Base.ToCSharpKeyword();
		if (cs == "bool")
			defaultLiteral = "false";
		else if (cs == "float")
			defaultLiteral = "0f";
		else if (cs == "double")
			defaultLiteral = "0d";
		else if (cs == "long")
			defaultLiteral = "0L";
		else if (cs == "ulong")
			defaultLiteral = "0UL";
		return cs;
	}

	static string FormatDefault(TypeRef type, string raw)
	{
		if (type.Base == SchemaBaseType.Bool)
			return raw == "true" ? "true" : "false";
		if (type.Base == SchemaBaseType.Float)
		{
			if (raw == "inf" || raw == "infinity")
				return "float.PositiveInfinity";
			if (raw == "-inf" || raw == "-infinity")
				return "float.NegativeInfinity";
			if (raw == "nan")
				return "float.NaN";
			return raw + "f";
		}
		if (type.Base == SchemaBaseType.Double)
		{
			if (raw == "inf" || raw == "infinity")
				return "double.PositiveInfinity";
			if (raw == "-inf" || raw == "-infinity")
				return "double.NegativeInfinity";
			if (raw == "nan")
				return "double.NaN";
			return raw + "d";
		}
		if (type.Base == SchemaBaseType.Long)
			return raw + "L";
		if (type.Base == SchemaBaseType.ULong)
			return raw + "UL";
		return raw;
	}

	string ResolveTypeName(TypeRef type)
	{
		if (type.Base.IsScalar())
			return type.Base.ToCSharpKeyword();
		if (type.Base == SchemaBaseType.Obj && type.ReferencedName != null)
			return type.ReferencedName;
		return "object";
	}

	static string ToPascalCase(string s)
	{
		if (string.IsNullOrEmpty(s))
			return s;
		var sb = new StringBuilder(s.Length);
		bool upper = true;
		foreach (var c in s)
		{
			if (c == '_')
			{
				upper = true;
				continue;
			}
			sb.Append(upper ? char.ToUpperInvariant(c) : c);
			upper = false;
		}
		return sb.ToString();
	}

	static string ToCamelCase(string s)
	{
		string pascal = ToPascalCase(s);
		if (string.IsNullOrEmpty(pascal))
			return pascal;
		return char.ToLowerInvariant(pascal[0]) + pascal.Substring(1);
	}

	static int AlignUp(int value, int alignment)
	{
		if (alignment <= 1)
			return value;
		return (value + alignment - 1) & ~(alignment - 1);
	}

	static string FormatEnumDefault(EnumDef ed, string raw)
	{
		string under = ed.Underlying.ToCSharpKeyword();
		if (raw.Length > 0 && (char.IsDigit(raw[0]) || raw[0] == '-'))
			return "(" + under + ")" + raw;
		return "(" + under + ")" + ed.Name + "." + raw;
	}

	static bool IsValidCSharpIdentifier(string s)
	{
		if (string.IsNullOrEmpty(s))
			return false;
		if (s[0] == '.' || s[s.Length - 1] == '.')
			return false;
		for (int i = 0; i < s.Length; i++)
		{
			char c = s[i];
			if (c == '.')
				continue;
			if (c == '_')
				continue;
			if (i == 0 || (i > 0 && s[i - 1] == '.'))
			{
				if (!char.IsLetter(c) && c != '_')
					return false;
			}
			else if (!char.IsLetterOrDigit(c))
				return false;
		}
		return true;
	}
}
