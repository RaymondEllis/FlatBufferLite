namespace FlatBufferLite.Tests;

public class FlatBufferBuilderTests
{
	[Fact]
	public void EmptyTable_NoFields()
	{
		Span<byte> buf = stackalloc byte[256];
		var b = new FlatBufferBuilder(buf);
		int tp = b.StartTable(0, 0, 4);
		Assert.False(Vtable.HasField(b.Buffer, tp, 4));
	}

	[Fact]
	public void SingleInt_RoundTrips()
	{
		Span<byte> buf = stackalloc byte[256];
		var b = new FlatBufferBuilder(buf);
		int tp = b.StartTable(1, 4, 4);
		Vtable.Write<int>(b.Buffer, tp, 4, 4, 12345, 0);
		Assert.Equal(12345, Vtable.Read<int>(b.Buffer, tp, 4, 0));
	}

	[Fact]
	public void DefaultValue_OmitsField()
	{
		Span<byte> buf = stackalloc byte[256];
		var b = new FlatBufferBuilder(buf);
		int tp = b.StartTable(1, 4, 4);
		Vtable.Write<int>(b.Buffer, tp, 4, 4, 42, 42);
		Assert.False(Vtable.HasField(b.Buffer, tp, 4));
		Assert.Equal(42, Vtable.Read<int>(b.Buffer, tp, 4, 42));
		Assert.Equal(99, Vtable.Read<int>(b.Buffer, tp, 4, 99));
	}

	[Fact]
	public void MultipleScalars_RoundTrip()
	{
		Span<byte> buf = stackalloc byte[256];
		var b = new FlatBufferBuilder(buf);
		int tp = b.StartTable(4, 16, 8);
		Vtable.Write<byte>(b.Buffer, tp, 4, 4, 7, 0);
		Vtable.Write<short>(b.Buffer, tp, 6, 6, 1000, 0);
		Vtable.Write<int>(b.Buffer, tp, 8, 8, 70000, 0);
		Vtable.Write<long>(b.Buffer, tp, 10, 12, 0x1234567890ABCDEFL, 0);
		Assert.Equal((byte)7, Vtable.Read<byte>(b.Buffer, tp, 4, 0));
		Assert.Equal((short)1000, Vtable.Read<short>(b.Buffer, tp, 6, 0));
		Assert.Equal(70000, Vtable.Read<int>(b.Buffer, tp, 8, 0));
		Assert.Equal(0x1234567890ABCDEFL, Vtable.Read<long>(b.Buffer, tp, 10, 0));
	}

	[Fact]
	public void StringField_RoundTrips()
	{
		Span<byte> buf = stackalloc byte[512];
		var b = new FlatBufferBuilder(buf);
		int s = b.CreateString("Hello, FlatBufferLite!"u8);
		int tp = b.StartTable(1, 4, 4);
		Vtable.WriteOffset(b.Buffer, tp, 4, 4, s);
		var str = new FlatString(b.Buffer, Vtable.ReadIndirect(b.Buffer, tp, 4));
		Assert.True(str.IsValid);
		Assert.Equal(22, str.Length);
		Assert.Equal("Hello, FlatBufferLite!", str.ToString());
	}

	[Fact]
	public void MissingString_IsInvalid()
	{
		Span<byte> buf = stackalloc byte[128];
		var b = new FlatBufferBuilder(buf);
		int tp = b.StartTable(1, 4, 4);
		var str = new FlatString(b.Buffer, Vtable.ReadIndirect(b.Buffer, tp, 4));
		Assert.False(str.IsValid);
		Assert.Equal(0, str.Length);
	}

	[Fact]
	public void VectorOfInts_RoundTrips()
	{
		ReadOnlySpan<int> data = stackalloc int[] { 1, 2, 3, 4, 5 };
		Span<byte> buf = stackalloc byte[256];
		var b = new FlatBufferBuilder(buf);
		int vec = b.CreateVector<int>(data);
		int tp = b.StartTable(1, 4, 4);
		Vtable.WriteOffset(b.Buffer, tp, 4, 4, vec);
		var v = new FlatVector<int>(b.Buffer, Vtable.ReadIndirect(b.Buffer, tp, 4));
		Assert.Equal(5, v.Length);
		var read = v.AsSpan;
		for (int i = 0; i < data.Length; i++)
			Assert.Equal(data[i], read[i]);
	}

	[Fact]
	public void VectorOfBytes_RoundTrips()
	{
		ReadOnlySpan<byte> data = stackalloc byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
		Span<byte> buf = stackalloc byte[256];
		var b = new FlatBufferBuilder(buf);
		int vec = b.CreateVector<byte>(data);
		int tp = b.StartTable(1, 4, 4);
		Vtable.WriteOffset(b.Buffer, tp, 4, 4, vec);
		var v = new FlatVector<byte>(b.Buffer, Vtable.ReadIndirect(b.Buffer, tp, 4));
		Assert.Equal(4, v.Length);
		Assert.True(v.AsSpan.SequenceEqual(data));
	}

	[Fact]
	public void FileIdentifier_Embedded()
	{
		Span<byte> buf = stackalloc byte[128];
		var b = new FlatBufferBuilder(buf);
		int tp = b.StartTable(0, 0, 4);
		b.MarkRoot(tp, "ABCD"u8);
		var span = b.AsSpan();
		Assert.True(FlatBufferReader.HasIdentifier(span, "ABCD"u8));
	}

	[Fact]
	public void NestedTables_RoundTrip()
	{
		Span<byte> buf = stackalloc byte[256];
		var b = new FlatBufferBuilder(buf);

		int childTp = b.StartTable(1, 4, 4);
		Vtable.Write<int>(b.Buffer, childTp, 4, 4, 99, 0);

		int parentTp = b.StartTable(1, 4, 4);
		Vtable.WriteOffset(b.Buffer, parentTp, 4, 4, childTp);

		int childPos = Vtable.ReadIndirect(b.Buffer, parentTp, 4);
		Assert.Equal(99, Vtable.Read<int>(b.Buffer, childPos, 4, 0));
	}

	[Fact]
	public void BufferTooSmall_Throws()
	{
		Assert.Throws<InvalidOperationException>(static () =>
		{
			Span<byte> buf = stackalloc byte[4];
			var b = new FlatBufferBuilder(buf);
			b.StartTable(4, 16, 8);
		});
	}
}