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
			Title = "bag"u8.ToArray(),
			Scores = new[] { 1, 2, 3 },
			Names = new[] { "one"u8.ToArray(), "two"u8.ToArray() },
			Qualities = new[] { Quality.Low, Quality.High },
			Items = new[]
			{
				new ItemNative
				{
					Id = 7,
					Name = "item"u8.ToArray(),
					Pos = new Vec2 { X = 3.5f, Y = -4.5f },
					Quality = Quality.High,
				},
			},
		};

		BagNative.Serialize(ref builder, in source);
		var bytes = builder.Finish();
		var read = new BagNative();
		BagNative.Deserialize(bytes, ref read);

		Assert.NotNull(read.Title);
		Assert.Equal("bag"u8.ToArray(), read.Title);
		Assert.Equal(new[] { 1, 2, 3 }, read.Scores);
		Assert.NotNull(read.Names);
		Assert.Equal("one"u8.ToArray(), read.Names[0]);
		Assert.Equal("two"u8.ToArray(), read.Names[1]);
		Assert.Equal(new[] { Quality.Low, Quality.High }, read.Qualities);
		Assert.NotNull(read.Items);
		Assert.Single(read.Items);
		Assert.Equal(7, read.Items![0].Id);
		Assert.NotNull(read.Items[0].Name);
		Assert.True(read.Items[0].Name.AsSpan().SequenceEqual("item"u8));
		Assert.Equal(3.5f, read.Items[0].Pos.X);
		Assert.Equal(-4.5f, read.Items[0].Pos.Y);
		Assert.Equal(Quality.High, read.Items[0].Quality);
	}

	[Fact]
	public void NativeStruct_MixesWithRegularTables()
	{
		Span<byte> buffer = stackalloc byte[4096];
		var builder = new FlatBufferBuilder(buffer);
		var source = new BagNative
		{
			Title = "mixed-bag"u8.ToArray(),
			Items = new[]
			{
				new ItemNative
				{
					Id = 11,
					Name = "native-item"u8.ToArray(),
					Quality = Quality.Low,
				},
			},
		};

		var bag = BagNative.Serialize(ref builder, in source);
		var labelName = builder.CreateString("regular-label"u8);
		var label = Label.Create(ref builder, name: labelName);
		Shelf.Create(ref builder, bag: bag.AsOffset, label: label.AsOffset);

		var bytes = builder.Finish();
		var read = Shelf.GetRootAs(bytes);
		var native = new BagNative();
		read.Bag.ToNative(ref native);

		Assert.NotNull(native.Title);
		Assert.True(native.Title.AsSpan().SequenceEqual("mixed-bag"u8));
		Assert.NotNull(native.Items);
		Assert.True(native.Items[0].Name.AsSpan().SequenceEqual("native-item"u8));
		Assert.True(read.Label.Name.AsBytes.SequenceEqual("regular-label"u8));
	}

	[Fact]
	public void NativeStruct_CollectionFieldsRoundTripWithProvidedLists()
	{
		SetCollectionCreates();
		Span<byte> buffer = stackalloc byte[4096];
		var builder = new FlatBufferBuilder(buffer);
		var source = new CollectionBagNative
		{
			Title = new TestFlatBufferCollection<byte> { (byte)'b', (byte)'a', (byte)'g' },
			Scores = new TestFlatBufferCollection<int> { 1, 2, 3 },
			Names = new TestFlatBufferNativeVector<IFlatBufferCollection<byte>>
			{
				new TestFlatBufferCollection<byte> { (byte)'o', (byte)'n', (byte)'e' },
				new TestFlatBufferCollection<byte> { (byte)'t', (byte)'w', (byte)'o' },
			},
			Qualities = new TestFlatBufferCollection<Quality> { Quality.Low, Quality.High },
			Items = new TestFlatBufferNativeVector<ItemNative>
			{
				new() {
					Id = 7,
					Name = "item"u8.ToArray(),
					Pos = new Vec2 { X = 3.5f, Y = -4.5f },
					Quality = Quality.High,
				},
			},
		};

		CollectionBagNative.Serialize(ref builder, in source);
		var bytes = builder.Finish();
		var read = new CollectionBagNative();
		CollectionBagNative.Deserialize(bytes, ref read);

		Assert.NotNull(read.Title);
		Assert.Equal("bag"u8.ToArray(), read.Title);
		Assert.Equal(new[] { 1, 2, 3 }, read.Scores);
		Assert.NotNull(read.Names);
		Assert.Equal("one"u8.ToArray(), read.Names[0]);
		Assert.Equal("two"u8.ToArray(), read.Names[1]);
		Assert.Equal(new[] { Quality.Low, Quality.High }, read.Qualities);
		Assert.NotNull(read.Items);
		Assert.Single(read.Items);
		Assert.Equal(7, read.Items[0].Id);
		Assert.NotNull(read.Items[0].Name);
		Assert.True(read.Items[0].Name.AsSpan().SequenceEqual("item"u8));
		Assert.Equal(3.5f, read.Items[0].Pos.X);
		Assert.Equal(-4.5f, read.Items[0].Pos.Y);
		Assert.Equal(Quality.High, read.Items[0].Quality);
	}

	static void SetCollectionCreates()
	{
		FlatBufferCollections<byte>.Create = items => new TestFlatBufferCollection<byte>(items);
		FlatBufferCollections<int>.Create = items => new TestFlatBufferCollection<int>(items);
		FlatBufferCollections<Quality>.Create = items => new TestFlatBufferCollection<Quality>(items);
		FlatBufferNativeVectors<IFlatBufferCollection<byte>>.Create = items => new TestFlatBufferNativeVector<IFlatBufferCollection<byte>>(items);
		FlatBufferNativeVectors<ItemNative>.Create = items => new TestFlatBufferNativeVector<ItemNative>(items);
	}
}