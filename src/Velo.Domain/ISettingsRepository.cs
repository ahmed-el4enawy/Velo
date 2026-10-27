namespace Velo.Domain;

/// <summary>
/// Repository contract for persisting and retrieving keyed user preferences.
/// </summary>
public interface ISettingsRepository
{
    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
    Task<string?> GetRawValueAsync(string key, CancellationToken cancellationToken = default);
    Task SetRawValueAsync(string key, string value, CancellationToken cancellationToken = default);
}
