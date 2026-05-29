namespace FlatBufferLite.SourceGen
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// A readonly wrapper around an array that provides value equality semantics for use in Roslyn incremental source generators.
    /// Compares elements using <see cref="IEquatable{T}"/> to ensure proper value equality.
    /// </summary>
    /// <typeparam name="T">The element type, which must implement <see cref="IEquatable{T}"/>.</typeparam>
    public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
        where T : IEquatable<T>
    {
        private readonly T[] _array;

        public EquatableArray(T[] array)
        {
            _array = array ?? Array.Empty<T>();
        }

        /// <summary>Gets the number of elements in the array.</summary>
        public int Length => _array.Length;

        /// <summary>Gets the element at the specified index.</summary>
        public T this[int index] => _array[index];

        /// <summary>Gets the underlying array.</summary>
        public T[] AsArray() => _array;

        /// <summary>Compares this array with another for value equality by comparing each element.</summary>
        public bool Equals(EquatableArray<T> other)
        {
            if (_array.Length != other._array.Length)
            {
                return false;
            }

            for (int i = 0; i < _array.Length; i++)
            {
                if (!_array[i].Equals(other._array[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Determines whether the specified object is equal to this array.</summary>
        public override bool Equals(object? obj)
        {
            return obj is EquatableArray<T> other && Equals(other);
        }

        /// <summary>Returns the hash code for this array by combining element hash codes.</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 17;
                for (int i = 0; i < _array.Length; i++)
                {
                    hashCode = hashCode * 31 + _array[i].GetHashCode();
                }
                return hashCode;
            }
        }

        /// <summary>Determines whether two <see cref="EquatableArray{T}"/> instances are equal.</summary>
        public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right)
        {
            return left.Equals(right);
        }

        /// <summary>Determines whether two <see cref="EquatableArray{T}"/> instances are not equal.</summary>
        public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right)
        {
            return !left.Equals(right);
        }

        /// <summary>Implicitly converts an array to an <see cref="EquatableArray{T}"/>.</summary>
        public static implicit operator EquatableArray<T>(T[] array)
        {
            return new EquatableArray<T>(array);
        }

        /// <summary>Returns an enumerator that iterates through the array.</summary>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(_array);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new EnumeratorImpl(_array);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new EnumeratorImpl(_array);
        }

        /// <summary>Struct enumerator for iterating through array elements.</summary>
        public struct Enumerator
        {
            private readonly T[] _array;
            private int _index;

            internal Enumerator(T[] array)
            {
                _array = array;
                _index = -1;
            }

            public T Current => _array[_index];

            public bool MoveNext()
            {
                _index++;
                return _index < _array.Length;
            }
        }

        private sealed class EnumeratorImpl : IEnumerator<T>
        {
            private readonly T[] _array;
            private int _index;

            public EnumeratorImpl(T[] array)
            {
                _array = array;
                _index = -1;
            }

            public T Current => _array[_index];

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                _index++;
                return _index < _array.Length;
            }

            public void Reset()
            {
                _index = -1;
            }

            public void Dispose()
            {
            }
        }
    }
}
