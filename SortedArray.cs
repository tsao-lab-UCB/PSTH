using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bonsai.Reactive;

namespace PSTH
{
    [Serializable]
    public class SortedArray<T> : ICollection<T>, IReadOnlyList<T>
    {
        private readonly List<T> _list;

        public int Count => _list.Count;

        public bool IsReadOnly => false;

        public T this[int index] => _list[index];

        public SortedArray()
        {
            _list = new List<T>();
        }

        public SortedArray(int capacity)
        {
            _list = new List<T>(capacity);
        }

        public SortedArray(IEnumerable<T> collection)
        {
            _list = new List<T>(collection.OrderBy(v => v));
        }

        public void Add(T item)
        {
            TryAdd(item, out _);
        }

        public bool TryAdd(T item, out int index)
        {
            var comparer = Comparer<T>.Default;
            for (index = 0; index < _list.Count; index++)
            {
                var diff = comparer.Compare(item, _list[index]);
                if (diff == 0) return false;
                if (diff > 0) continue;
                _list.Insert(index, item);
                return true;
            }
            _list.Add(item);
            return true;
        }

        public int IndexOf(T item)
        {
            return _list.IndexOf(item);
        }

        public void Clear()
        {
            _list.Clear();
        }

        public bool Contains(T item)
        {
            return _list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            return _list.Remove(item);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        public T[] ToArray()
        {
            return _list.ToArray();
        }

        public SortedArray<object> Convert()
        {
            return new SortedArray<object>(_list.Select(t => (object)t));
        }

        public SortedArray<T> Clone()
        {
            return new SortedArray<T>(_list);
        }
    }
}
