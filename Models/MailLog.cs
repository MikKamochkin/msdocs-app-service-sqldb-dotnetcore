using System;
using System.ComponentModel.DataAnnotations;

namespace DotNetCoreSqlDb.Models
{
    public class MailLog
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public string To { get; set; } = null!;
        [Required]
        public string Subject { get; set; } = null!;
        [Required]
        public string Body { get; set; } = null!;
        [Required]
        public DateTime Time { get; set; }
        
    }
}
