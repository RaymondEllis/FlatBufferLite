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
			var fileContents = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
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
					var schema = SchemaParser.ParseWithIncludes(at.Path, fileContents);
					foreach (var w in schema.Warnings)
						spc.ReportDiagnostic(Diagnostic.Create(
							new DiagnosticDescriptor("FBL003", "FlatBuffer unsupported feature", w, "FlatBufferLite", DiagnosticSeverity.Warning, isEnabledByDefault: true),
							Location.None));
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