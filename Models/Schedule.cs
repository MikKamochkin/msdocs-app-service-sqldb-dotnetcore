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
        public Assignments? Assignment { get; set; }

        [DisplayName("DateTime")]
        [Required]
        public required DateTime DateTime { get; set; }

        [DisplayName("Status")]
        [Required]
        public required string Status { get; set; }

        [DisplayName("Duration")]
        [Required]
        public required int Duration { get; set; }

        [DisplayName("Accounted")]
        [Required]
        public required bool Accounted { get; set; } = false;

        [DisplayName("LessonAccountingType")]
        [Required]
        public required string LessonAccountingType { get; set; } = "S100T100";

        [DisplayName("HasReachedMinimumDuration")]
        public bool? HasReachedMinimumDuration { get; set; } = false;

        [DisplayName("StudentChargeAmount")]
        [Required]
        public required float StudentChargeAmount { get; set; }

        [DisplayName("StudentChargeCurrency")]
        [Required]
        public required string StudentChargeCurrency { get; set; }

        [DisplayName("TeacherPayAmount")]
        [Required]
        public required float TeacherPayAmount { get; set; }

        [DisplayName("TeacherPayCurrency")]
        [Required]
        public required string TeacherPayCurrency { get; set; }

        [ForeignKey("Group")]
        public Guid GroupId { get; set; }
        public Group? Group { get; set; }

        [ForeignKey("Teacher")]
        public Guid TeacherId { get; set; }
        public Teacher? Teacher { get; set; }
    }
}