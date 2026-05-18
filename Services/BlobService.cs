using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DotNetCoreSqlDb.Models;

namespace DotNetCoreSqlDb.Services
{
    public class BlobService : IBlobService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName = "blob-container";
        private readonly ILogger<BlobService> _logger;

        public string ContainerName => _containerName;

        public BlobService(
            BlobServiceClient blobServiceClient,
            ILogger<BlobService> logger)
        {
            _blobServiceClient = blobServiceClient;
            _logger = logger;
        }

        public async Task<MessageAttachment> UploadMessageAttachmentAsync(
            IFormFile file,
            Guid messageId,
            Guid conversationId)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file was uploaded.");

            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);

            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

            var originalFileName = Path.GetFileName(file.FileName);

            var blobName =
                $"conversations/{conversationId}/messages/{messageId}/{Guid.NewGuid()}-{originalFileName}";

            var blobClient = containerClient.GetBlobClient(blobName);

            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                        ? "application/octet-stream"
                        : file.ContentType
                },
                Metadata = new Dictionary<string, string>
                {
                    ["messageId"] = messageId.ToString(),
                    ["conversationId"] = conversationId.ToString(),
                    ["originalFileName"] = originalFileName,
                    ["uploadedAtUtc"] = DateTime.UtcNow.ToString("O")
                }
            };

            await using var stream = file.OpenReadStream();

            await blobClient.UploadAsync(stream, uploadOptions);

            _logger.LogInformation(
                "Uploaded message attachment. MessageId={MessageId}, BlobName={BlobName}",
                messageId,
                blobName);

            return new MessageAttachment
            {
                Id = Guid.NewGuid(),
                MessageId = messageId,
                OriginalFileName = originalFileName,
                BlobName = blobName,
                ContainerName = _containerName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                    ? "application/octet-stream"
                    : file.ContentType,
                SizeBytes = file.Length,
                UploadedAtUtc = DateTime.UtcNow
            };
        }

        public async Task<BlobDownloadResult> DownloadAttachmentAsync(string blobName)
        {
            if (string.IsNullOrWhiteSpace(blobName))
                throw new ArgumentException("Blob name is required.");

            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);

            var blobClient = containerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync())
                throw new FileNotFoundException("Blob file was not found.", blobName);

            var downloadResult = await blobClient.DownloadContentAsync();

            return downloadResult.Value;
        }
    }
}