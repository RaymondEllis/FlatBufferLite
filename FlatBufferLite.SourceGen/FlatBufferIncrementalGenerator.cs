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
			.Where(static at => at.Path.EndsWith(".fbs", StringComparison.OrdinalIgnoreCase))
			.Select(static (at, ct) => (Path: at.Path, Content: at.GetText(ct)?.ToString() ?? ""))
			.Collect();

		context.RegisterSourceOutput(fbsFiles, (spc, files) =>
		{
			var fileContents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (var f in files)
			{
				if (f.Content.Length > 0)
					fileContents[f.Path] = f.Content;
			}

			string? commonBase = null;
			foreach (var f in files)
			{
				if (f.Content.Length == 0)
					continue;
				var normalized = SchemaPath.Normalize(f.Path);
				var lastSlash = normalized.LastIndexOf('/');
				var dir = lastSlash >= 0 ? normalized.Substring(0, lastSlash + 1) : "";
				if (dir.Length == 0)
					continue;
				if (commonBase == null)
				{
					commonBase = dir;
					continue;
				}
				int i = 0;
				while (i < commonBase.Length && i < dir.Length && char.ToLowerInvariant(commonBase[i]) == char.ToLowerInvariant(dir[i]))
					i++;
				if (i == 0)
				{
					commonBase = "";
					break;
				}
				var lastCommonSlash = commonBase.LastIndexOf('/', i - 1);
				commonBase = lastCommonSlash >= 0 ? commonBase.Substring(0, lastCommonSlash + 1) : "";
				if (commonBase.Length == 0)
					break;
			}

			foreach (var f in files)
			{
				spc.CancellationToken.ThrowIfCancellationRequested();
				if (f.Content.Length == 0)
					continue;
				try
				{
					var missingIncludes = new List<string>();
					var schema = SchemaParser.ParseWithIncludes(f.Path, fileContents, missingIncludes, spc.CancellationToken);
					foreach (var missing in missingIncludes)
						spc.ReportDiagnostic(Diagnostic.Create(DiagMissingInclude, Location.None, missing, f.Path));
					foreach (var w in schema.Warnings)
						spc.ReportDiagnostic(Diagnostic.Create(DiagWarning, Location.None, w));
					var code = new CodeEmitter(schema, spc.CancellationToken, commonBase).Emit();
					var hint = $"{BuildHintName(f.Path)}.g.cs";
					spc.AddSource(hint, SourceText.From(code, Encoding.UTF8));
				}
				catch (SchemaParseException ex)
				{
					spc.ReportDiagnostic(Diagnostic.Create(DiagParseError, Location.None, f.Path, ex.Message));
				}
				catch (Exception ex)
				{
					spc.ReportDiagnostic(Diagnostic.Create(DiagCodeGenError, Location.None, f.Path, ex.Message));
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
