using FlatBufferLite.Sample;

namespace FlatBufferLite.Tests;

public class GeneratedSchemaTests
{
	[Fact]
	public void PlayerTable_DefaultsAreReturned()
	{
		Span<byte> buf = stackalloc byte[256];
		var b = new FlatBufferBuilder(buf);
		Player.Create(ref b);

		var span = b.Finish();
		var player = Player.GetRootAs(span);
		Assert.Equal(0, player.Id);
		Assert.Equal((short)100, player.Hp);
		Assert.False(player.Name.IsValid);
		Assert.Equal(Status.Active, player.Status);
	}

	[Fact]
	public void PlayerTable_RoundTripsValues()
	{
		ReadOnlySpan<int> inv = stackalloc int[] { 10, 20, 30 };
		Span<byte> buf = stackalloc byte[512];
		var b = new FlatBufferBuilder(buf);

		int name = b.CreateString("Alice"u8);
		int invOff = b.CreateVector<int>(inv);

		var pb = Player.Create(ref b, id: 42, name: name, hp: 250, status: Status.Pending, inventory: invOff);

		var span = b.Finish();
		var player = Player.GetRootAs(span);
		Assert.Equal(42, player.Id);
		Assert.Equal("Alice", player.Name.ToString());
		Assert.Equal((short)250, player.Hp);
		Assert.Equal(Status.Pending, player.Status);
		var read = player.Inventory.AsSpan;
		Assert.Equal(3, read.Length);
		Assert.Equal(10, read[0]);
		Assert.Equal(20, read[1]);
		Assert.Equal(30, read[2]);
	}

	[Fact]
	public void PlayerTable_StructField_RoundTrips()
	{
		Span<byte> buf = stackalloc byte[256];
		var b = new FlatBufferBuilder(buf);
		var pb = Player.Create(ref b);
		pb.Position = new Vec3 { X = 1.5f, Y = -2.5f, Z = 3.5f };

		var span = b.Finish();
		var player = Player.GetRootAs(span);
		Assert.Equal(1.5f, player.Position.X);
		Assert.Equal(-2.5f, player.Position.Y);
		Assert.Equal(3.5f, player.Position.Z);
	}

	[Fact]
	public void Vec3Struct_HasExpectedSize()
	{
		Assert.Equal(12, System.Runtime.InteropServices.Marshal.SizeOf<Vec3>());
	}
}