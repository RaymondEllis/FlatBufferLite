using FlatBufferLite.NativeStructs;

namespace FlatBufferLite.Tests;

public class NativeStructTests
{
	[Fact]
	public void NativeStruct_RoundTripsScalarsStringsVectorsAndNestedTables()
	{
		Span<byte> buffer = stackalloc byte[4096];
		var builder = new FlatBufferBuilder(buffer);
		var source = new BagNative
		{
			Title = "bag",
			Scores = new[] { 1, 2, 3 },
			Names = new[] { "one", "two" },
			Qualities = new[] { Quality.Low, Quality.High },
			Items = new[]
			{
				new ItemNative
				{
					Id = 7,
					Name = "item",
					Pos = new Vec2 { X = 3.5f, Y = -4.5f },
					Quality = Quality.High,
				},
			},
		};

		BagNative.Serialize(ref builder, in source);
		var bytes = builder.Finish();
		var read = BagNative.Deserialize(bytes);

		Assert.Equal("bag", read.Title);
		Assert.Equal(new[] { 1, 2, 3 }, read.Scores);
		Assert.Equal(new[] { "one", "two" }, read.Names);
		Assert.Equal(new[] { Quality.Low, Quality.High }, read.Qualities);
		Assert.NotNull(read.Items);
		Assert.Single(read.Items);
		Assert.Equal(7, read.Items![0].Id);
		Assert.Equal("item", read.Items[0].Name);
		Assert.Equal(3.5f, read.Items[0].Pos.X);
		Assert.Equal(-4.5f, read.Items[0].Pos.Y);
		Assert.Equal(Quality.High, read.Items[0].Quality);
	}
}
