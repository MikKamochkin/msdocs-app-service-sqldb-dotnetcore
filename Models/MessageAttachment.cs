using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace DotNetCoreSqlDb.Models
{
    public class MessageAttachment
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [ForeignKey("Messages")]
        public Guid MessageId { get; set; }        
        public Messages? Message { get; set; }
        public string OriginalFileName { get; set; } = "";
        public string BlobName { get; set; } = "";
        public string ContainerName { get; set; } = "";
        public string ContentType { get; set; } = "";
        public long SizeBytes { get; set; }
        public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
        
    }
}
