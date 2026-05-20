using FlatBufferLite.SourceGen.Emit;
using FlatBufferLite.SourceGen.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
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
			.Select((at, ct) =>
			{
				var text = at.GetText(ct)?.ToString() ?? string.Empty;
				return (Path: at.Path, Text: text);
			});

		context.RegisterSourceOutput(fbsFiles, (spc, src) =>
		{
			try
			{
				var parser = new SchemaParser(src.Text);
				var schema = parser.Parse();
				var code = new CodeEmitter(schema).Emit();
				var hintBase = Path.GetFileNameWithoutExtension(src.Path);
				var hint = $"{SanitizeHint(hintBase)}.g.cs";
				spc.AddSource(hint, SourceText.From(code, System.Text.Encoding.UTF8));
			}
			catch (SchemaParseException ex)
			{
				spc.ReportDiagnostic(Diagnostic.Create(
					new DiagnosticDescriptor(
						"FBL001",
						"FlatBuffer schema parse error",
						$"Failed to parse '{src.Path}': {ex.Message}",
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
						$"Error generating code for '{src.Path}': {ex.Message}",
						"FlatBufferLite",
						DiagnosticSeverity.Error,
						isEnabledByDefault: true),
					Location.None));
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