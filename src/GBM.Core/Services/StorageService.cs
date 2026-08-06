using System.Text.Json;
using GBM.Core.Models;
using Microsoft.Extensions.Logging;

namespace GBM.Core.Services;

public class StorageService : IStorageService
{
    private readonly ILogger<StorageService> _logger;
    private readonly ISettingsService _settingsService;
    private readonly object _profilesLock = new();

    private const string ProfilesFileName = "conn_profile.json";

    public StorageService(ILogger<StorageService> logger, ISettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
    }

    public List<DeviceProfile> LoadProfiles()
    {
        lock (_profilesLock)
        {
            return LoadFromFile(GetProfilesPath()) ?? new List<DeviceProfile>();
        }
    }

    public void SaveProfiles(List<DeviceProfile> profiles)
    {
        lock (_profilesLock)
        {
            SaveToFile(GetProfilesPath(), profiles);
        }
    }

    public void ClearProfiles()
    {
        lock (_profilesLock)
        {
            try
            {
                var profilesPath = GetProfilesPath();
                if (File.Exists(profilesPath))
                {
                    File.Delete(profilesPath);
                    _logger.LogInformation("Cleared device profiles file");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear device profiles");
            }
        }
    }

    private List<DeviceProfile>? LoadFromFile(string filePath)
    {
        try
        {
            string actualPath = filePath;

            if (!File.Exists(actualPath))
            {
                // The atomic save writes to .tmp then renames. If the app crashed between
                // deleting the original and renaming the temp file, the data survives in .tmp.
                string tmpPath = filePath + ".tmp";
                if (File.Exists(tmpPath))
                {
                    _logger.LogWarning(
                        "Device profiles not found at {Path} but .tmp exists — recovering from interrupted save",
                        filePath);
                    try
                    {
                        File.Move(tmpPath, filePath);
                        actualPath = filePath;
                    }
                    catch (Exception moveEx)
                    {
                        _logger.LogWarning(moveEx,
                            "Could not rename .tmp to primary path, reading .tmp directly");
                        actualPath = tmpPath;
                    }
                }
                else
                {
                    return null;
                }
            }

            string json = "";
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    json = File.ReadAllText(actualPath);
                    break;
                }
                catch (IOException) when (attempt < 3)
                {
                    Thread.Sleep(50 * attempt);
                }
            }

            return JsonSerializer.Deserialize(json, GbmJsonContext.Default.ListDeviceProfile);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Device profiles file is corrupted at {Path}. Resetting.", filePath);
            TryDeleteFile(filePath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load device profiles from {Path}", filePath);
            return null;
        }
    }

    private void SaveToFile(string filePath, List<DeviceProfile> data)
    {
        string json = JsonSerializer.Serialize(data, GbmJsonContext.Default.ListDeviceProfile);
        string directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);
        string tempPath = filePath + ".tmp";

        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                File.WriteAllText(tempPath, json);

                if (File.Exists(filePath))
                    File.Delete(filePath);

                File.Move(tempPath, filePath);
                return;
            }
            catch (IOException) when (attempt < maxRetries)
            {
                Thread.Sleep(50 * attempt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save device profiles to {Path}", filePath);
                return;
            }
        }
    }

    private void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete corrupted file {Path}", filePath);
        }
    }

    private string GetProfilesPath() =>
        Path.Combine(_settingsService.GetAppDataPath(), ProfilesFileName);
}
