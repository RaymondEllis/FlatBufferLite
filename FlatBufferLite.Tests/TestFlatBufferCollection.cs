using System.Runtime.InteropServices;

namespace FlatBufferLite.Tests;

sealed class TestFlatBufferCollection<T> : List<T>, IFlatBufferCollection<T> where T : unmanaged
{
	public TestFlatBufferCollection() { }
	public TestFlatBufferCollection(int capacity) : base(capacity) { }

	public ReadOnlySpan<T> AsReadOnlySpan() => CollectionsMarshal.AsSpan(this);

	public void ReplaceRange(ref FlatVector<T> items)
	{
		var target = ResizeAndGetSpan(items.Length);
		items.AsSpan.CopyTo(target);
	}

	Span<T> ResizeAndGetSpan(int count)
	{
		if (Count > count)
		{
			RemoveRange(count, Count - count);
			return CollectionsMarshal.AsSpan(this);
		}
		if (Count < count)
		{
			EnsureCapacity(count);
			CollectionsMarshal.SetCount(this, count);
		}
		return CollectionsMarshal.AsSpan(this);
	}
}

sealed class TestFlatBufferNativeVector<T> : List<T>, IFlatBufferNativeVector<T>
{
	public TestFlatBufferNativeVector() { }
	public TestFlatBufferNativeVector(int capacity) : base(capacity) { }

	public void Resize(int count)
	{
		if (Count > count)
		{
			RemoveRange(count, Count - count);
			return;
		}
		if (Count < count)
		{
			EnsureCapacity(count);
			CollectionsMarshal.SetCount(this, count);
		}
	}

	public Span<T> AsSpan() => CollectionsMarshal.AsSpan(this);
}