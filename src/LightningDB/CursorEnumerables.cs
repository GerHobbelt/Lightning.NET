using System;
using System.Collections;
using System.Collections.Generic;

namespace LightningDB;

/// <summary>
/// An allocation-free enumerable over the key/value pairs of a <see cref="LightningCursor"/>,
/// starting at the cursor's current position. foreach binds to the struct enumerator with no
/// allocations; the <see cref="IEnumerable{T}"/> implementation is available for LINQ at the
/// cost of boxing the enumerator.
/// </summary>
public readonly struct CursorEnumerable : IEnumerable<(MDBValue key, MDBValue value)>
{
    private readonly LightningCursor _cursor;

    internal CursorEnumerable(LightningCursor cursor)
    {
        _cursor = cursor;
    }

    public Enumerator GetEnumerator() => new(_cursor);

    IEnumerator<(MDBValue key, MDBValue value)> IEnumerable<(MDBValue key, MDBValue value)>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<(MDBValue key, MDBValue value)>
    {
        private readonly LightningCursor _cursor;
        private (MDBValue key, MDBValue value) _current;

        internal Enumerator(LightningCursor cursor)
        {
            _cursor = cursor;
            _current = default;
        }

        public bool MoveNext()
        {
            var (resultCode, key, value) = _cursor.Next();
            if (resultCode == MDBResultCode.Success)
            {
                _current = (key, value);
                return true;
            }
            resultCode.ThrowOnReadError();
            return false;
        }

        public (MDBValue key, MDBValue value) Current => _current;

        object IEnumerator.Current => Current;

        public void Reset() => throw new NotSupportedException();

        //the cursor's lifetime belongs to the caller
        public void Dispose()
        {
        }
    }
}

/// <summary>
/// An allocation-free enumerable over the duplicate values for the key a <see cref="LightningCursor"/>
/// is positioned on. Requires MDB_DUPSORT. foreach binds to the struct enumerator with no allocations;
/// the <see cref="IEnumerable{T}"/> implementation is available for LINQ at the cost of boxing the enumerator.
/// </summary>
public readonly struct CursorDuplicateValuesEnumerable : IEnumerable<MDBValue>
{
    private readonly LightningCursor _cursor;

    internal CursorDuplicateValuesEnumerable(LightningCursor cursor)
    {
        _cursor = cursor;
    }

    public Enumerator GetEnumerator() => new(_cursor);

    IEnumerator<MDBValue> IEnumerable<MDBValue>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<MDBValue>
    {
        private readonly LightningCursor _cursor;
        private MDBValue _current;
        private bool _started;

        internal Enumerator(LightningCursor cursor)
        {
            _cursor = cursor;
            _current = default;
            _started = false;
        }

        public bool MoveNext()
        {
            if (!_started)
            {
                _started = true;
                _current = _cursor.GetCurrent().value;
                return true;
            }
            var (resultCode, _, value) = _cursor.NextDuplicate();
            if (resultCode == MDBResultCode.Success)
            {
                _current = value;
                return true;
            }
            resultCode.ThrowOnReadError();
            return false;
        }

        public MDBValue Current => _current;

        object IEnumerator.Current => Current;

        public void Reset() => throw new NotSupportedException();

        //the cursor's lifetime belongs to the caller
        public void Dispose()
        {
        }
    }
}
