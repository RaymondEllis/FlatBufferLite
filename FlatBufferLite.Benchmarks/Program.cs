using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using FlatBufferLite;
using FlatBufferLite.Bench;

BenchmarkRunner.Run<SizeBenchmarks>();

[MemoryDiagnoser]
public class SizeBenchmarks
{
	readonly byte[] _buf;
	readonly int _pos;

	public SizeBenchmarks()
	{
		_buf = new byte[Monster.GetMaxSize()];
		var b = new FlatBufferBuilder(_buf);
		int name = b.CreateString("Goblin"u8);
		var m = new Monster(ref b, hp: 100, mana: 150, name: name, x: 1.0f, y: 2.0f, z: 3.0f);
		_ = b.AsSpan();
		_pos = m.BufferPos;
	}

	[Benchmark]
	public int GetMaxSize() => Monster.GetMaxSize();

	[Benchmark]
	public int GetSize()
	{
		var m = new Monster(_buf, _pos);
		return m.GetSize();
	}
}
