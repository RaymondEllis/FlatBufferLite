using FlatBufferLite.SourceGen.Emit;
using FlatBufferLite.SourceGen.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FlatBufferLite.SourceGen;

[Generator]
public sealed class FlatBufferIncrementalGenerator : IIncrementalGenerator
{
	static readonly DiagnosticDescriptor DiagWarning = new("FBL003", "FlatBuffer unsupported feature", "{0}", "FlatBufferLite", DiagnosticSeverity.Warning, isEnabledByDefault: true);
	static readonly DiagnosticDescriptor DiagParseError = new("FBL001", "FlatBuffer schema parse error", "Failed to parse '{0}': {1}", "FlatBufferLite", DiagnosticSeverity.Error, isEnabledByDefault: true);
	static readonly DiagnosticDescriptor DiagCodeGenError = new("FBL002", "FlatBuffer codegen error", "Error generating code for '{0}': {1}", "FlatBufferLite", DiagnosticSeverity.Error, isEnabledByDefault: true);
	static readonly DiagnosticDescriptor DiagMissingInclude = new("FBL004", "FlatBuffer missing include", "Included file '{0}' not found (referenced from '{1}')", "FlatBufferLite", DiagnosticSeverity.Warning, isEnabledByDefault: true);

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		var fbsFiles = context.AdditionalTextsProvider
			.Where(at => at.Path.EndsWith(".fbs", StringComparison.OrdinalIgnoreCase))
			.Collect();

		context.RegisterSourceOutput(fbsFiles, (spc, files) =>
		{
			var fileContents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (var f in files)
			{
				var text = f.GetText()?.ToString();
				if (text != null)
					fileContents[f.Path] = text;
			}

			foreach (var at in files)
			{
				try
				{
					var missingIncludes = new List<string>();
					var schema = SchemaParser.ParseWithIncludes(at.Path, fileContents, missingIncludes);
					foreach (var missing in missingIncludes)
						spc.ReportDiagnostic(Diagnostic.Create(DiagMissingInclude, Location.None, missing, at.Path));
					foreach (var w in schema.Warnings)
						spc.ReportDiagnostic(Diagnostic.Create(DiagWarning, Location.None, w));
					var code = new CodeEmitter(schema).Emit();
					var hint = $"{BuildHintName(at.Path)}.g.cs";
					spc.AddSource(hint, SourceText.From(code, Encoding.UTF8));
				}
				catch (SchemaParseException ex)
				{
					spc.ReportDiagnostic(Diagnostic.Create(DiagParseError, Location.None, at.Path, ex.Message));
				}
				catch (Exception ex)
				{
					spc.ReportDiagnostic(Diagnostic.Create(DiagCodeGenError, Location.None, at.Path, ex.Message));
				}
			}
		});
	}

	static string BuildHintName(string path)
	{
		var normalized = path.Replace('\\', '/');
		var lastSlash = normalized.LastIndexOf('/');
		string relative;
		if (lastSlash >= 0)
		{
			var secondLast = normalized.LastIndexOf('/', lastSlash - 1);
			relative = secondLast >= 0
				? normalized.Substring(secondLast + 1)
				: normalized.Substring(lastSlash + 1);
		}
		else
		{
			relative = normalized;
		}
		var withoutExt = relative.EndsWith(".fbs", StringComparison.OrdinalIgnoreCase)
			? relative.Substring(0, relative.Length - 4)
			: relative;
		return SanitizeHint(withoutExt);
	}

	static string SanitizeHint(string s)
	{
		var sb = new StringBuilder(s.Length);
		foreach (var c in s)
			sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
		return sb.ToString();
	}
}
