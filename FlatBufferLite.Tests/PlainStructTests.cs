using FlatBufferLite.PlainStructs;

namespace FlatBufferLite.Tests;

public class PlainStructTests
{
	[Fact]
	public void PlainStruct_RoundTripsScalarsStringsVectorsAndNestedTables()
	{
		Span<byte> buffer = stackalloc byte[4096];
		var builder = new FlatBufferBuilder(buffer);
		var source = new Bag
		{
			Title = "bag"u8.ToArray(),
			Scores = new[] { 1, 2, 3 },
			Names = new[] { "one"u8.ToArray(), "two"u8.ToArray() },
			Qualities = new[] { Quality.Low, Quality.High },
			Items = new[]
			{
				new Item
				{
					Id = 7,
					Name = "item"u8.ToArray(),
					Pos = new Vec2 { X = 3.5f, Y = -4.5f },
					Quality = Quality.High,
				},
			},
		};

		Bag.Serialize(ref builder, in source);
		var bytes = builder.Finish();
		var read = new Bag();
		Bag.Deserialize(bytes, ref read);

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
	public void PlainStruct_MixesWithRegularTables()
	{
		Span<byte> buffer = stackalloc byte[4096];
		var builder = new FlatBufferBuilder(buffer);
		var source = new Bag
		{
			Title = "mixed-bag"u8.ToArray(),
			Items = new[]
			{
				new Item
				{
					Id = 11,
					Name = "plain-item"u8.ToArray(),
					Quality = Quality.Low,
				},
			},
		};

		var bag = Bag.Serialize(ref builder, in source);
		var labelName = builder.CreateString("regular-label"u8);
		var label = LabelRef.Create(ref builder, name: labelName);
		ShelfRef.Create(ref builder, bag: bag.AsOffset, label: label.AsOffset);

		var bytes = builder.Finish();
		var read = ShelfRef.GetRootAs(bytes);
		var plain = new Bag();
		read.Bag.ToPlain(ref plain);

		Assert.NotNull(plain.Title);
		Assert.True(plain.Title.AsSpan().SequenceEqual("mixed-bag"u8));
		Assert.NotNull(plain.Items);
		Assert.True(plain.Items[0].Name.AsSpan().SequenceEqual("plain-item"u8));
		Assert.True(read.Label.Name.AsBytes.SequenceEqual("regular-label"u8));
	}

	[Fact]
	public void PlainStruct_CollectionFieldsRoundTripWithProvidedLists()
	{
		var prevTitleCreate = FlatBufferCollections<byte>.Create;
		var prevScoresCreate = FlatBufferCollections<int>.Create;
		var prevQualitiesCreate = FlatBufferCollections<Quality>.Create;
		var prevNamesCreate = FlatBufferPlainVectors<IFlatBufferCollection<byte>>.Create;
		var prevItemsCreate = FlatBufferPlainVectors<Item>.Create;
		try
		{
		SetCollectionCreates();
		Span<byte> buffer = stackalloc byte[4096];
		var builder = new FlatBufferBuilder(buffer);
		var source = new CollectionBag
		{
			Title = new TestFlatBufferCollection<byte> { (byte)'b', (byte)'a', (byte)'g' },
			Scores = new TestFlatBufferCollection<int> { 1, 2, 3 },
			Names = new TestFlatBufferPlainVector<IFlatBufferCollection<byte>>
			{
				new TestFlatBufferCollection<byte> { (byte)'o', (byte)'n', (byte)'e' },
				new TestFlatBufferCollection<byte> { (byte)'t', (byte)'w', (byte)'o' },
			},
			Qualities = new TestFlatBufferCollection<Quality> { Quality.Low, Quality.High },
			Items = new TestFlatBufferPlainVector<Item>
			{
				new() {
					Id = 7,
					Name = "item"u8.ToArray(),
					Pos = new Vec2 { X = 3.5f, Y = -4.5f },
					Quality = Quality.High,
				},
			},
		};

		CollectionBag.Serialize(ref builder, in source);
		var bytes = builder.Finish();
		var read = new CollectionBag();
		CollectionBag.Deserialize(bytes, ref read);

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
		finally
		{
			FlatBufferCollections<byte>.Create = prevTitleCreate;
			FlatBufferCollections<int>.Create = prevScoresCreate;
			FlatBufferCollections<Quality>.Create = prevQualitiesCreate;
			FlatBufferPlainVectors<IFlatBufferCollection<byte>>.Create = prevNamesCreate;
			FlatBufferPlainVectors<Item>.Create = prevItemsCreate;
		}
	}

	static void SetCollectionCreates()
	{
		FlatBufferCollections<byte>.Create = items => new TestFlatBufferCollection<byte>(items);
		FlatBufferCollections<int>.Create = items => new TestFlatBufferCollection<int>(items);
		FlatBufferCollections<Quality>.Create = items => new TestFlatBufferCollection<Quality>(items);
		FlatBufferPlainVectors<IFlatBufferCollection<byte>>.Create = items => new TestFlatBufferPlainVector<IFlatBufferCollection<byte>>(items);
		FlatBufferPlainVectors<Item>.Create = items => new TestFlatBufferPlainVector<Item>(items);
	}
}
