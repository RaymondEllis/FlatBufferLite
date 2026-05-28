namespace FlatBufferLite;

public interface IFlatBufferCollection<T> : IList<T> where T : unmanaged
{
	ReadOnlySpan<T> AsReadOnlySpan();
	void ReplaceRange(ref FlatVector<T> items);
}

public interface IFlatBufferNativeVector<T> : IList<T>
{
	void Resize(int count);
	Span<T> AsSpan();
}

public static class FlatBufferCollections<T> where T : unmanaged
{
	public static Func<int, IFlatBufferCollection<T>>? Create;
	public static bool CreateChecked;
}

public static class FlatBufferNativeVectors<T>
{
	public static Func<int, IFlatBufferNativeVector<T>>? Create;
	public static bool CreateChecked;
}

public static class FlatBufferCollections
{
	public static IFlatBufferCollection<T> Create<T>(int items) where T : unmanaged => FlatBufferCollections<T>.Create!(items);

	public static void EnsureCreate<T>() where T : unmanaged
	{
		if (FlatBufferCollections<T>.CreateChecked)
			return;
		if (FlatBufferCollections<T>.Create == null)
			ThrowMissingCreate<T>();
		FlatBufferCollections<T>.CreateChecked = true;
	}

	static void ThrowMissingCreate<T>() where T : unmanaged => throw new InvalidOperationException($"FlatBufferCollections<{typeof(T).Name}>.Create must be set before deserializing native custom collections.");
}

public static class FlatBufferNativeVectors
{
	public static IFlatBufferNativeVector<T> Create<T>(int items) => FlatBufferNativeVectors<T>.Create!(items);

	public static void EnsureCreate<T>()
	{
		if (FlatBufferNativeVectors<T>.CreateChecked)
			return;
		if (FlatBufferNativeVectors<T>.Create == null)
			ThrowMissingCreate<T>();
		FlatBufferNativeVectors<T>.CreateChecked = true;
	}

	static void ThrowMissingCreate<T>() => throw new InvalidOperationException($"FlatBufferNativeVectors<{typeof(T).Name}>.Create must be set before deserializing native custom collections.");
}