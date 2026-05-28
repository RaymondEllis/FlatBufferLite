namespace FlatBufferLite.AllTypes;

public partial struct Vec2
{
	public Vec2 Add(Vec2 other) => new() { X = X + other.X, Y = Y + other.Y };

	public Vec2 Scale(float s) => new() { X = X * s, Y = Y * s };

	public readonly float LengthSquared() => X * X + Y * Y;

	public readonly float Dot(Vec2 other) => X * other.X + Y * other.Y;
}