using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace DotNetCoreSqlDb.Models
{
    public class Conversations
    {
        [Key]
        public Guid Id { get; set; }

        [ForeignKey("Student")]
        public Guid StudentID { get; set; }        
        public Student? Student { get; set; }

        [ForeignKey("Teacher")]
        public Guid? TeacherId { get; set; }
        public Teacher? Teacher { get; set; }

        public Guid? LastMessageSeenByStudentId { get; set; }

        public Guid? LastMessageSeenByTeacherId { get; set; }

        public DateTime? LastMessageTime { get; set; }
        [DisplayName("IsActive")]
        [Required]
        public required bool IsActive { get; set; }
    }
}