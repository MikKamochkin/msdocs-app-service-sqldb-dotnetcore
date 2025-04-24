using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace DotNetCoreSqlDb.Models
{
    public class Assignments
    {
        [Key]
        public Guid Id { get; set; }

        [ForeignKey("Group")]
        public Guid GroupId { get; set; }
        
        public Group? Group { get; set; }

        [ForeignKey("Teacher")]
        public Guid? TeacherId { get; set; }
        public Teacher? Teacher { get; set; }

        [DisplayName("StudentUnitCost")]
        [Required]
        public required float StudentUnitCost { get; set; }

        [DisplayName("StudentUnitType")]
        [Required]
        public required string StudentUnitType { get; set; }

        [DisplayName("StudentUnitBalance")]
        [Required]
        public required float StudentUnitBalance { get; set; }

        [DisplayName("StudentUnitDuration")]
        [Required]
        public required float StudentUnitDuration { get; set; }

        [DisplayName("TeacherPayForUnit")]
        [Required]
        public required float TeacherPayForUnit { get; set; }

        [DisplayName("TeacherPayUnitType")]
        [Required]
        public required string TeacherPayUnitType { get; set; }

        [DisplayName("IsActive")]
        [Required]
        public required bool IsActive { get; set; }

        // Navigation property for one-to-many relationship with Schedule
        public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
    }
}