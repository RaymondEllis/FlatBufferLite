using FlatBufferLite.SourceGen.IR;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FlatBufferLite.SourceGen.Parsing;

public enum TokenKind
{
	Ident, IntLit, FloatLit, StringLit,
	LBrace, RBrace, LParen, RParen, LBracket, RBracket,
	Colon, Semicolon, Comma, Assign, Dot,
	EOF,
}

public struct Token
{
	public TokenKind Kind;
	public string Text;
	public int Line;
	public int Column;
}

public sealed class SchemaParseException : Exception
{
	public int Line;
	public int Column;
	public SchemaParseException(string msg, int line, int col) : base($"({line},{col}): {msg}")
	{
		Line = line;
		Column = col;
	}
}

public sealed class Lexer
{
	readonly string _src;
	int _pos;
	int _line = 1;
	int _col = 1;

	public Lexer(string src) { _src = src; }

	public List<Token> Tokenize()
	{
		var list = new List<Token>();
		while (true)
		{
			SkipWhitespaceAndComments();
			if (_pos >= _src.Length)
			{
				list.Add(new Token { Kind = TokenKind.EOF, Line = _line, Column = _col });
				return list;
			}
			int startLine = _line, startCol = _col;
			char c = _src[_pos];
			if (IsIdentStart(c))
			{
				list.Add(ReadIdent(startLine, startCol));
				continue;
			}
			if (char.IsDigit(c) || (c == '-' && _pos + 1 < _src.Length && (char.IsDigit(_src[_pos + 1]) || _src[_pos + 1] == '.')))
			{
				list.Add(ReadNumber(startLine, startCol));
				continue;
			}
			if (c == '"')
			{
				list.Add(ReadString(startLine, startCol));
				continue;
			}
			list.Add(ReadPunct(startLine, startCol));
		}
	}

	static bool IsIdentStart(char c) => c == '_' || char.IsLetter(c);
	static bool IsIdentPart(char c) => c == '_' || char.IsLetterOrDigit(c);

	void Advance()
	{
		if (_src[_pos] == '\n')
		{
			_line++;
			_col = 1;
		}
		else
			_col++;
		_pos++;
	}

	void SkipWhitespaceAndComments()
	{
		while (_pos < _src.Length)
		{
			char c = _src[_pos];
			if (char.IsWhiteSpace(c))
			{
				Advance();
				continue;
			}
			if (c == '/' && _pos + 1 < _src.Length)
			{
				char n = _src[_pos + 1];
				if (n == '/')
				{
					while (_pos < _src.Length && _src[_pos] != '\n')
						Advance();
					continue;
				}
				if (n == '*')
				{
					Advance();
					Advance();
					while (_pos < _src.Length && !(_src[_pos] == '*' && _pos + 1 < _src.Length && _src[_pos + 1] == '/'))
						Advance();
					if (_pos < _src.Length)
					{
						Advance();
						Advance();
					}
					continue;
				}
			}
			break;
		}
	}

	Token ReadIdent(int l, int c)
	{
		int start = _pos;
		while (_pos < _src.Length && IsIdentPart(_src[_pos]))
			Advance();
		return new Token { Kind = TokenKind.Ident, Text = _src.Substring(start, _pos - start), Line = l, Column = c };
	}

	Token ReadNumber(int l, int c)
	{
		int start = _pos;
		bool isFloat = false;
		if (_src[_pos] == '-')
			Advance();
		if (_pos + 1 < _src.Length && _src[_pos] == '0' && (_src[_pos + 1] == 'x' || _src[_pos + 1] == 'X'))
		{
			Advance();
			Advance();
			while (_pos < _src.Length && IsHex(_src[_pos]))
				Advance();
			return new Token { Kind = TokenKind.IntLit, Text = _src.Substring(start, _pos - start), Line = l, Column = c };
		}
		while (_pos < _src.Length && char.IsDigit(_src[_pos]))
			Advance();
		if (_pos < _src.Length && _src[_pos] == '.')
		{
			isFloat = true;
			Advance();
			while (_pos < _src.Length && char.IsDigit(_src[_pos]))
				Advance();
		}
		if (_pos < _src.Length && (_src[_pos] == 'e' || _src[_pos] == 'E'))
		{
			isFloat = true;
			Advance();
			if (_pos < _src.Length && (_src[_pos] == '+' || _src[_pos] == '-'))
				Advance();
			while (_pos < _src.Length && char.IsDigit(_src[_pos]))
				Advance();
		}
		return new Token { Kind = isFloat ? TokenKind.FloatLit : TokenKind.IntLit, Text = _src.Substring(start, _pos - start), Line = l, Column = c };
	}

	static bool IsHex(char c) => char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

	Token ReadString(int l, int c)
	{
		Advance();
		var sb = new StringBuilder();
		while (_pos < _src.Length && _src[_pos] != '"')
		{
			if (_src[_pos] == '\\' && _pos + 1 < _src.Length)
			{
				char n = _src[_pos + 1];
				Advance();
				Advance();
				sb.Append(n switch { 'n' => '\n', 'r' => '\r', 't' => '\t', '"' => '"', '\\' => '\\', _ => n });
				continue;
			}
			sb.Append(_src[_pos]);
			Advance();
		}
		if (_pos < _src.Length)
			Advance();
		return new Token { Kind = TokenKind.StringLit, Text = sb.ToString(), Line = l, Column = c };
	}

	Token ReadPunct(int l, int c)
	{
		char ch = _src[_pos];
		Advance();
		TokenKind kind = ch switch
		{
			'{' => TokenKind.LBrace,
			'}' => TokenKind.RBrace,
			'(' => TokenKind.LParen,
			')' => TokenKind.RParen,
			'[' => TokenKind.LBracket,
			']' => TokenKind.RBracket,
			':' => TokenKind.Colon,
			';' => TokenKind.Semicolon,
			',' => TokenKind.Comma,
			'=' => TokenKind.Assign,
			'.' => TokenKind.Dot,
			_ => throw new SchemaParseException($"Unexpected character '{ch}'", l, c),
		};
		return new Token { Kind = kind, Text = ch.ToString(), Line = l, Column = c };
	}
}

public sealed class SchemaParser
{
	const int FlatBufferOffsetSize = 4;
	readonly List<Token> _tokens;
	int _pos;

	public SchemaParser(string source)
	{
		_tokens = new Lexer(source).Tokenize();
	}

	Token Peek(int n = 0)
	{
		int idx = _pos + n;
		if (idx >= _tokens.Count)
			return _tokens[_tokens.Count - 1]; // EOF token
		return _tokens[idx];
	}

	Token Next()
	{
		if (_pos >= _tokens.Count - 1)
		{
			var eof = _tokens[_tokens.Count - 1];
			throw new SchemaParseException("Unexpected end of file", eof.Line, eof.Column);
		}
		return _tokens[_pos++];
	}

	bool Match(TokenKind kind)
	{
		if (Peek().Kind == kind)
		{
			_pos++;
			return true;
		}

		return false;
	}
	Token Expect(TokenKind kind)
	{
		var t = Peek();
		if (t.Kind != kind)
			throw new SchemaParseException($"Expected {kind} got {t.Kind} ('{t.Text}')", t.Line, t.Column);
		_pos++;
		return t;
	}
	Token ExpectIdent()
	{
		var t = Peek();
		if (t.Kind != TokenKind.Ident)
			throw new SchemaParseException($"Expected identifier got {t.Kind}", t.Line, t.Column);
		_pos++;
		return t;
	}

	public Schema Parse()
	{
		var schema = ParseRaw();
		AssignUnionTags(schema);
		schema.Index();
		AssignStructLayout(schema);
		ResolveUnionFieldTypes(schema);
		AssignFieldOffsets(schema);
		AssignTableLayout(schema);
		return schema;
	}

	public Schema ParseRaw()
	{
		var schema = new Schema();
		string? currentNamespace = null;
		while (Peek().Kind != TokenKind.EOF)
		{
			var t = Peek();
			if (t.Kind != TokenKind.Ident)
				throw new SchemaParseException($"Unexpected token '{t.Text}'", t.Line, t.Column);
			switch (t.Text)
			{
				case "namespace":
					_pos++;
					currentNamespace = ParseQualifiedName();
					schema.Namespace = currentNamespace;
					Expect(TokenKind.Semicolon);
					break;
				case "table": ParseTable(schema, currentNamespace); break;
				case "struct": ParseStruct(schema, currentNamespace); break;
				case "enum": ParseEnum(schema, currentNamespace); break;
				case "union": ParseUnion(schema, currentNamespace); break;
				case "root_type": _pos++; schema.AddRootType(ParseQualifiedName()); Expect(TokenKind.Semicolon); break;
				case "file_identifier": _pos++; schema.FileIdentifier = Expect(TokenKind.StringLit).Text; Expect(TokenKind.Semicolon); break;
				case "file_extension": _pos++; schema.FileExtension = Expect(TokenKind.StringLit).Text; Expect(TokenKind.Semicolon); break;
				case "include": _pos++; schema.Includes.Add(Expect(TokenKind.StringLit).Text); Expect(TokenKind.Semicolon); break;
				case "native_include": _pos++; Expect(TokenKind.StringLit); Expect(TokenKind.Semicolon); break;
				case "rpc_service": ParseRpcService(schema); break;
				case "attribute":
					_pos++;
					if (Peek().Kind == TokenKind.StringLit)
						_pos++;
					else
						ExpectIdent();
					Expect(TokenKind.Semicolon);
					break;
				default: throw new SchemaParseException($"Unknown declaration '{t.Text}'", t.Line, t.Column);
			}
		}
		schema.MarkLocalCounts();
		return schema;
	}

	public static Schema ParseWithIncludes(string entryFilePath, IReadOnlyDictionary<string, string> fileContents, List<string>? missingIncludes = null)
	{
		// Normalise to forward slashes so the visited set is consistent regardless
		// of how the caller constructed the path (Roslyn on Windows uses backslashes).
		var normalized = SchemaPath.Normalize(entryFilePath);
		var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { normalized };
		return ParseWithIncludes(normalized, fileContents, visited, missingIncludes);
	}

	static bool TryLookupFile(IReadOnlyDictionary<string, string> files, string path, out string? content)
	{
		if (files.TryGetValue(path, out content!))
			return true;
		// Try the other separator style; fileContents keys may use backslashes (Roslyn
		// on Windows) or forward slashes (cross-platform dictionaries in tests).
		var alt = path.IndexOf('/') >= 0 ? path.Replace('/', '\\') : path.Replace('\\', '/');
		return files.TryGetValue(alt, out content!);
	}

	private static Schema ParseWithIncludes(string filePath, IReadOnlyDictionary<string, string> fileContents, HashSet<string> visited, List<string>? missingIncludes)
	{
		TryLookupFile(fileContents, filePath, out var source);
		source ??= "";
		// dir is the forward-slash directory portion of filePath (trailing slash included).
		var lastSlash = filePath.LastIndexOf('/');
		var dir = lastSlash >= 0 ? filePath.Substring(0, lastSlash + 1) : "";
		var parser = new SchemaParser(source);
		var schema = parser.ParseRaw();

		foreach (var include in schema.Includes)
		{
			var resolved = SchemaPath.Normalize(dir + include);
			if (!visited.Add(resolved))
				continue;
			if (!TryLookupFile(fileContents, resolved, out _))
			{
				missingIncludes?.Add(include);
				continue;
			}
			var included = ParseWithIncludes(resolved, fileContents, visited, missingIncludes);
			schema.MergeFrom(included);
		}

		AssignUnionTags(schema);
		schema.Index();
		AssignStructLayout(schema);
		ResolveUnionFieldTypes(schema);
		AssignFieldOffsets(schema);
		AssignTableLayout(schema);
		return schema;
	}

	string ParseQualifiedName()
	{
		var sb = new StringBuilder();
		sb.Append(ExpectIdent().Text);
		while (Match(TokenKind.Dot))
		{
			sb.Append('.');
			sb.Append(ExpectIdent().Text);
		}
		return sb.ToString();
	}

	void ParseTable(Schema schema, string? ns)
	{
		_pos++;
		var name = ExpectIdent().Text;
		var table = new TableDef { Name = name, Namespace = ns };
		ParseTableMetadata(table);
		Expect(TokenKind.LBrace);
		while (Peek().Kind != TokenKind.RBrace)
			table.Fields.Add(ParseFieldDef());
		Expect(TokenKind.RBrace);
		schema.Tables.Add(table);
	}

	void ParseStruct(Schema schema, string? ns)
	{
		_pos++;
		var name = ExpectIdent().Text;
		var s = new StructDef { Name = name, Namespace = ns };
		ParseStructMetadata(s);
		Expect(TokenKind.LBrace);
		while (Peek().Kind != TokenKind.RBrace)
		{
			var f = ParseFieldDef();
			s.Fields.Add(new StructFieldDef { Name = f.Name, Type = f.Type, ForceAlign = f.ForceAlign });
		}
		Expect(TokenKind.RBrace);
		schema.Structs.Add(s);
	}

	void ParseEnum(Schema schema, string? ns)
	{
		_pos++;
		var name = ExpectIdent().Text;
		var underlying = SchemaBaseType.Int;
		bool isBitFlags = false;
		if (Match(TokenKind.Colon))
		{
			underlying = ParseScalarType();
		}
		if (Peek().Kind == TokenKind.LParen)
		{
			Expect(TokenKind.LParen);
			while (Peek().Kind != TokenKind.RParen)
			{
				var meta = ExpectIdent().Text;
				if (meta == "bit_flags")
					isBitFlags = true;
				if (Match(TokenKind.Colon))
					Next();
				if (!Match(TokenKind.Comma))
					break;
			}
			Expect(TokenKind.RParen);
		}
		Expect(TokenKind.LBrace);
		var e = new EnumDef { Name = name, Underlying = underlying, IsBitFlags = isBitFlags, Namespace = ns };
		long autoVal = 0;
		while (Peek().Kind != TokenKind.RBrace)
		{
			var vname = ExpectIdent().Text;
			long val = autoVal;
			bool isExplicit = false;
			if (Match(TokenKind.Assign))
			{
				val = ParseLongLiteral();
				isExplicit = true;
			}
			e.Values.Add(new EnumValueDef { Name = vname, Value = val, IsExplicit = isExplicit });
			autoVal = val + 1;
			if (!Match(TokenKind.Comma))
				break;
		}
		Expect(TokenKind.RBrace);
		schema.Enums.Add(e);
	}

	void ParseUnion(Schema schema, string? ns)
	{
		_pos++;
		var name = ExpectIdent().Text;
		SkipMetadata();
		Expect(TokenKind.LBrace);
		var u = new UnionDef { Name = name, Namespace = ns };
		while (Peek().Kind != TokenKind.RBrace)
		{
			var t = ExpectIdent();
			string typeName = t.Text;
			string alias = typeName;
			while (Match(TokenKind.Dot))
				typeName += "." + ExpectIdent().Text;
			if (Match(TokenKind.Colon))
			{
				alias = typeName;
				var realType = ExpectIdent().Text;
				while (Match(TokenKind.Dot))
					realType += "." + ExpectIdent().Text;
				typeName = realType;
			}
			u.Members.Add(new UnionMember { Name = alias, TypeName = typeName });
			if (!Match(TokenKind.Comma))
				break;
		}
		Expect(TokenKind.RBrace);
		schema.Unions.Add(u);
	}

	void ParseRpcService(Schema schema)
	{
		_pos++;
		var name = ExpectIdent().Text;
		schema.Warnings.Add($"rpc_service '{name}' is not supported and will be ignored.");
		Expect(TokenKind.LBrace);
		while (Peek().Kind != TokenKind.RBrace)
		{
			ExpectIdent(); // method name
			Expect(TokenKind.LParen);
			ParseQualifiedName(); // request type
			Expect(TokenKind.RParen);
			Expect(TokenKind.Colon);
			ParseQualifiedName(); // response type
			SkipMetadata();
			Expect(TokenKind.Semicolon);
		}
		Expect(TokenKind.RBrace);
	}

	FieldDef ParseFieldDef()
	{
		var name = ExpectIdent().Text;
		Expect(TokenKind.Colon);
		var type = ParseType();
		var field = new FieldDef { Name = name, Type = type };
		if (Match(TokenKind.Assign))
		{
			var t = Next();
			field.DefaultValue = t.Text;
		}
		ParseFieldMetadata(field);
		Expect(TokenKind.Semicolon);
		return field;
	}

	void ParseTableMetadata(TableDef table)
	{
		if (Peek().Kind != TokenKind.LParen)
			return;
		Expect(TokenKind.LParen);
		while (Peek().Kind != TokenKind.RParen)
		{
			var attr = ExpectIdent().Text;
			switch (attr)
			{
				case "original_order":
					table.OriginalOrder = true;
					break;
				case "plain_struct":
					table.PlainStruct = true;
					break;
				default:
					if (Match(TokenKind.Colon))
						Next();
					break;
			}
			if (!Match(TokenKind.Comma))
				break;
		}
		Expect(TokenKind.RParen);
	}

	void ParseStructMetadata(StructDef s)
	{
		if (Peek().Kind != TokenKind.LParen)
			return;
		Expect(TokenKind.LParen);
		while (Peek().Kind != TokenKind.RParen)
		{
			var attr = ExpectIdent().Text;
			if (attr == "force_align")
			{
				Expect(TokenKind.Colon);
				s.ForceAlign = (int)ParseLongLiteral();
			}
			else if (Match(TokenKind.Colon))
				Next();
			if (!Match(TokenKind.Comma))
				break;
		}
		Expect(TokenKind.RParen);
	}

	void ParseFieldMetadata(FieldDef field)
	{
		if (Peek().Kind != TokenKind.LParen)
			return;
		Expect(TokenKind.LParen);
		while (Peek().Kind != TokenKind.RParen)
		{
			var attr = ExpectIdent().Text;
			switch (attr)
			{
				case "deprecated":
					field.Deprecated = true;
					break;
				case "required":
					field.Required = true;
					break;
				case "key":
					field.IsKey = true;
					break;
				case "flexbuffer":
					field.IsFlexBuffer = true;
					break;
				case "CustomCollection":
					field.CustomCollection = true;
					break;
				case "hash":
					Expect(TokenKind.Colon);
					field.HashAlgorithm = Expect(TokenKind.StringLit).Text;
					break;
				case "nested_flatbuffer":
					Expect(TokenKind.Colon);
					field.NestedFlatBufferType = Expect(TokenKind.StringLit).Text;
					break;
				case "force_align":
					Expect(TokenKind.Colon);
					field.ForceAlign = (int)ParseLongLiteral();
					break;
				case "id":
					Expect(TokenKind.Colon);
					field.Id = (int)ParseLongLiteral();
					break;
				default:
					if (Match(TokenKind.Colon))
						Next();
					break;
			}
			if (!Match(TokenKind.Comma))
				break;
		}
		Expect(TokenKind.RParen);
	}

	void SkipMetadata()
	{
		if (Peek().Kind != TokenKind.LParen)
			return;
		Expect(TokenKind.LParen);
		int depth = 1;
		while (depth > 0)
		{
			var t = Next();
			if (t.Kind == TokenKind.LParen)
				depth++;
			else if (t.Kind == TokenKind.RParen)
				depth--;
		}
	}

	TypeRef ParseType()
	{
		if (Match(TokenKind.LBracket))
		{
			var inner = ParseType();
			Expect(TokenKind.RBracket);
			return new TypeRef
			{
				Base = SchemaBaseType.Vector,
				ElementBase = inner.Base,
				ReferencedName = inner.ReferencedName,
			};
		}
		var t = ExpectIdent();
		switch (t.Text)
		{
			case "bool": return new TypeRef { Base = SchemaBaseType.Bool };
			case "byte": case "int8": return new TypeRef { Base = SchemaBaseType.Byte };
			case "ubyte": case "uint8": return new TypeRef { Base = SchemaBaseType.UByte };
			case "short": case "int16": return new TypeRef { Base = SchemaBaseType.Short };
			case "ushort": case "uint16": return new TypeRef { Base = SchemaBaseType.UShort };
			case "int": case "int32": return new TypeRef { Base = SchemaBaseType.Int };
			case "uint": case "uint32": return new TypeRef { Base = SchemaBaseType.UInt };
			case "long": case "int64": return new TypeRef { Base = SchemaBaseType.Long };
			case "ulong": case "uint64": return new TypeRef { Base = SchemaBaseType.ULong };
			case "float": case "float32": return new TypeRef { Base = SchemaBaseType.Float };
			case "double": case "float64": return new TypeRef { Base = SchemaBaseType.Double };
			case "string": return new TypeRef { Base = SchemaBaseType.String };
			default:
				var qn = new StringBuilder(t.Text);
				while (Match(TokenKind.Dot))
				{
					qn.Append('.');
					qn.Append(ExpectIdent().Text);
				}
				return new TypeRef { Base = SchemaBaseType.Obj, ReferencedName = qn.ToString() };
		}
	}

	SchemaBaseType ParseScalarType()
	{
		var t = ExpectIdent();
		return t.Text switch
		{
			"byte" or "int8" => SchemaBaseType.Byte,
			"ubyte" or "uint8" => SchemaBaseType.UByte,
			"short" or "int16" => SchemaBaseType.Short,
			"ushort" or "uint16" => SchemaBaseType.UShort,
			"int" or "int32" => SchemaBaseType.Int,
			"uint" or "uint32" => SchemaBaseType.UInt,
			"long" or "int64" => SchemaBaseType.Long,
			"ulong" or "uint64" => SchemaBaseType.ULong,
			_ => throw new SchemaParseException($"Expected scalar type, got '{t.Text}'", t.Line, t.Column),
		};
	}

	long ParseLongLiteral()
	{
		var t = Next();
		var text = t.Text;
		if (text.StartsWith("0x") || text.StartsWith("0X"))
			return long.Parse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		return long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
	}

	static void AssignFieldOffsets(Schema schema)
	{
		foreach (var table in schema.Tables)
		{
			bool hasExplicitIds = false;
			foreach (var f in table.Fields)
			{
				if (f.Id >= 0)
				{
					hasExplicitIds = true;
					break;
				}
			}

			if (hasExplicitIds)
			{
				int maxSlot = -1;
				foreach (var f in table.Fields)
				{
					int slot = f.Id >= 0 ? f.Id : 0;
					f.VTableOffset = 4 + slot * 2;
					int top = f.Type.Base == SchemaBaseType.Union ? slot + 1 : slot;
					if (top > maxSlot)
						maxSlot = top;
				}
				table.SlotCount = maxSlot + 1;
			}
			else
			{
				int slot = 0;
				foreach (var f in table.Fields)
				{
					f.VTableOffset = 4 + slot * 2;
					slot++;
					if (f.Type.Base == SchemaBaseType.Union)
						slot++;
				}
				table.SlotCount = slot;
			}
		}
	}

	static void AssignTableLayout(Schema schema)
	{
		foreach (var t in schema.Tables)
		{
			int off = 0;
			int maxAlign = 4; // soffset is 4-byte
			foreach (var f in GetTableLayoutFields(t, schema))
			{
				if (f.Deprecated)
				{
					f.InlineOffset = -1;
					f.UnionDataInlineOffset = -1;
					continue;
				}
				if (f.Type.Base == SchemaBaseType.Union)
				{
					f.InlineOffset = off; // type tag byte
					off += 1;
					off = AlignUp(off, 4);
					f.UnionDataInlineOffset = off; // 4-byte uoffset
					off += 4;
					continue;
				}
				if (f.Type.IsString || f.Type.IsVector)
				{
					off = AlignUp(off, 4);
					f.InlineOffset = off;
					off += 4;
					continue;
				}
				if (f.Type.IsObject && f.Type.ReferencedName != null &&
					schema.ByName.TryGetValue(f.Type.ReferencedName, out var def) && def is TableDef)
				{
					off = AlignUp(off, 4);
					f.InlineOffset = off;
					off += 4;
					continue;
				}
				int size = FieldSize(f.Type, schema, out int align);
				if (size == 0 && f.Type.IsObject && f.Type.ReferencedName != null && !schema.ByName.ContainsKey(f.Type.ReferencedName))
					schema.Warnings.Add($"Table '{t.Name}': field '{f.Name}' references unknown type '{f.Type.ReferencedName}'; layout cannot be computed without the included definition.");
				if (align > maxAlign)
					maxAlign = align;
				off = AlignUp(off, align);
				f.InlineOffset = off;
				off += size;
			}
			t.InlineSize = AlignUp(off, maxAlign);
			t.InlineAlign = maxAlign;
		}
	}

	static List<FieldDef> GetTableLayoutFields(TableDef table, Schema schema)
	{
		var fields = new List<FieldDef>(table.Fields);
		if (table.OriginalOrder)
			return fields;

		var order = new Dictionary<FieldDef, (int SortSize, int DeclarationIndex)>(fields.Count);
		for (int i = 0; i < fields.Count; i++)
		{
			var field = fields[i];
			order[field] = (GetTableFieldSortSize(field, schema), i);
		}

		fields.Sort((a, b) =>
		{
			var x = order[a];
			var y = order[b];
			int sizeCmp = y.SortSize.CompareTo(x.SortSize);
			if (sizeCmp != 0)
				return sizeCmp;
			return x.DeclarationIndex.CompareTo(y.DeclarationIndex);
		});
		return fields;
	}

	static int GetTableFieldSortSize(FieldDef field, Schema schema)
	{
		if (field.Type.IsUnion)
			return FlatBufferOffsetSize;
		if (field.Type.IsString || field.Type.IsVector)
			return FlatBufferOffsetSize;
		if (field.Type.IsObject && field.Type.ReferencedName != null &&
			schema.ByName.TryGetValue(field.Type.ReferencedName, out var def))
		{
			if (def is EnumDef ed)
				return ed.Underlying.InlineSize();
			if (def is StructDef or TableDef or UnionDef)
				// Match FlatBuffers sort-by-size buckets: SizeOf(BASE_TYPE_STRUCT/TABLE/UNION) == sizeof(Offset<void>) == 4.
				return FlatBufferOffsetSize;
			return FlatBufferOffsetSize;
		}
		return field.Type.Base.InlineSize();
	}

	static void AssignStructLayout(Schema schema)
	{
		foreach (var s in schema.Structs)
		{
			int offset = 0;
			int maxAlign = s.ForceAlign > 0 ? s.ForceAlign : 1;
			foreach (var f in s.Fields)
			{
				int size = FieldSize(f.Type, schema, out int align);
				if (f.ForceAlign > 0)
					align = f.ForceAlign;
				if (align > maxAlign)
					maxAlign = align;
				offset = AlignUp(offset, align);
				f.Offset = offset;
				f.Size = size;
				offset += size;
			}
			s.Size = AlignUp(offset, maxAlign);
			s.Alignment = maxAlign;
		}
	}

	static int FieldSize(TypeRef type, Schema schema, out int align)
	{
		if (type.Base.IsScalar())
		{
			align = type.Base.InlineSize();
			return align;
		}
		if (type.Base == SchemaBaseType.Obj && type.ReferencedName != null
			&& schema.ByName.TryGetValue(type.ReferencedName, out var def))
		{
			if (def is StructDef sd)
			{
				align = sd.Alignment > 0 ? sd.Alignment : 1;
				return sd.Size;
			}
			if (def is EnumDef ed)
			{
				align = ed.Underlying.InlineSize();
				return align;
			}
		}
		align = 1;
		return 0;
	}

	static int AlignUp(int value, int alignment)
	{
		if (alignment <= 1)
			return value;
		return (value + alignment - 1) & ~(alignment - 1);
	}

	static void AssignUnionTags(Schema schema)
	{
		foreach (var u in schema.Unions)
		{
			byte tag = 1;
			foreach (var m in u.Members)
				m.Tag = tag++;
		}
	}

	static void ResolveUnionFieldTypes(Schema schema)
	{
		foreach (var t in schema.Tables)
		{
			foreach (var f in t.Fields)
			{
				if (f.Type.Base == SchemaBaseType.Obj && f.Type.ReferencedName != null &&
					schema.ByName.TryGetValue(f.Type.ReferencedName, out var def) && def is UnionDef)
				{
					f.Type.Base = SchemaBaseType.Union;
				}
			}
		}
	}
}
