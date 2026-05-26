using FlatBufferLite.SourceGen.Emit;
using FlatBufferLite.SourceGen.Parsing;

namespace FlatBufferLite.Tests;

/// <summary>
/// Simulates what FlatBufferIncrementalGenerator does:
/// every .fbs file comes in as an AdditionalText with its absolute path as the key.
/// ParseWithIncludes must resolve cross-file includes correctly from that dictionary.
/// </summary>
public class IncludeAdditionalFilesTests
{
	static Dictionary<string, string> LoadSchemasDir()
	{
		var schemasDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Schemas"));
		var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var path in Directory.GetFiles(schemasDir, "*.fbs"))
			files[path] = File.ReadAllText(path);
		return files;
	}

	[Fact]
	public void AdditionalFiles_IncludesMain_ResolvesSharedStructs()
	{
		var fileContents = LoadSchemasDir();

		var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var path in fileContents.Keys)
		{
			var missing = new List<string>();
			var schema = SchemaParser.ParseWithIncludes(path, fileContents, missing);
			Assert.Empty(missing);
			results[path] = new CodeEmitter(schema).Emit();
		}

		var sharedCode = results.First(r => r.Key.EndsWith("shared.fbs", StringComparison.OrdinalIgnoreCase)).Value;
		Assert.Contains("public partial struct Vector3I", sharedCode);

		var mainCode = results.First(r => r.Key.EndsWith("includes_main.fbs", StringComparison.OrdinalIgnoreCase)).Value;
		Assert.Contains("public readonly ref struct Chunk", mainCode);
		Assert.DoesNotContain("public partial struct Vector3I", mainCode);
		Assert.Contains("Vector3I pos", mainCode);
	}

	[Fact]
	public void AdditionalFiles_BackslashKeys_ResolvedCorrectly()
	{
		// Simulate Roslyn on Windows: fileContents keyed by absolute backslash paths.
		var schemasDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Schemas"));
		var fileContents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var path in Directory.GetFiles(schemasDir, "*.fbs"))
			fileContents[path.Replace('/', '\\')] = File.ReadAllText(path);

		var mainPath = fileContents.Keys.First(k => k.EndsWith("includes_main.fbs", StringComparison.OrdinalIgnoreCase));
		var missing = new List<string>();
		var schema = SchemaParser.ParseWithIncludes(mainPath, fileContents, missing);

		Assert.Empty(missing);
		Assert.Contains("Vector3I", schema.ByName.Keys);
	}
}