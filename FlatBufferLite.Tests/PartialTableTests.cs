using FlatBufferLite.AllTypes;

namespace FlatBufferLite.Tests;

public class PartialTableTests
{
	[Fact]
	public void Score_PartialTable_UserMethod_ReturnsTrueAboveThreshold()
	{
		Span<byte> buf = stackalloc byte[ScoreRef.GetMaxSize()];
		var b = new FlatBufferBuilder(buf);
		var sb = ScoreRef.Create(ref b);
		sb.Value = 1000L;
		sb.MarkAsRoot(ref b);
		var span = b.Finish();

		var score = ScoreRef.GetRootAs(span);
		Assert.True(score.IsHighScore(500L));
		Assert.False(score.IsHighScore(2000L));
	}

	[Fact]
	public void Score_PartialTable_UserMethod_InvalidTable_ReturnsFalse()
	{
		var score = default(ScoreRef);
		Assert.False(score.IsHighScore(0L));
	}

	[Fact]
	public void Refs_PartialTable_UserMethod_DetectsNonZeroVec2()
	{
		Span<byte> buf = stackalloc byte[RefsRef.GetMaxSize(strValByteCount: 0)];
		var b = new FlatBufferBuilder(buf);
		RefsRef.Create(ref b, vec2Val: new Vec2 { X = 1.0f, Y = 0.0f });
		var span = b.Finish();

		var refs = RefsRef.GetRootAs(span);
		Assert.True(refs.HasVec2());
	}

	[Fact]
	public void Refs_PartialTable_UserMethod_ZeroVec2_ReturnsFalse()
	{
		Span<byte> buf = stackalloc byte[RefsRef.GetMaxSize(strValByteCount: 0)];
		var b = new FlatBufferBuilder(buf);
		RefsRef.Create(ref b);
		var span = b.Finish();

		var refs = RefsRef.GetRootAs(span);
		Assert.False(refs.HasVec2());
	}

}
