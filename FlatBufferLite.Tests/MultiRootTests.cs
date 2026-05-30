using FlatBufferLite.MultiRoot;

namespace FlatBufferLite.Tests;

public class MultiRootTests
{
	[Fact]
	public void MultiRoot_RegionData_AutoMarksRoot()
	{
		Span<byte> buf = stackalloc byte[RegionDataRef.GetMaxSize(nameByteCount: 7)];
		var b = new FlatBufferBuilder(buf);
		var name = b.CreateString("region1"u8);
		var region = RegionDataRef.Create(ref b, id: 1, name: name);

		var span = b.Finish();
		var read = RegionDataRef.GetRootAs(span);
		Assert.Equal(1, read.Id);
		Assert.Equal("region1", read.Name.ToString());
	}

	[Fact]
	public void MultiRoot_WorldIndexData_AutoMarksRoot()
	{
		Span<byte> buf = stackalloc byte[WorldIndexDataRef.GetMaxSize()];
		var b = new FlatBufferBuilder(buf);
		var world = WorldIndexDataRef.Create(ref b, version: 42);

		var span = b.Finish();
		var read = WorldIndexDataRef.GetRootAs(span);
		Assert.Equal(42, read.Version);
	}

	[Fact]
	public void MarkAsRoot_ManuallyMarksNonRootTable()
	{
		Span<byte> buf = stackalloc byte[MetaDataRef.GetMaxSize()];
		var b = new FlatBufferBuilder(buf);
		var meta = MetaDataRef.Create(ref b);
		meta.Tag = 99;
		meta.MarkAsRoot(ref b);

		var span = b.Finish();
		var read = MetaDataRef.GetRootAs(span);
		Assert.Equal(99, read.Tag);
	}

}
