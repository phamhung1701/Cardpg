using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;

    public static T Instance
    {
        get
        {
            lock (typeof(Singleton<T>))
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<T>();
                    if (_instance == null)
                    {
                        var go = new GameObject($"[{typeof(T).Name}]");
                        DontDestroyOnLoad(go);
                        _instance = go.AddComponent<T>();
                    }
                }
                return _instance;
            }
        }
    }

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this as T;
        DontDestroyOnLoad(gameObject);
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}