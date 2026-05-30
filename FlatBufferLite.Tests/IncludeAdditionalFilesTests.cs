using FlatBufferLite.SourceGen.Parsing;

namespace FlatBufferLite.Tests;

public class IncludeAdditionalFilesTests
{
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
