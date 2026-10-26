using System;
using System.Collections.Generic;
using GoalOverlay.Services;

namespace GoalOverlay.Tests.Mocks;

/// <summary>
/// Fake Windows Registry lưu trữ giá trị trong bộ nhớ để phục vụ unit test cho AutostartService
/// </summary>
public class FakeRegistryService : IRegistryService
{
    private readonly Dictionary<string, object> _storage = new(StringComparer.OrdinalIgnoreCase);

    public bool ThrowOnRead { get; set; }
    public bool ThrowOnWrite { get; set; }

    private static string MakeKey(string subKeyPath, string valueName) => $"{subKeyPath}\\{valueName}";

    public object? GetCurrentUserValue(string subKeyPath, string valueName)
    {
        if (ThrowOnRead)
        {
            throw new UnauthorizedAccessException("Simulated registry read access denied.");
        }

        _storage.TryGetValue(MakeKey(subKeyPath, valueName), out var val);
        return val;
    }

    public void SetCurrentUserValue(string subKeyPath, string valueName, object value)
    {
        if (ThrowOnWrite)
        {
            throw new UnauthorizedAccessException("Simulated registry write access denied.");
        }

        _storage[MakeKey(subKeyPath, valueName)] = value;
    }

    public void DeleteCurrentUserValue(string subKeyPath, string valueName)
    {
        if (ThrowOnWrite)
        {
            throw new UnauthorizedAccessException("Simulated registry delete access denied.");
        }

        _storage.Remove(MakeKey(subKeyPath, valueName));
    }

    public bool HasValue(string subKeyPath, string valueName)
    {
        return _storage.ContainsKey(MakeKey(subKeyPath, valueName));
    }
}
