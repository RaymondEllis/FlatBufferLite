using FlatBufferLite.Unions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlatBufferLite.Tests;

public class UnionTests
{
	[StructLayout(LayoutKind.Explicit, Size = 8)]
	public struct PointI { [FieldOffset(0)] public int X; [FieldOffset(4)] public int Y; }

	[StructLayout(LayoutKind.Explicit, Size = 8)]
	public struct PointF { [FieldOffset(0)] public float X; [FieldOffset(4)] public float Y; }

	[Union]
	[StructLayout(LayoutKind.Explicit, Size = 12)]
	public struct ContiguousShape : IUnion
	{
		[FieldOffset(8)] public byte Tag;
		[FieldOffset(0)] public PointI AsInt;
		[FieldOffset(0)] public PointF AsFloat;

		public readonly bool HasValue => Tag != 0;
		public object? Value => throw new NotImplementedException("Boxing not supported. Use TryGetValue.");

		public static ContiguousShape From(PointI v) => new() { AsInt = v, Tag = 1 };
		public static ContiguousShape From(PointF v) => new() { AsFloat = v, Tag = 2 };

		public readonly bool TryGetValue(out PointI value)
		{
			if (Tag == 1)
			{
				value = AsInt;
				return true;
			}
			value = default;
			return false;
		}
		public readonly bool TryGetValue(out PointF value)
		{
			if (Tag == 2)
			{
				value = AsFloat;
				return true;
			}
			value = default;
			return false;
		}
	}

	[Fact]
	public void Contiguous_Default_IsNone()
	{
		ContiguousShape s = default;
		Assert.False(s.HasValue);
		Assert.Equal(0, s.Tag);
	}

	[Fact]
	public void Contiguous_IntVariant_TryGet_Works()
	{
		var s = ContiguousShape.From(new PointI { X = 10, Y = 20 });
		Assert.True(s.HasValue);
		Assert.Equal(1, s.Tag);
		Assert.True(s.TryGetValue(out PointI v));
		Assert.Equal(10, v.X);
		Assert.Equal(20, v.Y);
		Assert.False(s.TryGetValue(out PointF _));
	}

	[Fact]
	public void Contiguous_FloatVariant_TryGet_Works()
	{
		var s = ContiguousShape.From(new PointF { X = 1.5f, Y = -2.5f });
		Assert.True(s.TryGetValue(out PointF v));
		Assert.Equal(1.5f, v.X);
		Assert.Equal(-2.5f, v.Y);
	}

	[Fact]
	public void Contiguous_ArrayOfUnions_IsContiguous()
	{
		var arr = new ContiguousShape[4];
		arr[0] = ContiguousShape.From(new PointI { X = 1, Y = 2 });
		arr[1] = ContiguousShape.From(new PointF { X = 3f, Y = 4f });
		arr[2] = default;
		arr[3] = ContiguousShape.From(new PointI { X = 5, Y = 6 });

		Assert.Equal(12, Marshal.SizeOf<ContiguousShape>());
		Assert.True(arr[0].TryGetValue(out PointI a));
		Assert.Equal(1, a.X);
		Assert.True(arr[1].TryGetValue(out PointF b));
		Assert.Equal(3f, b.X);
		Assert.False(arr[2].HasValue);
		Assert.True(arr[3].TryGetValue(out PointI d));
		Assert.Equal(5, d.X);
	}

	[Fact]
	public void Contiguous_TryGetValue_DoesNotAllocate()
	{
		var s = ContiguousShape.From(new PointI { X = 1, Y = 2 });
		long before = GC.GetAllocatedBytesForCurrentThread();
		for (int i = 0; i < 1000; i++)
			_ = s.TryGetValue(out PointI _);
		long after = GC.GetAllocatedBytesForCurrentThread();
		Assert.Equal(0, after - before);
	}

	[Fact]
	public void SourceGen_ShapeKind_TagValues()
	{
		Assert.Equal(0, (byte)ShapeKind.NONE);
		Assert.Equal(1, (byte)ShapeKind.Rect);
		Assert.Equal(2, (byte)ShapeKind.Circle);
	}

	[Fact]
	public void SourceGen_RectVariant_RoundTrips()
	{
		Span<byte> buf = stackalloc byte[Scene.GetMaxSize(nameByteCount: 10) + Rect.TableMaxSize];
		var b = new FlatBufferBuilder(buf);

		var sceneName = b.CreateString("scene-rect"u8);

		var rectB = Rect.Create(ref b, w: 3.0f, h: 7.0f);
		Scene.Create(ref b, name: sceneName, shapeType: ShapeKind.Rect, shape: rectB.BufferPos, count: 42);

		var span = b.Finish();
		var read = Scene.GetRootAs(span);

		Assert.Equal(ShapeKind.Rect, read.ShapeType);
		Assert.Equal(42, read.Count);
		var s = read.Shape;
		Assert.True(s.HasValue);
		Assert.True(s.TryGetAsRect(out var rect));
		Assert.Equal(3.0f, rect.W);
		Assert.Equal(7.0f, rect.H);
		Assert.False(s.TryGetAsCircle(out _));
	}

	[Fact]
	public void SourceGen_CircleVariant_RoundTrips()
	{
		Span<byte> buf = stackalloc byte[Scene.TableMaxSize + Circle.TableMaxSize];
		var b = new FlatBufferBuilder(buf);

		var cb = Circle.Create(ref b, r: 5.5f);
		Scene.Create(ref b, shapeType: ShapeKind.Circle, shape: cb.BufferPos, count: 7);

		var span = b.Finish();
		var read = Scene.GetRootAs(span);

		Assert.Equal(ShapeKind.Circle, read.ShapeType);
		Assert.Equal(7, read.Count);
		var s = read.Shape;
		Assert.True(s.TryGetAsCircle(out var circle));
		Assert.Equal(5.5f, circle.R);
		Assert.False(s.TryGetAsRect(out _));
	}

	[Fact]
	public void SourceGen_AbsentUnion_IsNone()
	{
		Span<byte> buf = stackalloc byte[Scene.TableMaxSize];
		var b = new FlatBufferBuilder(buf);
		Scene.Create(ref b);

		var span = b.Finish();
		var read = Scene.GetRootAs(span);

		Assert.Equal(ShapeKind.NONE, read.ShapeType);
		Assert.False(read.Shape.HasValue);
		Assert.Equal(0, read.Count);
	}

	[Fact]
	public void SourceGen_FieldAfterUnion_CorrectOffset()
	{
		Span<byte> buf = stackalloc byte[Scene.TableMaxSize + Circle.TableMaxSize];
		var b = new FlatBufferBuilder(buf);

		var cb = Circle.Create(ref b, r: 1.0f);
		Scene.Create(ref b, shapeType: ShapeKind.Circle, shape: cb.BufferPos, count: 99);

		var span = b.Finish();
		var read = Scene.GetRootAs(span);

		Assert.Equal(99, read.Count);
		Assert.Equal(ShapeKind.Circle, read.ShapeType);
	}
}