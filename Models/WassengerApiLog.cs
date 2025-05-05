using System;
using System.ComponentModel.DataAnnotations;

namespace DotNetCoreSqlDb.Models
{
    public class WassengerApiLog
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        // e.g. "POST /v1/messages { \"phone\":\"...\", ... }"
        [Required]
        public string Request { get; set; } = null!;

        [Required]
        public DateTime Time { get; set; }

        // full response JSON or error message
        [Required]
        public string Response { get; set; } = null!;
    }
}
