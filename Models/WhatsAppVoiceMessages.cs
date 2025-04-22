using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DotNetCoreSqlDb.Models
{
    public class WhatsAppVoiceMessages
    {
        [Key]
        public Guid Id { get; set; }

        [DisplayName("FileUrl")]
        public string FileUrl { get; set; } = "";

        [DisplayName("Description")]
        public string Description { get; set; } = "";

        [DisplayName("Wassenger Message ID")]
        public string? WassengerMessageId { get; set; }

        [DisplayName("Name")]
        public string? Name { get; set; }

        

    }
} 