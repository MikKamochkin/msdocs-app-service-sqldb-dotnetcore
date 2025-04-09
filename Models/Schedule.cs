using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DotNetCoreSqlDb.Models
{
    public class Schedule
    {
        [Key]
        public Guid Id { get; set; }

        [ForeignKey("Assignment")]
        public Guid AssignmentId { get; set; }
        public Assignments Assignment { get; set; }

        [DisplayName("DateTime")]
        [Required]
        public required DateTime DateTime { get; set; }

        [DisplayName("Status")]
        [Required]
        public required string Status { get; set; }

        [DisplayName("Duration")]
        [Required]
        public required int Duration { get; set; }
    }
}