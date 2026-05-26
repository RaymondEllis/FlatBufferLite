using FlatBufferLite.AllTypes;

namespace FlatBufferLite.Tests;

public class PartialStructTests
{
    [Fact]
    public void Vec2_PartialStruct_Add_ReturnsSum()
    {
        var a = new Vec2 { X = 1.0f, Y = 2.0f };
        var b = new Vec2 { X = 3.0f, Y = 4.0f };
        var result = a.Add(b);
        Assert.Equal(4.0f, result.X);
        Assert.Equal(6.0f, result.Y);
    }

    [Fact]
    public void Vec2_PartialStruct_Scale_MultipliesComponents()
    {
        var v = new Vec2 { X = 2.0f, Y = -3.0f };
        var result = v.Scale(2.0f);
        Assert.Equal(4.0f, result.X);
        Assert.Equal(-6.0f, result.Y);
    }

    [Fact]
    public void Vec2_PartialStruct_LengthSquared_IsCorrect()
    {
        var v = new Vec2 { X = 3.0f, Y = 4.0f };
        Assert.Equal(25.0f, v.LengthSquared());
    }

    [Fact]
    public void Vec2_PartialStruct_Dot_IsCorrect()
    {
        var a = new Vec2 { X = 1.0f, Y = 0.0f };
        var b = new Vec2 { X = 0.0f, Y = 1.0f };
        Assert.Equal(0.0f, a.Dot(b));

        var c = new Vec2 { X = 2.0f, Y = 3.0f };
        var d = new Vec2 { X = 4.0f, Y = 5.0f };
        Assert.Equal(23.0f, c.Dot(d));
    }

    [Fact]
    public void Vec2_PartialStruct_RoundTripFromBuffer_UserMethodWorks()
    {
        Span<byte> buf = stackalloc byte[Refs.TableMaxSize];
        var b = new FlatBufferBuilder(buf);
        var refs = Refs.Create(ref b, vec2Val: new Vec2 { X = 3.0f, Y = 4.0f });
        refs.MarkAsRoot(ref b);
        var span = b.Finish();

        var r = Refs.GetRootAs(span);
        var v = r.Vec2Val;

        Assert.Equal(25.0f, v.LengthSquared());
        var doubled = v.Scale(2.0f);
        Assert.Equal(6.0f, doubled.X);
        Assert.Equal(8.0f, doubled.Y);
    }

    [Fact]
    public void Vec2_PartialStruct_ChainedMethods_Work()
    {
        var origin = new Vec2 { X = 1.0f, Y = 1.0f };
        var offset = new Vec2 { X = 2.0f, Y = 3.0f };
        var result = origin.Add(offset).Scale(0.5f);
        Assert.Equal(1.5f, result.X);
        Assert.Equal(2.0f, result.Y);
    }
}
