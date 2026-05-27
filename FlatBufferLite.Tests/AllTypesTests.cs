using FlatBufferLite.AllTypes;

namespace FlatBufferLite.Tests;

public class AllTypesTests
{
	[Fact]
	public void Scalars_AllTypesRoundTrip()
	{
		Span<byte> buf = stackalloc byte[Scalars.GetMaxSize()];
		var b = new FlatBufferBuilder(buf);
		var sb = Scalars.Create(ref b);
		sb.BoolVal = true;
		sb.ByteVal = -5;
		sb.UbyteVal = 200;
		sb.ShortVal = -300;
		sb.UshortVal = 60000;
		sb.IntVal = -70000;
		sb.UintVal = 3_000_000_000u;
		sb.LongVal = -5_000_000_000L;
		sb.UlongVal = 10_000_000_000_000UL;
		sb.FloatVal = 1.5f;
		sb.DoubleVal = 3.141592653589793;
		sb.DefaultShort = 99;
		sb.DefaultBool = false;

		var span = b.Finish();
		var s = Scalars.GetRootAs(span);

		Assert.True(s.BoolVal);
		Assert.Equal((sbyte)-5, s.ByteVal);
		Assert.Equal((byte)200, s.UbyteVal);
		Assert.Equal((short)-300, s.ShortVal);
		Assert.Equal((ushort)60000, s.UshortVal);
		Assert.Equal(-70000, s.IntVal);
		Assert.Equal(3_000_000_000u, s.UintVal);
		Assert.Equal(-5_000_000_000L, s.LongVal);
		Assert.Equal(10_000_000_000_000UL, s.UlongVal);
		Assert.Equal(1.5f, s.FloatVal);
		Assert.Equal(3.141592653589793, s.DoubleVal);
		Assert.Equal((short)99, s.DefaultShort);
		Assert.False(s.DefaultBool);
	}

	[Fact]
	public void Scalars_DefaultsReturnedWhenAbsent()
	{
		Span<byte> buf = stackalloc byte[Scalars.GetMaxSize()];
		var b = new FlatBufferBuilder(buf);
		Scalars.Create(ref b);

		var span = b.Finish();
		var s = Scalars.GetRootAs(span);

		Assert.False(s.BoolVal);
		Assert.Equal((sbyte)0, s.ByteVal);
		Assert.Equal((byte)0, s.UbyteVal);
		Assert.Equal((short)0, s.ShortVal);
		Assert.Equal((ushort)0, s.UshortVal);
		Assert.Equal(0, s.IntVal);
		Assert.Equal(0u, s.UintVal);
		Assert.Equal(0L, s.LongVal);
		Assert.Equal(0UL, s.UlongVal);
		Assert.Equal(0f, s.FloatVal);
		Assert.Equal(0d, s.DoubleVal);
		Assert.Equal((short)42, s.DefaultShort);
		Assert.True(s.DefaultBool);
	}

	[Fact]
	public void Score_RoundTrips()
	{
		Span<byte> buf = stackalloc byte[Score.GetMaxSize()];
		var b = new FlatBufferBuilder(buf);
		var sb = Score.Create(ref b);
		sb.Value = 9_876_543_210L;
		sb.MarkAsRoot(ref b);
		var span = b.Finish();
		var sc = Score.GetRootAs(span);
		Assert.Equal(9_876_543_210L, sc.Value);
	}

	[Fact]
	public void Refs_AllFieldsRoundTrip()
	{
		Span<byte> buf = stackalloc byte[Refs.GetMaxSize(strValByteCount: 11)];
		var b = new FlatBufferBuilder(buf);

		var hello = b.CreateString("hello world"u8);

		var score = Score.Create(ref b, value: 42L);
		var rb = Refs.Create(ref b, strVal: hello, scoreVal: score.AsOffset, vec2Val: new Vec2 { X = 3.0f, Y = -1.5f }, colorVal: Color.Blue, permsVal: Permissions.ReadWrite);
		rb.MarkAsRoot(ref b);
		var span = b.Finish();
		var r = Refs.GetRootAs(span);

		Assert.Equal("hello world", r.StrVal.ToString());
		Assert.Equal(42L, r.ScoreVal.Value);
		var v = r.Vec2Val;
		Assert.Equal(3.0f, v.X);
		Assert.Equal(-1.5f, v.Y);
		Assert.Equal(Color.Blue, r.ColorVal);
		Assert.Equal(Permissions.ReadWrite, r.PermsVal);
	}

	[Fact]
	public void Refs_AbsentRefsAreInvalid()
	{
		Span<byte> buf = stackalloc byte[Refs.GetMaxSize()];
		var b = new FlatBufferBuilder(buf);
		var rb = Refs.Create(ref b);
		var r = new Refs(buf, rb.BufferPos);

		Assert.False(r.StrVal.IsValid);
		Assert.False(r.ScoreVal.IsValid);
		Assert.Equal(Color.Red, r.ColorVal);
		Assert.Equal(Permissions.None, r.PermsVal);
	}

	[Fact]
	public void Vectors_ScalarVectorsRoundTrip()
	{
		ReadOnlySpan<int> ints = stackalloc int[] { -1, 0, 1, int.MaxValue };
		ReadOnlySpan<byte> bytes = stackalloc byte[] { 0, 127, 255 };
		ReadOnlySpan<float> floats = stackalloc float[] { 1.0f, -1.0f, 0.5f };
		ReadOnlySpan<long> longs = stackalloc long[] { long.MinValue, 0L, long.MaxValue };

		Span<byte> buf = stackalloc byte[Vectors.GetMaxSize(intVecCount: 4, byteVecCount: 3, floatVecCount: 3, longVecCount: 3)];
		var b = new FlatBufferBuilder(buf);
		var iv = b.CreateVector(ints);
		var bv = b.CreateVector(bytes);
		var fv = b.CreateVector(floats);
		var lv = b.CreateVector(longs);

		var vb = Vectors.Create(ref b, intVec: iv, byteVec: bv, floatVec: fv, longVec: lv);
		var v = new Vectors(buf, vb.BufferPos);

		var ri = v.IntVec.AsSpan;
		Assert.Equal(4, ri.Length);
		Assert.Equal(-1, ri[0]);
		Assert.Equal(int.MaxValue, ri[3]);

		var rb2 = v.ByteVec.AsSpan;
		Assert.Equal(3, rb2.Length);
		Assert.Equal((byte)127, rb2[1]);

		var rf = v.FloatVec.AsSpan;
		Assert.Equal(3, rf.Length);
		Assert.Equal(0.5f, rf[2]);

		var rl = v.LongVec.AsSpan;
		Assert.Equal(3, rl.Length);
		Assert.Equal(long.MaxValue, rl[2]);
	}

	[Fact]
	public void Vectors_StringVector_RoundTrips()
	{
		Span<byte> buf = stackalloc byte[Vectors.GetMaxSize(strVecCount: 3, strVecByteCount: 14)];
		var b = new FlatBufferBuilder(buf);

		int s0 = b.CreateString("alpha"u8);
		int s1 = b.CreateString("beta"u8);
		int s2 = b.CreateString("gamma"u8);
		ReadOnlySpan<int> offsets = stackalloc int[] { s0, s1, s2 };
		var strVec = b.CreateVectorOfOffsets(offsets);

		var vb = Vectors.Create(ref b, strVec: strVec);
		var fv = new Vectors(buf, vb.BufferPos).StrVec;

		Assert.Equal(3, fv.Length);
		Assert.Equal("alpha", fv[0].ToString());
		Assert.Equal("beta", fv[1].ToString());
		Assert.Equal("gamma", fv[2].ToString());
	}

	[Fact]
	public void Vectors_StructVector_RoundTrips()
	{
		ReadOnlySpan<Vec2> vecs = stackalloc Vec2[]
		{
			new Vec2 { X = 1.0f, Y = 2.0f },
			new Vec2 { X = -3.5f, Y = 4.25f },
			new Vec2 { X = 0.0f, Y = -0.5f },
		};
		Span<byte> buf = stackalloc byte[Vectors.GetMaxSize(vec2VecCount: 3)];
		var b = new FlatBufferBuilder(buf);
		var vv = b.CreateVector(vecs);

		var vb = Vectors.Create(ref b, vec2Vec: vv);
		var fv = new Vectors(buf, vb.BufferPos).Vec2Vec;

		Assert.Equal(3, fv.Length);
		var s = fv.AsSpan;
		Assert.Equal(1.0f, s[0].X);
		Assert.Equal(-3.5f, s[1].X);
		Assert.Equal(-0.5f, s[2].Y);
	}

	[Fact]
	public void Vectors_EmptyVectorIsValid()
	{
		Span<byte> buf = stackalloc byte[Vectors.GetMaxSize(intVecCount: 0)];
		var b = new FlatBufferBuilder(buf);
		var empty = b.CreateVector<int>(ReadOnlySpan<int>.Empty);
		var vb = Vectors.Create(ref b, intVec: empty);
		var fv = new Vectors(buf, vb.BufferPos).IntVec;

		Assert.True(fv.IsValid);
		Assert.Equal(0, fv.Length);
	}

	[Fact]
	public void Vectors_AbsentVectorIsInvalid()
	{
		Span<byte> buf = stackalloc byte[Vectors.GetMaxSize()];
		var b = new FlatBufferBuilder(buf);
		var vb = Vectors.Create(ref b);
		var v = new Vectors(buf, vb.BufferPos);

		Assert.False(v.IntVec.IsValid);
		Assert.False(v.StrVec.IsValid);
		Assert.False(v.Vec2Vec.IsValid);
	}

	[Fact]
	public void BitFlags_GeneratedValuesArePowersOfTwo()
	{
		Assert.Equal(1u, (uint)Flags.Read);
		Assert.Equal(2u, (uint)Flags.Write);
		Assert.Equal(4u, (uint)Flags.Execute);
	}

	[Fact]
	public void BitFlags_RoundTrips()
	{
		Span<byte> buf = stackalloc byte[Flagged.GetMaxSize()];
		var b = new FlatBufferBuilder(buf);
		var fb = Flagged.Create(ref b, perms: Flags.Read | Flags.Execute);
		var f = new Flagged(buf, fb.BufferPos);
		Assert.Equal(Flags.Read | Flags.Execute, f.Perms);
		Assert.Equal(5u, (uint)f.Perms);
	}

	[Fact]
	public void BitFlags_DefaultIsZero()
	{
		Span<byte> buf = stackalloc byte[Flagged.GetMaxSize()];
		var b = new FlatBufferBuilder(buf);
		var fb = Flagged.Create(ref b);
		var f = new Flagged(buf, fb.BufferPos);
		Assert.Equal(0u, (uint)f.Perms);
	}

	[Fact]
	public void Union_CircleVariant_RoundTrips()
	{
		Span<byte> buf = stackalloc byte[WithUnion.GetMaxSize(nameByteCount: 9, tagByteCount: 11)];
		var b = new FlatBufferBuilder(buf);

		var myCircle = b.CreateString("my-circle"u8);
		var afterUnion = b.CreateString("after-union"u8);

		var cb = Circle.Create(ref b, radius: 5.0f);
		var wu = WithUnion.Create(ref b, name: myCircle, value: 99, shapeType: ShapeKind.Circle, shape: cb.BufferPos, tag: afterUnion);
		var read = new WithUnion(buf, wu.BufferPos);

		Assert.Equal("my-circle", read.Name.ToString());
		Assert.Equal(99, read.Value);
		Assert.Equal(ShapeKind.Circle, read.ShapeType);
		var s = read.Shape;
		Assert.True(s.HasValue);
		Assert.True(s.TryGetAsCircle(out var circle));
		Assert.Equal(5.0f, circle.Radius);
		Assert.False(s.TryGetAsRectangle(out _));
		Assert.Equal("after-union", read.Tag.ToString());
	}

	[Fact]
	public void Union_RectangleVariant_RoundTrips()
	{
		Span<byte> buf = stackalloc byte[WithUnion.GetMaxSize(nameByteCount: 7)];
		var b = new FlatBufferBuilder(buf);

		var nameOff = b.CreateString("my-rect"u8);

		var rb = Rectangle.Create(ref b, width: 3.0f, height: 7.5f);
		var wu = WithUnion.Create(ref b, name: nameOff, value: 7, shapeType: ShapeKind.Rectangle, shape: rb.BufferPos);
		var read = new WithUnion(buf, wu.BufferPos);

		Assert.Equal(ShapeKind.Rectangle, read.ShapeType);
		var s = read.Shape;
		Assert.True(s.TryGetAsRectangle(out var rect));
		Assert.Equal(3.0f, rect.Width);
		Assert.Equal(7.5f, rect.Height);
		Assert.False(s.TryGetAsCircle(out _));
	}

	[Fact]
	public void Union_AbsentUnionIsNone()
	{
		Span<byte> buf = stackalloc byte[WithUnion.GetMaxSize()];
		var b = new FlatBufferBuilder(buf);
		var wu = WithUnion.Create(ref b);
		var read = new WithUnion(buf, wu.BufferPos);

		Assert.Equal(ShapeKind.NONE, read.ShapeType);
		Assert.False(read.Shape.HasValue);
	}

	[Fact]
	public void Union_FieldAfterUnion_CorrectVTableOffset()
	{
		Span<byte> buf = stackalloc byte[WithUnion.GetMaxSize(tagByteCount: 8)];
		var b = new FlatBufferBuilder(buf);

		var sentinel = b.CreateString("sentinel"u8);

		var cb = Circle.Create(ref b, radius: 1.0f);
		var wu = WithUnion.Create(ref b, shapeType: ShapeKind.Circle, shape: cb.BufferPos, tag: sentinel);
		var read = new WithUnion(buf, wu.BufferPos);

		Assert.Equal("sentinel", read.Tag.ToString());
		Assert.Equal(ShapeKind.Circle, read.ShapeType);
	}
}