using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;


namespace DotNetCoreSqlDb.Models
{
    public class EmailPaymentsLog
    {
        [Key]
        public Guid Id { get; set; }
        
        public bool? HandledAutomatically { get; set; }

        [ForeignKey("StudentBalanceTransactionLog")]
        public Guid? StudentBalanceTransactionLogId { get; set; }

        public DateTime? DateTime { get; set; }

        public string? ReasonForFailure { get; set; }

        public string? StudentName { get; set; }

        public string? PayerName { get; set; }

        public string? EmailSubject { get; set; }

        public StudentBalanceTransactionLog? StudentBalanceTransactionLog { get; set; } = null!;
    }
}