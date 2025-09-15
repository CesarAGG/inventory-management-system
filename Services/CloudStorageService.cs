using Azure.Identity;
using Microsoft.Graph;
using System;
using System.IO;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services;

public class CloudStorageService : ICloudStorageService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CloudStorageService> _logger;
    private GraphServiceClient? _graphClient;

    public CloudStorageService(IConfiguration configuration, ILogger<CloudStorageService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private GraphServiceClient GetGraphClient()
    {
        if (_graphClient != null)
        {
            return _graphClient;
        }

        var configSection = _configuration.GetSection("CloudStorage:OneDrive");
        var tenantId = configSection["TenantId"];
        var clientId = configSection["ClientId"];
        var clientSecret = configSection["ClientSecret"];

        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            _logger.LogError("OneDrive configuration for TenantId, ClientId, or ClientSecret is missing.");
            throw new InvalidOperationException("OneDrive authentication is not properly configured.");
        }

        var options = new ClientSecretCredentialOptions
        {
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
        };

        var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret, options);
        _graphClient = new GraphServiceClient(clientSecretCredential, new[] { "https://graph.microsoft.com/.default" });

        return _graphClient;
    }

    public async Task<string?> UploadFileAsync(string fileName, Stream contentStream)
    {
        try
        {
            var graphClient = GetGraphClient();
            var configSection = _configuration.GetSection("CloudStorage:OneDrive");
            var userId = configSection["UserId"];
            var targetFolder = configSection["TargetFolder"];

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(targetFolder))
            {
                _logger.LogError("OneDrive UserId or TargetFolder is missing from configuration.");
                throw new InvalidOperationException("OneDrive user/folder configuration is not properly set.");
            }

            // Use a memory stream for retry capability, as the original might be closed or non-seekable.
            using var memoryStream = new MemoryStream();
            await contentStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var uploadedItem = await graphClient.Users[userId].Drive.Root
                .ItemWithPath(targetFolder + "/" + fileName)
                .Content
                .PutAsync(memoryStream);

            _logger.LogInformation("Successfully uploaded file {FileName} to OneDrive. File ID: {FileId}", fileName, uploadedItem?.Id);
            return uploadedItem?.WebUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to OneDrive.");
            return null;
        }
    }
}