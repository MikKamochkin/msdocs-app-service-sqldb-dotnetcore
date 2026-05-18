using Azure.Storage.Blobs.Models;
using DotNetCoreSqlDb.Models;

namespace DotNetCoreSqlDb.Services
{
    public interface IBlobService
    {
        Task<MessageAttachment> UploadMessageAttachmentAsync(
            IFormFile file,
            Guid messageId,
            Guid conversationId);

        Task<BlobDownloadResult> DownloadAttachmentAsync(string blobName);

        string ContainerName { get; }
    }
}