using System.Collections.Concurrent;

namespace McpDotnet;

/// <summary>
/// A singleton class that manages shared state across all tools.
/// Thread-safe implementation using ConcurrentDictionary for concurrent access.
/// </summary>
public sealed class StateManager
{
    private static readonly Lazy<StateManager> _instance = new(() => new StateManager());
    private readonly ConcurrentDictionary<string, object?> _state = new();

    /// <summary>
    /// Gets the singleton instance of StateManager.
    /// </summary>
    public static StateManager Instance => _instance.Value;

    private StateManager() { }

    /// <summary>
    /// Get a value from the state by key.
    /// </summary>
    /// <typeparam name="T">Type of the value</typeparam>
    /// <param name="key">The key to look up</param>
    /// <param name="defaultValue">The default value to return if key is not found</param>
    /// <returns>The value associated with the key, or the default value if not found</returns>
    public T? Get<T>(string key, T? defaultValue = default)
    {
        if (_state.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Get a value from the state by key (non-generic version).
    /// </summary>
    /// <param name="key">The key to look up</param>
    /// <param name="defaultValue">The default value to return if key is not found</param>
    /// <returns>The value associated with the key, or the default value if not found</returns>
    public object? Get(string key, object? defaultValue = null)
    {
        return _state.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Set a value in the state.
    /// </summary>
    /// <param name="key">The key to store the value under</param>
    /// <param name="value">The value to store</param>
    /// <returns>The value that was stored</returns>
    public T Set<T>(string key, T value)
    {
        _state[key] = value;
        return value;
    }

    /// <summary>
    /// Set a value in the state (non-generic version).
    /// </summary>
    /// <param name="key">The key to store the value under</param>
    /// <param name="value">The value to store</param>
    /// <returns>The value that was stored</returns>
    public object? Set(string key, object? value)
    {
        _state[key] = value;
        return value;
    }

    /// <summary>
    /// Delete a value from the state.
    /// </summary>
    /// <param name="key">The key to delete</param>
    /// <returns>True if the key was deleted, False if it didn't exist</returns>
    public bool Delete(string key)
    {
        return _state.TryRemove(key, out _);
    }

    /// <summary>
    /// Check if a key exists in the state.
    /// </summary>
    /// <param name="key">The key to check</param>
    /// <returns>True if the key exists, False otherwise</returns>
    public bool Has(string key)
    {
        return _state.ContainsKey(key);
    }

    /// <summary>
    /// Get a copy of the entire state dictionary.
    /// </summary>
    /// <returns>A copy of the state dictionary</returns>
    public Dictionary<string, object?> GetAll()
    {
        return new Dictionary<string, object?>(_state);
    }

    /// <summary>
    /// Clear all state data.
    /// </summary>
    public void Clear()
    {
        _state.Clear();
    }

    /// <summary>
    /// Get the number of items in the state.
    /// </summary>
    public int Count => _state.Count;

    /// <summary>
    /// Get all keys in the state.
    /// </summary>
    /// <returns>Collection of all keys</returns>
    public IEnumerable<string> Keys => _state.Keys;

    /// <summary>
    /// Get all values in the state.
    /// </summary>
    /// <returns>Collection of all values</returns>
    public IEnumerable<object?> Values => _state.Values;

    /// <summary>
    /// Try to get a value from the state.
    /// </summary>
    /// <typeparam name="T">Type of the value</typeparam>
    /// <param name="key">The key to look up</param>
    /// <param name="value">The value if found</param>
    /// <returns>True if key was found and value is of correct type</returns>
    public bool TryGet<T>(string key, out T? value)
    {
        if (_state.TryGetValue(key, out var obj) && obj is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = default;
        return false;
    }
}
