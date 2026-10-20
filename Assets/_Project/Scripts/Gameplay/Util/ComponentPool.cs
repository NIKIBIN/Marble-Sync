using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace MarbleSync.Gameplay
{
    /// <summary>Prefab-backed pool. Level views are rebuilt on every level change, so pieces are recycled.</summary>
    public sealed class ComponentPool<T> where T : Component
    {
        private readonly ObjectPool<T> _pool;
        private readonly List<T> _active = new List<T>();

        public ComponentPool(T prefab, Transform parent, int defaultCapacity = 16)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            _pool = new ObjectPool<T>(
                () => UnityEngine.Object.Instantiate(prefab, parent),
                item => item.gameObject.SetActive(true),
                item => item.gameObject.SetActive(false),
                item => UnityEngine.Object.Destroy(item.gameObject),
                collectionCheck: true,
                defaultCapacity: defaultCapacity);
        }

        public IReadOnlyList<T> Active => _active;

        public T Get()
        {
            T item = _pool.Get();
            _active.Add(item);
            return item;
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                _pool.Release(_active[i]);
            }
            _active.Clear();
        }
    }
}
