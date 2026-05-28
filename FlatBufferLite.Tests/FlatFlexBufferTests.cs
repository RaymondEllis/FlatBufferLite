namespace FlatBufferLite.Tests;

public class FlatFlexBufferTests
{
	[Fact]
	public void RootInt_CanBeRead()
	{
		Span<byte> bytes = stackalloc byte[] { 42, 4, 1 };
		var flex = new FlatFlexBuffer(bytes);
		Assert.True(flex.IsValid);
		Assert.Equal(FlexBufferType.Int, flex.Root.Type);
		Assert.Equal(42, flex.Root.AsInt64());
	}

	[Fact]
	public void RootString_CanBeRead()
	{
		Span<byte> bytes = stackalloc byte[] { 3, (byte)'a', (byte)'b', (byte)'c', 0, 4, 20, 1 };
		var flex = new FlatFlexBuffer(bytes);
		Assert.True(flex.Root.TryGetStringBytes(out var utf8));
		Assert.Equal("abc", System.Text.Encoding.UTF8.GetString(utf8));
	}

	[Fact]
	public void FlexBufferField_GeneratedAccessor_RoundTrips()
	{
		Span<byte> buf = stackalloc byte[256];
		var b = new FlatBufferBuilder(buf);
		Span<byte> flex = stackalloc byte[] { 42, 4, 1 };
		var data = b.CreateFlexBuffer(flex);
		FlatBufferLite.Attr.FlexRef.Create(ref b, data: data);

		var bytes = b.Finish();
		var read = FlatBufferLite.Attr.FlexRef.GetRootAs(bytes);
		Assert.Equal(42, read.DataFlex.Root.AsInt64());
	}
}
