using System.IO;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services;

public interface ICloudStorageService
{
    Task<string?> UploadFileAsync(string fileName, Stream contentStream);
}