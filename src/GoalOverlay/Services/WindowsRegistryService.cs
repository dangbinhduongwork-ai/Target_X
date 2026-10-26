using System;
using Microsoft.Win32;

namespace GoalOverlay.Services;

/// <summary>
/// Thao tác thực tế với Windows Registry (HKCU)
/// </summary>
public class WindowsRegistryService : IRegistryService
{
    public object? GetCurrentUserValue(string subKeyPath, string valueName)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(subKeyPath, writable: false);
        return key?.GetValue(valueName);
    }

    public void SetCurrentUserValue(string subKeyPath, string valueName, object value)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(subKeyPath, writable: true);
        if (key == null)
        {
            throw new InvalidOperationException($"Không thể mở Registry key với quyền ghi: HKCU\\{subKeyPath}");
        }
        key.SetValue(valueName, value);
    }

    public void DeleteCurrentUserValue(string subKeyPath, string valueName)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(subKeyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
