namespace System.Runtime.CompilerServices;

public interface IUnion
{
	object? Value { get; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class UnionAttribute : Attribute { }
