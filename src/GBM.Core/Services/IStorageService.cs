using GBM.Core.Models;

namespace GBM.Core.Services;

public interface IStorageService
{
    List<DeviceProfile> LoadProfiles();
    void SaveProfiles(List<DeviceProfile> profiles);
    void ClearProfiles();
}
