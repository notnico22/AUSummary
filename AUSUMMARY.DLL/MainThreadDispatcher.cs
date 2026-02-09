using System;
using System.Collections.Generic;
using UnityEngine;

namespace AUSUMMARY.DLL;

/// <summary>
/// Dispatches actions to the Unity main thread to avoid IL2CPP GC issues
/// </summary>
public class MainThreadDispatcher : MonoBehaviour
{
    private static MainThreadDispatcher? _instance;
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();
    private static readonly object _lock = new object();

    public static MainThreadDispatcher Instance
    {
        get
        {
            if (_instance == null)
            {
                throw new InvalidOperationException("MainThreadDispatcher not initialized! Make sure it's added as a component in the plugin.");
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        lock (_lock)
        {
            while (_executionQueue.Count > 0)
            {
                try
                {
                    var action = _executionQueue.Dequeue();
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error executing main thread action: {ex}");
                }
            }
        }
    }

    /// <summary>
    /// Enqueues an action to be executed on the main Unity thread
    /// </summary>
    public static void Enqueue(Action action)
    {
        if (action == null)
            return;

        lock (_lock)
        {
            _executionQueue.Enqueue(action);
        }
    }

    /// <summary>
    /// Checks if we're currently on the main thread
    /// </summary>
    public static bool IsMainThread()
    {
        return System.Threading.Thread.CurrentThread.ManagedThreadId == 1;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
