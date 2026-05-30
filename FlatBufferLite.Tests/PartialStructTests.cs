using FlatBufferLite.AllTypes;

namespace FlatBufferLite.Tests;

public class PartialStructTests
{
	[Fact]
	public void Vec2_PartialStruct_RoundTripFromBuffer_UserMethodWorks()
	{
		Span<byte> buf = stackalloc byte[RefsRef.GetMaxSize(strValByteCount: 0)];
		var b = new FlatBufferBuilder(buf);
		var refs = RefsRef.Create(ref b, vec2Val: new Vec2 { X = 3.0f, Y = 4.0f });
		refs.MarkAsRoot(ref b);
		var span = b.Finish();

		var r = RefsRef.GetRootAs(span);
		var v = r.Vec2Val;

		Assert.Equal(3.0f, v.X);
		Assert.Equal(4.0f, v.Y);
		Assert.Equal(25.0f, v.LengthSquared());
		var doubled = v.Scale(2.0f);
		Assert.Equal(6.0f, doubled.X);
		Assert.Equal(8.0f, doubled.Y);
	}

}
