namespace FlatBufferLite.AllTypes;

public readonly ref partial struct Score
{
	public bool IsHighScore(long threshold) => IsValid && Value >= threshold;
}

public readonly ref partial struct Refs
{
	public bool HasVec2() => Vec2Val.X != 0f || Vec2Val.Y != 0f;
}