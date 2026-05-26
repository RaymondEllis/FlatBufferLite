using FlatBufferLite.Coverage;

namespace FlatBufferLite.Tests;

public class CoverageTests
{
	[Fact]
	public void EnumDefault_AbsentField_ReturnsSchemaDefault()
	{
		Span<byte> buf = stackalloc byte[Entity.TableMaxSize];
		var b = new FlatBufferBuilder(buf);
		Entity.Create(ref b);

		var span = b.Finish();
		var e = Entity.GetRootAs(span);

		Assert.Equal(Direction.South, e.Dir);
	}

	[Fact]
	public void EnumDefault_ExplicitValue_RoundTrips()
	{
		Span<byte> buf = stackalloc byte[Entity.TableMaxSize];
		var b = new FlatBufferBuilder(buf);
		var entity = Entity.Create(ref b);
		entity.Dir = Direction.West;

		var span = b.Finish();
		var e = Entity.GetRootAs(span);

		Assert.Equal(Direction.West, e.Dir);
	}

	[Fact]
	public void VectorOfEnum_RoundTrips()
	{
		Span<byte> buf = stackalloc byte[Entity.GetMaxSize(tagsCount: 3)];
		var b = new FlatBufferBuilder(buf);
		var tags = b.CreateVector<byte>(new byte[] { (byte)Direction.North, (byte)Direction.East, (byte)Direction.West });
		Entity.Create(ref b, tags: tags);

		var span = b.Finish();
		var e = Entity.GetRootAs(span);

		Assert.Equal(3, e.Tags.Length);
		Assert.Equal((byte)Direction.North, e.Tags[0]);
		Assert.Equal((byte)Direction.East, e.Tags[1]);
		Assert.Equal((byte)Direction.West, e.Tags[2]);
	}
}