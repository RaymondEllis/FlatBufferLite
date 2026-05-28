using FlatBufferLite.PlainStructs;
using FlatBufferLite.Sample;
using System.Runtime.CompilerServices;

namespace FlatBufferLite.Tests;

public class ZeroAllocationTests
{
	static byte[] BuildScalarBuffer()
	{
		var buf = new byte[256];
		var b = new FlatBufferBuilder(buf);
		int tp = b.StartTable(4, 16, 4);
		Vtable.Write<int>(buf, tp, 4, 4, 1, 0);
		Vtable.Write<int>(buf, tp, 6, 8, 2, 0);
		Vtable.Write<int>(buf, tp, 8, 12, 3, 0);
		Vtable.Write<int>(buf, tp, 10, 16, 4, 0);
		b.MarkRoot(tp);
		return b.Finish().ToArray();
	}

	[Fact]
	public void TableScalarReads_DoNotAllocate()
	{
		var bytes = BuildScalarBuffer();

		WarmScalar(bytes);

		long before = GC.GetAllocatedBytesForCurrentThread();
		long sum = 0;
		for (int i = 0; i < 10_000; i++)
		{
			ReadOnlySpan<byte> span = bytes;
			int pos = FlatBufferReader.GetRootOffset(span);
			sum += Vtable.Read<int>(span, pos, 4, 0);
			sum += Vtable.Read<int>(span, pos, 6, 0);
			sum += Vtable.Read<int>(span, pos, 8, 0);
			sum += Vtable.Read<int>(span, pos, 10, 0);
		}
		long after = GC.GetAllocatedBytesForCurrentThread();
		Assert.Equal(0, after - before);
		Assert.Equal(100_000L, sum);

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void WarmScalar(byte[] bytes)
		{
			ReadOnlySpan<byte> span = bytes;
			int pos = FlatBufferReader.GetRootOffset(span);
			_ = Vtable.Read<int>(span, pos, 4, 0);
		}
	}

	[Fact]
	public void VectorAsSpan_DoesNotAllocate()
	{
		var data = new int[64];
		for (int i = 0; i < data.Length; i++)
			data[i] = i;

		var raw = new byte[512];
		var b = new FlatBufferBuilder(raw);
		int vec = b.CreateVector<int>(data);
		int tp = b.StartTable(1, 4, 4);
		Vtable.WriteOffset(raw, tp, 4, 4, vec);
		b.MarkRoot(tp);
		var bytes = b.Finish().ToArray();

		WarmVector(bytes);

		long before = GC.GetAllocatedBytesForCurrentThread();
		long sum = 0;
		for (int i = 0; i < 5_000; i++)
		{
			Span<byte> span = bytes;
			int pos = FlatBufferReader.GetRootOffset(span);
			var v = new FlatVector<int>(span, Vtable.ReadIndirect(span, pos, 4));
			var s = v.AsSpan;
			for (int j = 0; j < s.Length; j++)
				sum += s[j];
		}
		long after = GC.GetAllocatedBytesForCurrentThread();
		Assert.Equal(0, after - before);

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void WarmVector(byte[] bytes)
		{
			Span<byte> span = bytes;
			int pos = FlatBufferReader.GetRootOffset(span);
			var v = new FlatVector<int>(span, Vtable.ReadIndirect(span, pos, 4));
			_ = v.AsSpan;
		}
	}

	[Fact]
	public void StringAsBytes_DoesNotAllocate()
	{
		var raw = new byte[256];
		var b = new FlatBufferBuilder(raw);
		int s = b.CreateString("performance"u8);
		int tp = b.StartTable(1, 4, 4);
		Vtable.WriteOffset(raw, tp, 4, 4, s);
		b.MarkRoot(tp);
		var bytes = b.Finish().ToArray();

		WarmString(bytes);

		long before = GC.GetAllocatedBytesForCurrentThread();
		int total = 0;
		for (int i = 0; i < 5_000; i++)
		{
			ReadOnlySpan<byte> span = bytes;
			int pos = FlatBufferReader.GetRootOffset(span);
			var str = new FlatString(span, Vtable.ReadIndirect(span, pos, 4));
			total += str.AsBytes.Length;
		}
		long after = GC.GetAllocatedBytesForCurrentThread();
		Assert.Equal(0, after - before);
		Assert.Equal(5_000 * "performance"u8.Length, total);

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void WarmString(byte[] bytes)
		{
			ReadOnlySpan<byte> span = bytes;
			int pos = FlatBufferReader.GetRootOffset(span);
			var str = new FlatString(span, Vtable.ReadIndirect(span, pos, 4));
			_ = str.AsBytes.Length;
		}
	}

	[Fact]
	public void Builder_RoundTrip_DoesNotAllocate()
	{
		var buf = new byte[1024];
		ReadOnlySpan<byte> name = "warmup"u8;
		ReadOnlySpan<int> data = new int[] { 1, 2, 3, 4, 5 };

		Warm(buf, name, data);

		long before = GC.GetAllocatedBytesForCurrentThread();
		for (int i = 0; i < 1000; i++)
			Round(buf, name, data);
		long after = GC.GetAllocatedBytesForCurrentThread();
		Assert.Equal(0, after - before);

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void Warm(Span<byte> buf, ReadOnlySpan<byte> name, ReadOnlySpan<int> data)
			=> Round(buf, name, data);

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void Round(Span<byte> buf, ReadOnlySpan<byte> name, ReadOnlySpan<int> data)
		{
			var b = new FlatBufferBuilder(buf);
			int s = b.CreateString(name);
			int v = b.CreateVector<int>(data);
			int tp = b.StartTable(3, 12, 4);
			Vtable.Write<int>(buf, tp, 4, 4, 7, 0);
			Vtable.WriteOffset(buf, tp, 6, 8, s);
			Vtable.WriteOffset(buf, tp, 8, 12, v);
			b.MarkRoot(tp);
			var span = b.Finish();
			int pos = FlatBufferReader.GetRootOffset(span);
			_ = Vtable.Read<int>(span, pos, 4, 0);
			_ = new FlatString(span, Vtable.ReadIndirect(span, pos, 6));
			_ = new FlatVector<int>(span, Vtable.ReadIndirect(span, pos, 8)).AsSpan;
		}
	}

	[Fact]
	public void PlainStructSerialize_DoesNotAllocateScratchArrays()
	{
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
		var buf = new byte[4096];

		Warm(buf, in source);

		long before = GC.GetAllocatedBytesForCurrentThread();
		for (int i = 0; i < 1000; i++)
			Round(buf, in source);
		long after = GC.GetAllocatedBytesForCurrentThread();
		Assert.Equal(0, after - before);

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void Warm(Span<byte> buf, in Bag source) => Round(buf, in source);

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void Round(Span<byte> buf, in Bag source)
		{
			var b = new FlatBufferBuilder(buf);
			Bag.Serialize(ref b, in source);
			var span = b.Finish();
			var read = BagRef.GetRootAs(span);
			_ = read.Scores.Length + read.Names.Length + read.Items.Length + read.Qualities.Length;
		}
	}

	[Fact]
	public void GeneratedBuilder_DoesNotAllocate()
	{
		var buf = new byte[PlayerRef.GetMaxSize(nameByteCount: 5)];
		Warm(buf);

		long before = GC.GetAllocatedBytesForCurrentThread();
		for (int i = 0; i < 1000; i++)
			Round(buf);
		long after = GC.GetAllocatedBytesForCurrentThread();
		Assert.Equal(0, after - before);

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void Warm(Span<byte> buf) => Round(buf);

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void Round(Span<byte> buf)
		{
			var b = new FlatBufferBuilder(buf);
			var name = b.CreateString("Alice"u8);
			PlayerRef.Create(ref b, id: 42, name: name, hp: 250, status: Status.Pending, position: new Vec3 { X = 1.0f, Y = 2.0f, Z = 3.0f });

			var span = b.Finish();
			var read = PlayerRef.GetRootAs(span);
			_ = read.Id + read.Hp;
		}
	}
}