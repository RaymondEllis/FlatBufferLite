using FlatBufferLite.SourceGen.Emit;
using FlatBufferLite.SourceGen.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlatBufferLite.SourceGen;

[Generator]
public sealed class FlatBufferIncrementalGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		var fbsFiles = context.AdditionalTextsProvider
			.Where(at => at.Path.EndsWith(".fbs", System.StringComparison.OrdinalIgnoreCase))
			.Collect();

		context.RegisterSourceOutput(fbsFiles, (spc, files) =>
		{
			var fileMap = new Dictionary<string, (string Path, string Text)>(System.StringComparer.OrdinalIgnoreCase);
			foreach (var at in files)
			{
				var text = at.GetText(spc.CancellationToken)?.ToString() ?? string.Empty;
				fileMap[at.Path] = (at.Path, text);
				var fileName = Path.GetFileName(at.Path);
				if (!fileMap.ContainsKey(fileName))
					fileMap[fileName] = (at.Path, text);
			}

			foreach (var at in files)
			{
				try
				{
					var text = fileMap[at.Path].Text;
					var dir = Path.GetDirectoryName(at.Path) ?? "";
					string? Resolver(string includePath)
					{
						var relative = Path.Combine(dir, includePath);
						if (fileMap.TryGetValue(relative, out var found))
							return found.Text;
						if (fileMap.TryGetValue(includePath, out found))
							return found.Text;
						var normalized = Path.GetFullPath(relative);
						if (fileMap.TryGetValue(normalized, out found))
							return found.Text;
						return null;
					}

					var schema = SchemaParser.ParseWithIncludes(text, Resolver);
					var code = new CodeEmitter(schema).Emit();
					var hintBase = Path.GetFileNameWithoutExtension(at.Path);
					var hint = $"{SanitizeHint(hintBase)}.g.cs";
					spc.AddSource(hint, SourceText.From(code, System.Text.Encoding.UTF8));
				}
				catch (SchemaParseException ex)
				{
					spc.ReportDiagnostic(Diagnostic.Create(
						new DiagnosticDescriptor(
							"FBL001",
							"FlatBuffer schema parse error",
							$"Failed to parse '{at.Path}': {ex.Message}",
							"FlatBufferLite",
							DiagnosticSeverity.Error,
							isEnabledByDefault: true),
						Location.None));
				}
				catch (System.Exception ex)
				{
					spc.ReportDiagnostic(Diagnostic.Create(
						new DiagnosticDescriptor(
							"FBL002",
							"FlatBuffer codegen error",
							$"Error generating code for '{at.Path}': {ex.Message}",
							"FlatBufferLite",
							DiagnosticSeverity.Error,
							isEnabledByDefault: true),
						Location.None));
				}
			}
		});
	}

	static string SanitizeHint(string s)
	{
		var sb = new System.Text.StringBuilder(s.Length);
		foreach (var c in s)
			sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
		return sb.ToString();
	}
}