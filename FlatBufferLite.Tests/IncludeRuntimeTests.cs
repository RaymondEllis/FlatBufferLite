using FlatBufferLite.Includes;

namespace FlatBufferLite.Tests;

public class IncludeRuntimeTests
{
	[Fact]
	public void IncludedStruct_CanBeUsedInTable()
	{
		Span<byte> buf = stackalloc byte[256];
		var b = new FlatBufferBuilder(buf);
		var chunk = Chunk.Create(ref b, pos: new Vector3I { X = 10, Y = 20, Z = 30 });

		var span = b.Finish();
		var read = Chunk.GetRootAs(span);
		Assert.Equal(10, read.Pos.X);
		Assert.Equal(20, read.Pos.Y);
		Assert.Equal(30, read.Pos.Z);
	}

	[Fact]
	public void IncludedStruct_HasCorrectLayout()
	{
		Assert.Equal(12, System.Runtime.InteropServices.Marshal.SizeOf<Vector3I>());
	}

	[Fact]
	public void IncludedStruct_ZeroAlloc_RoundTrip()
	{
		var buf = new byte[256];
		Warm(buf);

		long before = GC.GetAllocatedBytesForCurrentThread();
		for (int i = 0; i < 1000; i++)
			Round(buf);
		long after = GC.GetAllocatedBytesForCurrentThread();
		Assert.Equal(0, after - before);

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
		static void Warm(Span<byte> buf) => Round(buf);

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
		static void Round(Span<byte> buf)
		{
			var b = new FlatBufferBuilder(buf);
			Chunk.Create(ref b, pos: new Vector3I { X = 1, Y = 2, Z = 3 });
			var span = b.Finish();
			var read = Chunk.GetRootAs(span);
			_ = read.Pos.X + read.Pos.Y + read.Pos.Z;
		}
	}
}
