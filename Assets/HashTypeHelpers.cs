using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;

namespace Core.Algorithm
{
    //给哈希类型添加上按序遍历的功能
    [Serializable]
    [MessagePackObject]
    public class DictionaryWrapper<TKey, TValue> : IDictionary<TKey, TValue>
    {
        [Key(0)]
        protected IDictionary<TKey, TValue> _dictionary;
        public DictionaryWrapper()
        {
            _dictionary = new Dictionary<TKey, TValue>();
        }

        public DictionaryWrapper(int capacity)
        {
            _dictionary = new Dictionary<TKey, TValue>(capacity);
        }
        public DictionaryWrapper(IDictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary;
        }

        public virtual TValue this[TKey key]
        {
            get => _dictionary[key];
            set => _dictionary[key] = value;
        }

        public ICollection<TKey> Keys => _dictionary.Keys;

        public ICollection<TValue> Values => _dictionary.Values;

        public int Count => _dictionary.Count;

        public bool IsReadOnly => _dictionary.IsReadOnly;

        public virtual void Add(TKey key, TValue value)
        {
            _dictionary.Add(key, value);
        }

        public virtual void Add(KeyValuePair<TKey, TValue> item)
        {
            _dictionary.Add(item);
        }

        public virtual void Clear()
        {
            _dictionary.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return _dictionary.Contains(item);
        }

        public bool ContainsKey(TKey key)
        {
            return _dictionary.ContainsKey(key);
        }

        public virtual void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            _dictionary.CopyTo(array, arrayIndex);
        }

        public virtual IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return _dictionary.GetEnumerator();
        }

        public virtual bool Remove(TKey key)
        {
            return _dictionary.Remove(key);
        }

        public virtual bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return _dictionary.Remove(item);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _dictionary.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_dictionary).GetEnumerator();
        }
    }
    [Serializable]
    [MessagePackObject(AllowPrivate = true)]
    public class LinkedDictionary<TKey, TValue> : DictionaryWrapper<TKey,TValue>, IDictionary<TKey, TValue>
    {
        [Key(0)]
        private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _savedNode = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>();
        [Key(1)]
        private readonly LinkedList<KeyValuePair<TKey, TValue>> _insertedOrderManager = new LinkedList<KeyValuePair<TKey, TValue>>();
        public LinkedDictionary()
        {
            _dictionary = new Dictionary<TKey, TValue>();
        }

        /*public DictionaryWrapper(int capacity)
    {
        _dictionary = new Dictionary<TKey, TValue>(capacity);
    }*/
        public LinkedDictionary(IDictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary;
            loadNodes();
        }
        private void loadNodes()
        {
            foreach (var i in _dictionary)
            {
                _insertedOrderManager.AddLast(i);
            }
        }
        private void appendNode(TKey key,TValue value) {
            var add = new LinkedListNode<KeyValuePair<TKey, TValue>>(new KeyValuePair<TKey,TValue>(key, value));
            if(_savedNode.ContainsKey(key))
            {
                _insertedOrderManager.AddAfter(_savedNode[key],add);
                _insertedOrderManager.Remove(_savedNode[key]);
                _savedNode[key] = add;
            }
            else
            {
                _insertedOrderManager.AddLast(add);
                _savedNode[key] = add;
            }
        }
        private void removeNode(TKey key) {
            if (_savedNode.ContainsKey(key))
            {
                _insertedOrderManager.Remove(_savedNode[key]);
                _savedNode.Remove(key);
            }
        }
        public override TValue this[TKey key]
        {
            get => _dictionary[key];
            set { 
                _dictionary[key] = value;
                appendNode(key, value);
            }
        }

        public override void Add(TKey key, TValue value)
        {
            _dictionary.Add(key, value);
            appendNode(key, value);
        }

        public override void Add(KeyValuePair<TKey, TValue> item)
        {
            _dictionary.Add(item);
            appendNode(item.Key, item.Value);
        }

        public override void Clear()
        {
            _dictionary.Clear();
            _insertedOrderManager.Clear();
            _savedNode.Clear();
        }


        public override void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            _insertedOrderManager.CopyTo(array, arrayIndex);
        }

        public override IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return _insertedOrderManager.GetEnumerator();
        }

        public override bool Remove(TKey key)
        {
            var ret = _dictionary.Remove(key);
            removeNode(key);
            return ret;
        }

        public override bool Remove(KeyValuePair<TKey, TValue> item)
        {
            var ret = _dictionary.Remove(item);
            Remove(item.Key);
            return ret;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_insertedOrderManager).GetEnumerator();
        }
    }
    [MessagePackObject]
    public class HashSetWrapper<T> : ISet<T>
    {
        [Key(0)]
        protected readonly HashSet<T> _hashSet;
        public HashSetWrapper()
        {
            _hashSet = new HashSet<T>();
        }
        public HashSetWrapper(HashSet<T> hashSet)
        {
            _hashSet = hashSet;
        }

        public int Count => _hashSet.Count;

        public bool IsReadOnly => ((ICollection<T>)_hashSet).IsReadOnly;

        public virtual bool Add(T item)
        {
            return _hashSet.Add(item);
        }

        void ICollection<T>.Add(T item)
        {
            _hashSet.Add(item);
        }

        public virtual void Clear()
        {
            _hashSet.Clear();
        }

        public bool Contains(T item)
        {
            return _hashSet.Contains(item);
        }

        public virtual void CopyTo(T[] array, int arrayIndex)
        {
            _hashSet.CopyTo(array, arrayIndex);
        }

        public virtual void ExceptWith(IEnumerable<T> other)
        {
            _hashSet.ExceptWith(other);
        }

        public virtual IEnumerator<T> GetEnumerator()
        {
            return _hashSet.GetEnumerator();
        }

        public virtual void IntersectWith(IEnumerable<T> other)
        {
            _hashSet.IntersectWith(other);
        }

        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            return _hashSet.IsProperSubsetOf(other);
        }

        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            return _hashSet.IsProperSupersetOf(other);
        }

        public bool IsSubsetOf(IEnumerable<T> other)
        {
            return _hashSet.IsSubsetOf(other);
        }

        public bool IsSupersetOf(IEnumerable<T> other)
        {
            return _hashSet.IsSupersetOf(other);
        }

        public bool Overlaps(IEnumerable<T> other)
        {
            return _hashSet.Overlaps(other);
        }

        public virtual bool Remove(T item)
        {
            return _hashSet.Remove(item);
        }

        public bool SetEquals(IEnumerable<T> other)
        {
            return _hashSet.SetEquals(other);
        }

        public virtual void SymmetricExceptWith(IEnumerable<T> other)
        {
            _hashSet.SymmetricExceptWith(other);
        }

        public virtual void UnionWith(IEnumerable<T> other)
        {
            _hashSet.UnionWith(other);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_hashSet).GetEnumerator();
        }
    }
    [Serializable]
    [MessagePackObject(AllowPrivate = true)]
    public class LinkedHashSet<T> : HashSetWrapper<T>, ISet<T>
    {
        [Key(0)]
        Dictionary<T,LinkedListNode<T>> _savedNodes = new Dictionary<T,LinkedListNode<T>>();
        [Key(1)]
        LinkedList<T> _insertedOrderSaver = new LinkedList<T>();
        public override bool Add(T item)
        {
            if (_hashSet.Add(item))
            {
                _insertedOrderSaver.AddLast(item);
                _savedNodes.Add(item,_insertedOrderSaver.Last);
                return true;
            }
            return false;
        }
        void ICollection<T>.Add(T item)
        {
            Add(item);
        }
        public override void Clear()
        {
            _hashSet.Clear();
            _savedNodes.Clear();
            _insertedOrderSaver.Clear();
        }
        public override void CopyTo(T[] array, int arrayIndex)
        {
            _insertedOrderSaver.CopyTo(array, arrayIndex);
        }
        public override void UnionWith(IEnumerable<T> other)
        {
            if (other == null)
            {
                throw new ArgumentNullException("other");
            }

            foreach (T item in other)
            {
                Add(item);
            }
        }
        public override void ExceptWith(IEnumerable<T> other)
        {
            if (other == null)
            {
                throw new ArgumentNullException("other");
            }

            if (_hashSet.Count == 0)
            {
                return;
            }

            if (other == this)
            {
                Clear();
                return;
            }

            foreach (T item in other)
            {
                Remove(item);
            }
        }
        public override IEnumerator<T> GetEnumerator()
        {
            return _insertedOrderSaver.GetEnumerator();
        }

        public override void IntersectWith(IEnumerable<T> other)
        {
            _hashSet.IntersectWith(other);
            //求交集求完之后只会删掉元素……
            var node = _insertedOrderSaver.First;
            LinkedListNode<T> tmp = node;
            while (node != null) {
                tmp = node.Next;
                if (node.Value==null||!_hashSet.Contains(node.Value))
                {
                    _insertedOrderSaver.Remove(node);
                    _savedNodes.Remove(node.Value);
                }
                node= tmp;
            }
        }
        public override bool Remove(T item)
        {
            if (_savedNodes.ContainsKey(item))
            {
                _insertedOrderSaver.Remove(_savedNodes[item]);
                _savedNodes.Remove(item);
            }
            return _hashSet.Remove(item);
        }
        private static bool AreEqualityComparersEqual(HashSet<T> set1, HashSet<T> set2)
        {
            return set1.Comparer.Equals(set2.Comparer);
        }
        public override void SymmetricExceptWith(IEnumerable<T> other)
        {
            if (other == null)
            {
                throw new ArgumentNullException("other");
            }

            if (_hashSet.Count == 0)
            {
                UnionWith(other);
            }
            else if (other == this)
            {
                Clear();
            }
            else if (other is HashSet<T> hashSet && AreEqualityComparersEqual(_hashSet, hashSet))
            {
                foreach (T item in other)
                {
                    if (!Remove(item))
                    {
                        Add(item);
                    }
                }
            }
            else
            {
                foreach(T item in other)
                {
                    if (!Add(item))
                    {
                        Remove(item);
                    }
                }
            }
        }

    }
    [MessagePackObject]
    public class LinkedPooledHashSet<T> : LinkedHashSet<T>, IPoolable
    {
        public void Reset() { Clear(); }
    }
}