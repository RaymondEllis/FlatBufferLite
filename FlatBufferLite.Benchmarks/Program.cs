using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using FlatBufferLite;
using FlatBufferLite.Bench;

BenchmarkRunner.Run<WriteBenchmarks>();

[MemoryDiagnoser]
public class WriteBenchmarks
{
	[Benchmark]
	public int SizeMonster() =>
		Monster.GetMaxSize(nameByteCount: 6, inventoryCount: 5, weaponsCount: 2, pathCount: 2)
		+ Weapon.GetMaxSize(nameByteCount: 5)
		+ Weapon.GetMaxSize(nameByteCount: 3);
}
