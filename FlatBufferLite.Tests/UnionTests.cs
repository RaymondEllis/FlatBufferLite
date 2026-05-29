using FlatBufferLite.Coverage;
using FlatBufferLite.MixedUnions;
using FlatBufferLite.PlainStructs;
using FlatBufferLite.Unions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlatBufferLite.Tests;

public class UnionTests
{
	[Fact]
	public void GeneratedUnionTypes_UseUnionInterfaceAndAttribute()
	{
		AssertGeneratedUnion(typeof(PointOrSize));
		AssertGeneratedUnion(typeof(DirectionOrPoint));
		AssertGeneratedUnion(typeof(DirectionOrPointPlain));
		AssertGeneratedUnion(typeof(ShapeRef));
		AssertGeneratedUnion(typeof(Shape));
		AssertGeneratedUnion(typeof(MixedValueRef));
		AssertGeneratedUnion(typeof(MixedValue));
		AssertGeneratedUnion(typeof(PlainShapeRef));
		AssertGeneratedUnion(typeof(PlainShape));
	}

	static void AssertGeneratedUnion(Type type)
	{
		Assert.True(typeof(IUnion).IsAssignableFrom(type));
		Assert.NotNull(Attribute.GetCustomAttribute(type, typeof(UnionAttribute)));
	}

	[Fact]
	public void ContiguousUnion_Default_IsNone()
	{
		PointOrSize shape = default;

		Assert.False(shape.HasValue);
		Assert.Equal(0, shape.Tag);
		Assert.False(shape.TryGetValue(out Point _));
		Assert.False(shape.TryGetValue(out Size _));
	}

	[Fact]
	public void ContiguousUnion_EnumVariant_Works()
	{
		DirectionOrPoint value = Direction.West;

		Assert.True(value.HasValue);
		Assert.Equal(1, value.Tag);
		Assert.True(value.TryGetValue(out Direction direction));
		Assert.Equal(Direction.West, direction);
		Assert.False(value.TryGetValue(out Point _));
	}

	[Fact]
	public void ContiguousUnion_EnumAndStructUnion_PointVariant_Works()
	{
		DirectionOrPoint value = new Point { X = 11, Y = 12 };

		Assert.True(value.HasValue);
		Assert.Equal(2, value.Tag);
		Assert.True(value.TryGetValue(out Point point));
		Assert.Equal(11, point.X);
		Assert.Equal(12, point.Y);
		Assert.False(value.TryGetValue(out Direction _));
	}

	[Fact]
	public void ContiguousUnion_UnknownTag_IsNone()
	{
		PointOrSize shape = default;
		shape.Tag = 99;

		Assert.False(shape.HasValue);
		Assert.False(shape.TryGetValue(out Point _));
		Assert.False(shape.TryGetValue(out Size _));
	}

	[Fact]
	public void ContiguousUnion_PointVariant_Works()
	{
		PointOrSize shape = new Point { X = 10, Y = 20 };

		Assert.True(shape.HasValue);
		Assert.Equal(1, shape.Tag);
		Assert.True(shape.TryGetValue(out Point point));
		Assert.Equal(10, point.X);
		Assert.Equal(20, point.Y);
		Assert.False(shape.TryGetValue(out Size _));
	}

	[Fact]
	public void ContiguousUnion_SizeVariant_Works()
	{
		PointOrSize shape = new Size { W = 30, H = 40 };

		Assert.True(shape.HasValue);
		Assert.Equal(2, shape.Tag);
		Assert.True(shape.TryGetValue(out Size size));
		Assert.Equal(30, size.W);
		Assert.Equal(40, size.H);
		Assert.False(shape.TryGetValue(out Point _));
	}

	[Fact]
	public void ContiguousUnion_Array_IsPacked()
	{
		var shapes = new PointOrSize[4];
		shapes[0] = new Point { X = 1, Y = 2 };
		shapes[1] = new Size { W = 3, H = 4 };
		shapes[2] = default;
		shapes[3] = new Point { X = 5, Y = 6 };

		Assert.Equal(12, Marshal.SizeOf<PointOrSize>());
		Assert.True(shapes[0].TryGetValue(out Point first));
		Assert.Equal(1, first.X);
		Assert.True(shapes[1].TryGetValue(out Size second));
		Assert.Equal(3, second.W);
		Assert.False(shapes[2].HasValue);
		Assert.True(shapes[3].TryGetValue(out Point fourth));
		Assert.Equal(5, fourth.X);
	}

	[Fact]
	public void ContiguousUnion_TryGetValue_DoesNotAllocate()
	{
		PointOrSize shape = new Point { X = 1, Y = 2 };

		long before = GC.GetAllocatedBytesForCurrentThread();
		for (int i = 0; i < 1000; i++)
			_ = shape.TryGetValue(out Point _);
		long after = GC.GetAllocatedBytesForCurrentThread();

		Assert.Equal(0, after - before);
	}

	[Fact]
	public void ContiguousUnion_Value_ThrowsNoBoxing()
	{
		PointOrSize shape = new Point { X = 1, Y = 2 };

		var ex = Assert.Throws<NotImplementedException>(() => shape.Value);
		Assert.Equal("No boxing allowed.", ex.Message);
	}

	[Fact]
	public void RefUnion_TagValues_AreStable()
	{
		Assert.Equal(0, (byte)ShapeKind.NONE);
		Assert.Equal(1, (byte)ShapeKind.Rect);
		Assert.Equal(2, (byte)ShapeKind.Circle);
	}

	[Fact]
	public void RefUnion_RectVariant_RoundTrips()
	{
		Span<byte> buffer = stackalloc byte[SceneRef.GetMaxSize(nameByteCount: 10)];
		var builder = new FlatBufferBuilder(buffer);

		var sceneName = builder.CreateString("scene-rect"u8);
		var rect = RectRef.Create(ref builder, w: 3.0f, h: 7.0f);
		SceneRef.Create(ref builder, name: sceneName, shapeType: ShapeKind.Rect, shape: rect.BufferPos, count: 42);

		var bytes = builder.Finish();
		var read = SceneRef.GetRootAs(bytes);

		Assert.Equal(ShapeKind.Rect, read.ShapeType);
		Assert.Equal(42, read.Count);
		Assert.True(read.Shape.HasValue);
		Assert.True(read.Shape.TryGetAsRect(out var readRect));
		Assert.Equal(3.0f, readRect.W);
		Assert.Equal(7.0f, readRect.H);
		Assert.False(read.Shape.TryGetAsCircle(out _));
	}

	[Fact]
	public void RefUnion_CircleVariant_RoundTrips()
	{
		Span<byte> buffer = stackalloc byte[SceneRef.GetMaxSize()];
		var builder = new FlatBufferBuilder(buffer);

		var circle = CircleRef.Create(ref builder, r: 5.5f);
		SceneRef.Create(ref builder, shapeType: ShapeKind.Circle, shape: circle.BufferPos, count: 7);

		var bytes = builder.Finish();
		var read = SceneRef.GetRootAs(bytes);

		Assert.Equal(ShapeKind.Circle, read.ShapeType);
		Assert.Equal(7, read.Count);
		Assert.True(read.Shape.HasValue);
		Assert.True(read.Shape.TryGetAsCircle(out var readCircle));
		Assert.Equal(5.5f, readCircle.R);
		Assert.False(read.Shape.TryGetAsRect(out _));
	}

	[Fact]
	public void RefUnion_NoneVariant_RoundTrips()
	{
		Span<byte> buffer = stackalloc byte[SceneRef.GetMaxSize()];
		var builder = new FlatBufferBuilder(buffer);
		SceneRef.Create(ref builder);

		var bytes = builder.Finish();
		var read = SceneRef.GetRootAs(bytes);

		Assert.Equal(ShapeKind.NONE, read.ShapeType);
		Assert.False(read.Shape.HasValue);
		Assert.Equal(0, read.Count);
	}

	[Fact]
	public void RefUnion_FieldAfterUnion_UsesCorrectOffset()
	{
		Span<byte> buffer = stackalloc byte[SceneRef.GetMaxSize()];
		var builder = new FlatBufferBuilder(buffer);

		var circle = CircleRef.Create(ref builder, r: 1.0f);
		SceneRef.Create(ref builder, shapeType: ShapeKind.Circle, shape: circle.BufferPos, count: 99);

		var bytes = builder.Finish();
		var read = SceneRef.GetRootAs(bytes);

		Assert.Equal(99, read.Count);
		Assert.Equal(ShapeKind.Circle, read.ShapeType);
	}

	[Fact]
	public void RefUnion_CanBeCreatedFromMemberRef()
	{
		Span<byte> buffer = stackalloc byte[SceneRef.GetMaxSize()];
		var builder = new FlatBufferBuilder(buffer);

		var rect = RectRef.Create(ref builder, w: 4.0f, h: 8.0f);
		ShapeRef shape = rect;

		Assert.True(shape.HasValue);
		Assert.True(shape.TryGetAsRect(out var readRect));
		Assert.Equal(4.0f, readRect.W);
		Assert.Equal(8.0f, readRect.H);
		Assert.False(shape.TryGetAsCircle(out _));
	}

	[Fact]
	public void RefUnion_Value_ThrowsNoBoxing()
	{
		Span<byte> buffer = stackalloc byte[SceneRef.GetMaxSize()];
		var builder = new FlatBufferBuilder(buffer);
		var rect = RectRef.Create(ref builder, w: 4.0f, h: 8.0f);
		ShapeRef shape = rect;

		try
		{
			_ = shape.Value;
			Assert.Fail("Expected NotImplementedException.");
		}
		catch (NotImplementedException ex)
		{
			Assert.Equal("No boxing allowed.", ex.Message);
		}
	}

	[Fact]
	public void OpaqueRefUnion_TableVariant_RoundTripsTagAndPresence()
	{
		Span<byte> buffer = stackalloc byte[512];
		var builder = new FlatBufferBuilder(buffer);

		var circle = MixedCircleRef.Create(ref builder, r: 2.5f);
		MixedSceneRef.Create(ref builder, valueType: MixedValueKind.MixedCircle, value: circle.BufferPos, after: 123);

		var bytes = builder.Finish();
		var read = MixedSceneRef.GetRootAs(bytes);

		Assert.Equal(MixedValueKind.MixedCircle, read.ValueType);
		Assert.True(read.Value.HasValue);
		Assert.True(read.Value.TryGetAsMixedCircle(out var readCircle));
		Assert.Equal(2.5f, readCircle.R);
		Assert.Equal(123, read.After);
	}

	[Fact]
	public void OpaqueRefUnion_Value_ThrowsNoBoxing()
	{
		Span<byte> buffer = stackalloc byte[512];
		var builder = new FlatBufferBuilder(buffer);
		var circle = MixedCircleRef.Create(ref builder, r: 3.5f);
		MixedSceneRef.Create(ref builder, valueType: MixedValueKind.MixedCircle, value: circle.BufferPos, after: 1);

		var bytes = builder.Finish();
		var read = MixedSceneRef.GetRootAs(bytes);

		try
		{
			_ = read.Value.Value;
			Assert.Fail("Expected NotImplementedException.");
		}
		catch (NotImplementedException ex)
		{
			Assert.Equal("No boxing allowed.", ex.Message);
		}
	}

	[Fact]
	public void OpaqueRefUnion_NoneVariant_RoundTrips()
	{
		Span<byte> buffer = stackalloc byte[512];
		var builder = new FlatBufferBuilder(buffer);
		MixedSceneRef.Create(ref builder, after: 77);

		var bytes = builder.Finish();
		var read = MixedSceneRef.GetRootAs(bytes);

		Assert.Equal(MixedValueKind.NONE, read.ValueType);
		Assert.False(read.Value.HasValue);
		Assert.Equal(77, read.After);
	}

	[Fact]
	public void PlainUnion_Default_IsNone()
	{
		PlainShape shape = default;

		Assert.False(shape.HasValue);
		Assert.Equal(PlainShapeKind.NONE, shape.Kind);
		Assert.False(shape.TryGetValue(out PlainCircle _));
		Assert.False(shape.TryGetValue(out PlainRectangle _));
	}

	[Fact]
	public void PlainUnion_CircleVariant_Works()
	{
		var circle = new PlainCircle
		{
			Radius = 6.25f,
			Label = "circle"u8.ToArray(),
		};
		PlainShape shape = circle;

		Assert.True(shape.HasValue);
		Assert.Equal(PlainShapeKind.PlainCircle, shape.Kind);
		Assert.True(shape.TryGetValue(out PlainCircle readCircle));
		Assert.Equal(6.25f, readCircle.Radius);
		Assert.NotNull(readCircle.Label);
		Assert.True(readCircle.Label.AsSpan().SequenceEqual("circle"u8));
		Assert.False(shape.TryGetValue(out PlainRectangle _));
	}

	[Fact]
	public void PlainUnion_RectangleVariant_Works()
	{
		var rectangle = new PlainRectangle { Width = 3.5f, Height = 8.25f };
		PlainShape shape = rectangle;

		Assert.True(shape.HasValue);
		Assert.Equal(PlainShapeKind.PlainRectangle, shape.Kind);
		Assert.True(shape.TryGetValue(out PlainRectangle readRectangle));
		Assert.Equal(3.5f, readRectangle.Width);
		Assert.Equal(8.25f, readRectangle.Height);
		Assert.False(shape.TryGetValue(out PlainCircle _));
	}

	[Fact]
	public void PlainUnion_EnumVariant_Works()
	{
		DirectionOrPointPlain value = Direction.East;

		Assert.True(value.HasValue);
		Assert.Equal(DirectionOrPointKind.Direction, value.Kind);
		Assert.True(value.TryGetValue(out Direction direction));
		Assert.Equal(Direction.East, direction);
		Assert.False(value.TryGetValue(out Point _));
	}

	[Fact]
	public void PlainUnion_StructVariant_Works()
	{
		DirectionOrPointPlain value = new Point { X = 13, Y = 14 };

		Assert.True(value.HasValue);
		Assert.Equal(DirectionOrPointKind.Point, value.Kind);
		Assert.True(value.TryGetValue(out Point point));
		Assert.Equal(13, point.X);
		Assert.Equal(14, point.Y);
		Assert.False(value.TryGetValue(out Direction _));
	}

	[Fact]
	public void PlainUnion_MixedRefUnionStructVariant_Works()
	{
		MixedValue value = new MixedPoint { X = 21, Y = 22 };

		Assert.True(value.HasValue);
		Assert.Equal(MixedValueKind.MixedPoint, value.Kind);
		Assert.True(value.TryGetValue(out MixedPoint point));
		Assert.Equal(21, point.X);
		Assert.Equal(22, point.Y);
		Assert.False(value.TryGetValue(out Offset<MixedCircleRef> _));
	}

	[Fact]
	public void PlainUnion_Value_ThrowsNoBoxing()
	{
		var rectangle = new PlainRectangle { Width = 3.5f, Height = 8.25f };
		PlainShape shape = rectangle;

		var ex = Assert.Throws<NotImplementedException>(() => shape.Value);
		Assert.Equal("No boxing allowed.", ex.Message);
	}

	[Fact]
	public void PlainUnion_RefAccessorReadsPlainTableMembers()
	{
		Span<byte> buffer = stackalloc byte[4096];
		var builder = new FlatBufferBuilder(buffer);
		var circle = new PlainCircle
		{
			Radius = 2.25f,
			Label = "ref-circle"u8.ToArray(),
		};
		var circleRef = PlainCircle.Serialize(ref builder, in circle);
		RefShapeHolderRef.Create(ref builder, shapeType: PlainShapeKind.PlainCircle, shape: circleRef.BufferPos);

		var bytes = builder.Finish();
		var read = RefShapeHolderRef.GetRootAs(bytes);

		Assert.Equal(PlainShapeKind.PlainCircle, read.ShapeType);
		Assert.True(read.Shape.HasValue);
		Assert.True(read.Shape.TryGetAsPlainCircle(out var readCircleRef));
		var readCircle = default(PlainCircle);
		readCircleRef.ToPlain(ref readCircle);
		Assert.Equal(2.25f, readCircle.Radius);
		Assert.NotNull(readCircle.Label);
		Assert.True(readCircle.Label.AsSpan().SequenceEqual("ref-circle"u8));
	}

	[Fact]
	public void PlainUnion_CanCarryRefUnionTableOffset()
	{
		Span<byte> buffer = stackalloc byte[512];
		var builder = new FlatBufferBuilder(buffer);
		var circle = MixedCircleRef.Create(ref builder, r: 6.75f);
		var circleOffset = circle.AsOffset;
		var value = new MixedValue(in circleOffset);

		Assert.True(value.HasValue);
		Assert.Equal(MixedValueKind.MixedCircle, value.Kind);
		Assert.True(value.TryGetValue(out Offset<MixedCircleRef> readOffset));
		Assert.Equal(circle.BufferPos, readOffset.Value);
	}

	[Fact]
	public void PlainStruct_WithRefUnionOffset_RoundTrips()
	{
		Span<byte> buffer = stackalloc byte[1024];
		var builder = new FlatBufferBuilder(buffer);
		var circle = MixedCircleRef.Create(ref builder, r: 8.5f);
		var circleOffset = circle.AsOffset;
		var source = new MixedPlainHolder
		{
			Value = new MixedValue(in circleOffset),
		};

		MixedPlainHolder.Serialize(ref builder, in source);
		var bytes = builder.Finish();
		var readRef = MixedPlainHolderRef.GetRootAs(bytes);
		var readPlain = new MixedPlainHolder();
		MixedPlainHolder.Deserialize(bytes, ref readPlain);

		Assert.Equal(MixedValueKind.MixedCircle, readRef.ValueType);
		Assert.True(readRef.Value.TryGetAsMixedCircle(out var readCircle));
		Assert.Equal(8.5f, readCircle.R);
		Assert.True(readPlain.Value.HasValue);
		Assert.Equal(MixedValueKind.MixedCircle, readPlain.Value.Kind);
		Assert.True(readPlain.Value.TryGetValue(out Offset<MixedCircleRef> readOffset));
		Assert.True(readOffset.Value > 0);
	}

	[Fact]
	public void PlainUnion_CircleField_RoundTrips()
	{
		Span<byte> buffer = stackalloc byte[4096];
		var builder = new FlatBufferBuilder(buffer);
		var circle = new PlainCircle
		{
			Radius = 6.25f,
			Label = "circle"u8.ToArray(),
		};
		var source = new ShapeHolder
		{
			Name = "holder"u8.ToArray(),
			Shape = new PlainShape(in circle),
		};

		ShapeHolder.Serialize(ref builder, in source);
		var bytes = builder.Finish();
		var read = new ShapeHolder();
		ShapeHolder.Deserialize(bytes, ref read);

		Assert.NotNull(read.Name);
		Assert.Equal("holder"u8.ToArray(), read.Name);
		Assert.True(read.Shape.HasValue);
		Assert.Equal(PlainShapeKind.PlainCircle, read.Shape.Kind);
		Assert.NotNull(read.Shape.PlainCircle);
		Assert.Null(read.Shape.PlainRectangle);
		Assert.True(read.Shape.TryGetValue(out PlainCircle readCircle));
		Assert.Equal(6.25f, readCircle.Radius);
		Assert.NotNull(readCircle.Label);
		Assert.True(readCircle.Label.AsSpan().SequenceEqual("circle"u8));
	}

	[Fact]
	public void PlainUnion_RectangleField_RoundTrips()
	{
		Span<byte> buffer = stackalloc byte[4096];
		var builder = new FlatBufferBuilder(buffer);
		var rectangle = new PlainRectangle { Width = 4.5f, Height = 9.5f };
		var source = new ShapeHolder
		{
			Name = "rectangle-holder"u8.ToArray(),
			Shape = new PlainShape(in rectangle),
		};

		ShapeHolder.Serialize(ref builder, in source);
		var bytes = builder.Finish();
		var read = new ShapeHolder();
		ShapeHolder.Deserialize(bytes, ref read);

		Assert.NotNull(read.Name);
		Assert.Equal("rectangle-holder"u8.ToArray(), read.Name);
		Assert.True(read.Shape.HasValue);
		Assert.Equal(PlainShapeKind.PlainRectangle, read.Shape.Kind);
		Assert.Null(read.Shape.PlainCircle);
		Assert.NotNull(read.Shape.PlainRectangle);
		Assert.True(read.Shape.TryGetValue(out PlainRectangle readRectangle));
		Assert.Equal(4.5f, readRectangle.Width);
		Assert.Equal(9.5f, readRectangle.Height);
	}

	[Fact]
	public void PlainUnion_NoneField_RoundTrips()
	{
		Span<byte> buffer = stackalloc byte[4096];
		var builder = new FlatBufferBuilder(buffer);
		var source = new ShapeHolder { Name = "empty"u8.ToArray() };

		ShapeHolder.Serialize(ref builder, in source);
		var bytes = builder.Finish();
		var read = new ShapeHolder();
		ShapeHolder.Deserialize(bytes, ref read);

		Assert.NotNull(read.Name);
		Assert.Equal("empty"u8.ToArray(), read.Name);
		Assert.False(read.Shape.HasValue);
		Assert.Equal(PlainShapeKind.NONE, read.Shape.Kind);
		Assert.Null(read.Shape.PlainCircle);
		Assert.Null(read.Shape.PlainRectangle);
	}
}
