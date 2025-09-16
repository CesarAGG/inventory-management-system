using Dropbox.Api;
using Dropbox.Api.Files;
using System;
using System.IO;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services;

public class CloudStorageService : ICloudStorageService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CloudStorageService> _logger;

    public CloudStorageService(IConfiguration configuration, ILogger<CloudStorageService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string?> UploadFileAsync(string fileName, Stream contentStream)
    {
        var accessToken = _configuration["CloudStorage:Dropbox:AccessToken"];
        var targetFolder = _configuration["CloudStorage:Dropbox:TargetFolder"];

        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(targetFolder))
        {
            _logger.LogError("Dropbox configuration for AccessToken or TargetFolder is missing.");
            throw new InvalidOperationException("Dropbox storage is not properly configured.");
        }

        try
        {
            using var dbx = new DropboxClient(accessToken);
            var fullPath = $"{targetFolder}/{fileName}";

            using var memoryStream = new MemoryStream();
            await contentStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var uploadedFile = await dbx.Files.UploadAsync(
                fullPath,
                WriteMode.Overwrite.Instance,
                body: memoryStream);

            _logger.LogInformation("Successfully uploaded file {FileName} to Dropbox. Path: {Path}", fileName, uploadedFile.PathDisplay);
            return uploadedFile.PathDisplay;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to Dropbox.");
            return null;
        }
    }
}