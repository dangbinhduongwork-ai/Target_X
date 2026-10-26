namespace GoalOverlay.Services;

/// <summary>
/// Giao diện trừu tượng hóa thao tác với Windows Registry phục vụ Unit Test và Fake Registry
/// </summary>
public interface IRegistryService
{
    object? GetCurrentUserValue(string subKeyPath, string valueName);
    void SetCurrentUserValue(string subKeyPath, string valueName, object value);
    void DeleteCurrentUserValue(string subKeyPath, string valueName);
}
