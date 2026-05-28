namespace FlatBufferLite.AllTypes;

public readonly ref partial struct ScoreRef
{
	public bool IsHighScore(long threshold) => IsValid && Value >= threshold;
}

public readonly ref partial struct RefsRef
{
	public bool HasVec2() => Vec2Val.X != 0f || Vec2Val.Y != 0f;
}