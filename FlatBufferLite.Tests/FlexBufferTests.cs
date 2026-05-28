namespace FlatBufferLite.Tests;

public class FlexBufferTests
{
	[Fact]
	public void ScalarRoots_RoundTrip()
	{
		Span<byte> buf = stackalloc byte[128];
		var b = new FlexBufferBuilder(buf);

		var bytes = b.Finish(FlexBufferValue.Int(-12345));
		Assert.Equal(-12345, FlexBuffer.GetRoot(bytes).AsInt64);

		b.Reset();
		bytes = b.Finish(FlexBufferValue.Bool(true));
		Assert.True(FlexBuffer.GetRoot(bytes).AsBool);

		b.Reset();
		bytes = b.Finish(FlexBufferValue.Float(12.5));
		Assert.Equal(12.5, FlexBuffer.GetRoot(bytes).AsDouble);
	}

	[Fact]
	public void StringBlobAndVector_RoundTrip()
	{
		Span<byte> buf = stackalloc byte[512];
		var b = new FlexBufferBuilder(buf);
		var text = b.CreateString("hello"u8);
		var blob = b.CreateBlob(stackalloc byte[] { 1, 2, 3 });
		ReadOnlySpan<FlexBufferValue> values = stackalloc FlexBufferValue[]
		{
			FlexBufferValue.Int(42),
			text,
			blob,
			FlexBufferValue.Bool(false),
		};
		var vec = b.CreateVector(values);

		var root = FlexBuffer.GetRoot(b.Finish(vec));
		Assert.Equal(FlexBufferType.Vector, root.Type);
		var read = root.AsVector;
		Assert.Equal(4, read.Length);
		Assert.Equal(42, read[0].AsInt64);
		Assert.Equal("hello", read[1].AsString);
		Assert.True(read[2].AsBlob.SequenceEqual(stackalloc byte[] { 1, 2, 3 }));
		Assert.False(read[3].AsBool);
	}

	[Fact]
	public void EmptyVector_RoundTrips()
	{
		Span<byte> buf = stackalloc byte[128];
		var b = new FlexBufferBuilder(buf);
		var vec = b.CreateVector(ReadOnlySpan<FlexBufferValue>.Empty);

		Assert.Equal(0, FlexBuffer.GetRoot(b.Finish(vec)).AsVector.Length);
	}

	[Fact]
	public void GeneratedFlexbufferAccessor_ReadsUByteVectorAsFlexBuffer()
	{
		Span<byte> flexBuf = stackalloc byte[128];
		var flexBuilder = new FlexBufferBuilder(flexBuf);
		var flexBytes = flexBuilder.Finish(FlexBufferValue.UInt(123));

		Span<byte> tableBuf = stackalloc byte[512];
		var tableBuilder = new FlatBufferBuilder(tableBuf);
		VectorOffset data = tableBuilder.CreateVector<byte>(flexBytes);
		Attr.FlexRef.Create(ref tableBuilder, data: data);
		var tableBytes = tableBuilder.Finish();

		var flex = Attr.FlexRef.GetRootAs(tableBytes).DataFlexBuffer;
		Assert.Equal(123UL, flex.AsUInt64);
	}

	[Fact]
	public void InvalidRoot_Throws()
	{
		Assert.Throws<ArgumentException>(() => FlexBuffer.GetRoot(stackalloc byte[] { 0, 1 }));
		Assert.Throws<ArgumentException>(() => FlexBuffer.GetRoot(stackalloc byte[] { 0, 3, 0 }));
	}

	[Fact]
	public void WrongType_Throws()
	{
		Assert.Throws<InvalidOperationException>(() =>
		{
			Span<byte> buf = stackalloc byte[128];
			var b = new FlexBufferBuilder(buf);
			_ = FlexBuffer.GetRoot(b.Finish(FlexBufferValue.Int(1))).AsString;
		});
	}

	[Fact]
	public void IndirectOffsetOutsideBuffer_Throws()
	{
		byte[] bytes = { 10, 1, (byte)((byte)FlexBufferType.String << 2) };

		Assert.Throws<ArgumentException>(() => FlexBuffer.GetRoot(bytes).AsString);
	}

	[Fact]
	public void BuilderBufferTooSmall_Throws()
	{
		Assert.Throws<InvalidOperationException>(() =>
		{
			Span<byte> buf = stackalloc byte[4];
			var b = new FlexBufferBuilder(buf);
			b.Finish(FlexBufferValue.Int(1));
		});
	}
}
