using System.Collections.Generic;
using UnityEngine;

public class ObjectPool<T> where T : Component
{
    readonly T _prefab;
    readonly Transform _parent;
    readonly Queue<T> _pool = new();

    public ObjectPool(T prefab, Transform parent, int initialSize = 0)
    {
        _prefab = prefab;
        _parent = parent;
        for (int i = 0; i < initialSize; i++)
        {
            var obj = UnityEngine.Object.Instantiate(_prefab, _parent);
            obj.gameObject.SetActive(false);
            _pool.Enqueue(obj);
        }
    }

    public T Get()
    {
        T obj = _pool.Count > 0 ? _pool.Dequeue() : UnityEngine.Object.Instantiate(_prefab, _parent);
        obj.gameObject.SetActive(true);
        return obj;
    }

    public void Return(T obj)
    {
        obj.gameObject.SetActive(false);
        _pool.Enqueue(obj);
    }

    public void ReturnAll()
    {
        foreach (var obj in _pool)
            if (obj != null) obj.gameObject.SetActive(false);
    }

    public int CountInactive => _pool.Count;
}
