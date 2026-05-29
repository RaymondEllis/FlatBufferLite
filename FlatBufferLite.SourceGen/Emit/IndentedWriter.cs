using System.Text;

namespace FlatBufferLite.SourceGen.Emit;

/// <summary>
/// A lightweight StringBuilder wrapper that tracks indentation level.
/// Recommended by Roslyn incremental generator best practices.
/// </summary>
public sealed class IndentedWriter
{
	readonly StringBuilder _sb = new(4096);
	int _indent;
	bool _needsIndent = true;
	const char IndentChar = '\t';

	public int Indent { get => _indent; set => _indent = value; }

	public void IncreaseIndent() => _indent++;
	public void DecreaseIndent()
	{
		if (_indent > 0)
			_indent--;
	}

	public IndentedWriter Append(string text)
	{
		if (text.Length == 0)
			return this;
		WriteIndentIfNeeded();
		_sb.Append(text);
		return this;
	}

	public IndentedWriter Append(char c)
	{
		WriteIndentIfNeeded();
		_sb.Append(c);
		return this;
	}

	public IndentedWriter AppendLine(char c)
	{
		WriteIndentIfNeeded();
		_sb.Append(c);
		_sb.AppendLine();
		_needsIndent = true;
		return this;
	}

	public IndentedWriter Append(int value)
	{
		WriteIndentIfNeeded();
		_sb.Append(value);
		return this;
	}

	public IndentedWriter AppendLine(int value)
	{
		WriteIndentIfNeeded();
		_sb.Append(value);
		_sb.AppendLine();
		_needsIndent = true;
		return this;
	}

	public IndentedWriter Append(long value)
	{
		WriteIndentIfNeeded();
		_sb.Append(value);
		return this;
	}

	public IndentedWriter AppendLine(long value)
	{
		WriteIndentIfNeeded();
		_sb.Append(value);
		_sb.AppendLine();
		_needsIndent = true;
		return this;
	}

	public IndentedWriter AppendLine()
	{
		_sb.AppendLine();
		_needsIndent = true;
		return this;
	}

	public IndentedWriter AppendLine(string text)
	{
		WriteIndentIfNeeded();
		_sb.AppendLine(text);
		_needsIndent = true;
		return this;
	}

	public IndentedWriter EndStatement() => AppendLine(';');

	public IndentedWriter OpenBlock(string? header = null)
	{
		if (header != null)
			AppendLine(header);
		AppendLine("{");
		IncreaseIndent();
		return this;
	}

	public IndentedWriter CloseBlock() => CloseBlock("");

	public IndentedWriter CloseBlock(string suffix)
	{
		DecreaseIndent();
		WriteIndentIfNeeded();
		_sb.Append('}');
		_sb.AppendLine(suffix);
		_needsIndent = true;
		return this;
	}

	public void Clear()
	{
		_sb.Clear();
		_indent = 0;
		_needsIndent = true;
	}

	public override string ToString() => _sb.ToString();

	void WriteIndentIfNeeded()
	{
		if (_needsIndent)
		{
			for (int i = 0; i < _indent; i++)
				_sb.Append(IndentChar);
			_needsIndent = false;
		}
	}
}
