using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using FlatBufferLite;
using FlatBufferLite.Bench;

BenchmarkRunner.Run<WriteBenchmarks>();

[MemoryDiagnoser]
public class WriteBenchmarks
{
	static readonly int _bufSize =
		Monster.GetMaxSize(nameByteCount: 6, inventoryCount: 5, weaponsCount: 2, pathCount: 2)
		+ Weapon.GetMaxSize(nameByteCount: 5)
		+ Weapon.GetMaxSize(nameByteCount: 3);

	readonly byte[] _buf = new byte[WriteBenchmarks._bufSize];
	readonly byte[] _inventory = new byte[] { 0, 1, 2, 3, 4 };
	readonly Vec3[] _path = new Vec3[] { new Vec3 { X = 1, Y = 0, Z = 0 }, new Vec3 { X = 2, Y = 0, Z = 0 } };

	[Benchmark]
	public int WriteMonster()
	{
		var b = new FlatBufferBuilder(_buf);

		int swordName = b.CreateString("Sword"u8);
		int bowName   = b.CreateString("Bow"u8);
		int sword = new Weapon(ref b, name: swordName, damage: 30, equipType: EquipType.Sword).BufferPos;
		int bow   = new Weapon(ref b, name: bowName, damage: 15, equipType: EquipType.Bow).BufferPos;

		int weapons  = b.CreateVectorOfOffsets(new[] { sword, bow });
		int inventory = b.CreateVector<byte>((ReadOnlySpan<byte>)_inventory);
		int pathVec  = b.CreateVector<Vec3>((ReadOnlySpan<Vec3>)_path);
		int name     = b.CreateString("Goblin"u8);

		new Monster(ref b,
			pos: new Vec3 { X = 1.0f, Y = 2.0f, Z = 3.0f },
			hp: 100, mana: 150, name: name,
			color: new Color { R = 255, G = 0, B = 0, A = 255 },
			inventory: inventory, weapons: weapons, path: pathVec);

		return b.AsSpan().Length;
	}
}
