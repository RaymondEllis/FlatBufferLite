using FlatBufferLite.AllTypes;

namespace FlatBufferLite.Tests;

public class PartialTableTests
{
	[Fact]
	public void Score_PartialTable_UserMethod_ReturnsTrueAboveThreshold()
	{
		Span<byte> buf = stackalloc byte[Score.GetMaxSize()];
		var b = new FlatBufferBuilder(buf);
		var sb = Score.Create(ref b);
		sb.Value = 1000L;
		sb.MarkAsRoot(ref b);
		var span = b.Finish();

		var score = Score.GetRootAs(span);
		Assert.True(score.IsHighScore(500L));
		Assert.False(score.IsHighScore(2000L));
	}

	[Fact]
	public void Score_PartialTable_UserMethod_InvalidTable_ReturnsFalse()
	{
		var score = default(Score);
		Assert.False(score.IsHighScore(0L));
	}

	[Fact]
	public void Refs_PartialTable_UserMethod_DetectsNonZeroVec2()
	{
		Span<byte> buf = stackalloc byte[Refs.GetMaxSize()];
		var b = new FlatBufferBuilder(buf);
		Refs.Create(ref b, vec2Val: new Vec2 { X = 1.0f, Y = 0.0f });
		var span = b.Finish();

		var refs = Refs.GetRootAs(span);
		Assert.True(refs.HasVec2());
	}

	[Fact]
	public void Refs_PartialTable_UserMethod_ZeroVec2_ReturnsFalse()
	{
		Span<byte> buf = stackalloc byte[Refs.GetMaxSize()];
		var b = new FlatBufferBuilder(buf);
		Refs.Create(ref b);
		var span = b.Finish();

		var refs = Refs.GetRootAs(span);
		Assert.False(refs.HasVec2());
	}

	[Fact]
	public void PartialTable_GeneratedCode_ContainsPartialKeyword()
	{
		var source = """
            table Monster { hp: int; name: string; }
            root_type Monster;
            """;
		var schema = new FlatBufferLite.SourceGen.Parsing.SchemaParser(source).Parse();
		var code = new FlatBufferLite.SourceGen.Emit.CodeEmitter(schema).Emit();
		Assert.Contains("public readonly ref partial struct Monster", code);
	}
}