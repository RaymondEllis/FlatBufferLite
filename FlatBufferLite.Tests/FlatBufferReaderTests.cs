namespace FlatBufferLite.Tests;

public class FlatBufferReaderTests
{
	[Fact]
	public void ReadsScalarsLittleEndian()
	{
		var bytes = new byte[] { 0x78, 0x56, 0x34, 0x12 };
		Assert.Equal(0x12345678, FlatBufferReader.ReadUnaligned<int>(bytes, 0));
		Assert.Equal((uint)0x12345678, FlatBufferReader.ReadUnaligned<uint>(bytes, 0));
	}

	[Fact]
	public void ReadsShortLittleEndian()
	{
		var bytes = new byte[] { 0x34, 0x12 };
		Assert.Equal(0x1234, FlatBufferReader.ReadUnaligned<short>(bytes, 0));
	}

	[Fact]
	public void ReadsLongLittleEndian()
	{
		var bytes = new byte[8];
		bytes[0] = 0xEF;
		bytes[1] = 0xCD;
		bytes[2] = 0xAB;
		bytes[3] = 0x89;
		bytes[4] = 0x67;
		bytes[5] = 0x45;
		bytes[6] = 0x23;
		bytes[7] = 0x01;
		Assert.Equal(0x0123456789ABCDEFL, FlatBufferReader.ReadUnaligned<long>(bytes, 0));
	}

	[Fact]
	public void ReadsFloatAndDouble()
	{
		var f = 3.14159f;
		var d = 2.7182818284590452;
		var buf = new byte[16];
		BitConverter.TryWriteBytes(buf.AsSpan(0), f);
		BitConverter.TryWriteBytes(buf.AsSpan(4), d);
		Assert.Equal(f, FlatBufferReader.ReadUnaligned<float>(buf, 0));
		Assert.Equal(d, FlatBufferReader.ReadUnaligned<double>(buf, 4));
	}

	[Fact]
	public void HasIdentifier_MatchesAtOffset4()
	{
		var buf = new byte[8];
		"ABCD"u8.CopyTo(buf.AsSpan(4));
		Assert.True(FlatBufferReader.HasIdentifier(buf, "ABCD"u8));
		Assert.False(FlatBufferReader.HasIdentifier(buf, "XYZW"u8));
	}

	[Fact]
	public void OutOfRange_Throws()
	{
		var buf = new byte[2];
		Assert.Throws<ArgumentOutOfRangeException>(() => FlatBufferReader.ReadUnaligned<int>(buf, 0));
	}
}
