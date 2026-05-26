using FlatBufferLite.MultiRoot;

namespace FlatBufferLite.Tests;

public class MultiRootTests
{
	[Fact]
	public void MultiRoot_RegionData_AutoMarksRoot()
	{
		Span<byte> buf = stackalloc byte[RegionData.GetMaxSize(nameByteCount: 7)];
		var b = new FlatBufferBuilder(buf);
		var name = b.CreateString("region1"u8);
		var region = RegionData.Create(ref b, id: 1, name: name);

		var span = b.Finish();
		var read = RegionData.GetRootAs(span);
		Assert.Equal(1, read.Id);
		Assert.Equal("region1", read.Name.ToString());
	}

	[Fact]
	public void MultiRoot_WorldIndexData_AutoMarksRoot()
	{
		Span<byte> buf = stackalloc byte[WorldIndexData.TableMaxSize];
		var b = new FlatBufferBuilder(buf);
		var world = WorldIndexData.Create(ref b, version: 42);

		var span = b.Finish();
		var read = WorldIndexData.GetRootAs(span);
		Assert.Equal(42, read.Version);
	}

	[Fact]
	public void MarkAsRoot_ManuallyMarksNonRootTable()
	{
		Span<byte> buf = stackalloc byte[MetaData.TableMaxSize];
		var b = new FlatBufferBuilder(buf);
		var meta = MetaData.Create(ref b);
		meta.Tag = 99;
		meta.MarkAsRoot(ref b);

		var span = b.Finish();
		var read = MetaData.GetRootAs(span);
		Assert.Equal(99, read.Tag);
	}

	[Fact]
	public void MarkAsRoot_ExplicitCallOnNonRootTable()
	{
		var source = """
			table A { x: int; }
			table B { y: int; }
			root_type A;
			""";
		var parser = new SourceGen.Parsing.SchemaParser(source);
		var schema = parser.Parse();
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();

		Assert.Contains("public void MarkAsRoot(ref FlatBufferBuilder builder)", code);
		var bSection = code.Substring(code.IndexOf("public readonly ref struct B"));
		Assert.Contains("public void MarkAsRoot(ref FlatBufferBuilder builder)", bSection);
	}
}