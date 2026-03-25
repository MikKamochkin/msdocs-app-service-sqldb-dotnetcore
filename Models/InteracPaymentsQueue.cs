using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;


namespace DotNetCoreSqlDb.Models
{
    public class InteracPaymentsQueue
    {
        [Key]
        public Guid Id { get; set; }

        public DateTime? AddedToQueueTime { get; set; }

        public string? From { get; set; }

        public string? Subject { get; set; }

        public string? Body { get; set; }

        public string? ReasonForFailure { get; set; }
        public string? ReplyTo { get; set; }
    }
}