using FlatBufferLite.SourceGen.Emit;
using FlatBufferLite.SourceGen.IR;
using FlatBufferLite.SourceGen.Parsing;

namespace FlatBufferLite.Tests;

public class MultiIncludeTests
{
	static Dictionary<string, string> LoadDir()
	{
		var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Schemas", "multi_include"));
		var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var path in Directory.GetFiles(dir, "*.fbs", SearchOption.AllDirectories))
			files[path] = File.ReadAllText(path);
		return files;
	}

	static string Find(Dictionary<string, string> files, string suffix)
		=> files.Keys.Single(k => k.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

	[Fact]
	public void CrossNamespace_EmitterAddsUsingForReferencedNamespace()
	{
		// sprite.fbs (FlatBufferLite.Tests.MultiInclude) includes Vec2/Vec3 from
		// FlatBufferLite.Tests.MultiInclude.Math. Without a using directive those
		// type names in the emitted Sprite fields would not compile.
		var files = LoadDir();
		var schema = SchemaParser.ParseWithIncludes(Find(files, "sprite.fbs"), files);
		var code = new CodeEmitter(schema).Emit();

		Assert.Contains("using FlatBufferLite.Tests.MultiInclude.Math;", code);
		Assert.Contains("public readonly ref partial struct Sprite", code);
	}

	[Fact]
	public void CrossNamespace_EmitterAddsUsingForMultipleNamespaces()
	{
		// scene.fbs (FlatBufferLite.Tests.MultiInclude) references Vec3 from Math
		// and Transform from Physics. Both usings must appear.
		var files = LoadDir();
		var schema = SchemaParser.ParseWithIncludes(Find(files, "scene.fbs"), files);
		var code = new CodeEmitter(schema).Emit();

		Assert.Contains("using FlatBufferLite.Tests.MultiInclude.Math;", code);
		Assert.Contains("using FlatBufferLite.Tests.MultiInclude.Physics;", code);
		Assert.Contains("public readonly ref partial struct Scene", code);
	}

	[Fact]
	public void SubdirIncludes_TypesResolved()
	{
		var files = LoadDir();
		var missing = new List<string>();
		var schema = SchemaParser.ParseWithIncludes(Find(files, "sprite.fbs"), files, missing);

		Assert.Empty(missing);
		Assert.Empty(schema.Warnings);
		Assert.Contains("Vec2", schema.ByName.Keys);
		Assert.Contains("Vec3", schema.ByName.Keys);
		Assert.Contains("Sprite", schema.ByName.Keys);
	}

	[Fact]
	public void SubdirIncludes_SpriteLayoutCorrect()
	{
		var files = LoadDir();
		var schema = SchemaParser.ParseWithIncludes(Find(files, "sprite.fbs"), files);
		var sprite = (TableDef)schema.ByName["Sprite"];

		var pos = sprite.Fields.Single(f => f.Name == "pos");
		var normal = sprite.Fields.Single(f => f.Name == "normal");
		Assert.Equal(0, pos.InlineOffset);
		Assert.Equal(8, normal.InlineOffset);   // Vec2 = 8 bytes
		Assert.Equal(20, sprite.InlineSize);    // 8 + 12 = 20
	}

	[Fact]
	public void DiamondIncludes_NoDuplicatesNoMissingNoWarnings()
	{
		// scene.fbs includes math/vec3.fbs directly, then particle.fbs and
		// transform.fbs which each also include math/vec3.fbs. Vec3 must appear
		// exactly once despite the three paths to it.
		var files = LoadDir();
		var missing = new List<string>();
		var schema = SchemaParser.ParseWithIncludes(Find(files, "scene.fbs"), files, missing);

		Assert.Empty(missing);
		Assert.Empty(schema.Warnings);
		Assert.Single(schema.Structs, s => s.Name == "Vec3");
		Assert.Contains("Vec3", schema.ByName.Keys);
		Assert.Contains("Transform", schema.ByName.Keys);
		Assert.Contains("Particle", schema.ByName.Keys);
		Assert.Contains("Scene", schema.ByName.Keys);
	}

	[Fact]
	public void DiamondIncludes_ParticleLayoutCorrect()
	{
		var files = LoadDir();
		var schema = SchemaParser.ParseWithIncludes(Find(files, "scene.fbs"), files);
		var particle = (TableDef)schema.ByName["Particle"];

		var vel = particle.Fields.Single(f => f.Name == "vel");
		var mass = particle.Fields.Single(f => f.Name == "mass");
		// Vec3 = 12 bytes align 4; float = 4 bytes align 4
		Assert.Equal(0, vel.InlineOffset);
		Assert.Equal(12, mass.InlineOffset);
		Assert.Equal(16, particle.InlineSize);
	}

	[Fact]
	public void DiamondIncludes_TransformLayoutCorrect()
	{
		// transform.fbs is processed after math/vec3.fbs is already in visited.
		// Its intermediate sub-schema has Transform.Size=0 (Vec3 not yet in ByName).
		// The root schema's AssignStructLayout must recompute and produce Size=12.
		var files = LoadDir();
		var schema = SchemaParser.ParseWithIncludes(Find(files, "scene.fbs"), files);
		var xform = (StructDef)schema.ByName["Transform"];

		Assert.Equal(12, xform.Size);
		Assert.Equal(4, xform.Alignment);
	}

	[Fact]
	public void DiamondIncludes_EachFileEmitsOwnTypesOnly()
	{
		var files = LoadDir();
		var mathVec3Path = Find(files, Path.Combine("math", "vec3.fbs"));
		var particlePath = Find(files, "particle.fbs");
		var scenePath = Find(files, "scene.fbs");

		var vec3Code = new CodeEmitter(SchemaParser.ParseWithIncludes(mathVec3Path, files)).Emit();
		var particleCode = new CodeEmitter(SchemaParser.ParseWithIncludes(particlePath, files)).Emit();
		var sceneCode = new CodeEmitter(SchemaParser.ParseWithIncludes(scenePath, files)).Emit();

		Assert.Contains("public partial struct Vec3", vec3Code);
		Assert.DoesNotContain("Particle", vec3Code);
		Assert.DoesNotContain("Transform", vec3Code);

		Assert.Contains("public readonly ref partial struct Particle", particleCode);
		Assert.DoesNotContain("public partial struct Vec3", particleCode);
		Assert.DoesNotContain("public partial struct Transform", particleCode);

		Assert.Contains("public readonly ref partial struct Scene", sceneCode);
		Assert.DoesNotContain("public readonly ref partial struct Particle", sceneCode);
		Assert.DoesNotContain("public partial struct Vec3", sceneCode);
		Assert.DoesNotContain("public partial struct Transform", sceneCode);
	}
}
