using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace DotNetCoreSqlDb.Models
{
    public class Messages
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [ForeignKey("Sender")]
        public Guid SenderId { get; set; }        
        public User? Sender { get; set; }

        public string? Text { get; set; }
        
        public string? FileLink { get; set; }
        
        public DateTime TimeSent { get; set; }

        [ForeignKey("Conversations")]
        public Guid ConversationId { get; set; }        
        public Conversations? Conversation { get; set; }
        
        public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();
    }
}
