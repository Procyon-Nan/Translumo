using System;
using System.Collections.Generic;
using System.Linq;

namespace Translumo.Logging
{
    public sealed class RuntimeLogStore
    {
        public static RuntimeLogStore Instance { get; } = new RuntimeLogStore(1000);

        public event EventHandler<RuntimeLogEntry> EntryAdded;

        public event EventHandler Cleared;

        private readonly object _syncRoot = new object();
        private readonly Queue<RuntimeLogEntry> _entries = new Queue<RuntimeLogEntry>();
        private readonly int _capacity;
        private long _sequence;

        public RuntimeLogStore(int capacity)
        {
            _capacity = Math.Max(1, capacity);
        }

        public IReadOnlyList<RuntimeLogEntry> Snapshot()
        {
            lock (_syncRoot)
            {
                return _entries.ToArray();
            }
        }

        public void Add(RuntimeLogEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            lock (_syncRoot)
            {
                entry.Sequence = ++_sequence;
                _entries.Enqueue(entry);
                while (_entries.Count > _capacity)
                {
                    _entries.Dequeue();
                }
            }

            EntryAdded?.Invoke(this, entry);
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _entries.Clear();
            }

            Cleared?.Invoke(this, EventArgs.Empty);
        }
    }
}
