using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;


namespace DotNetCoreSqlDb.Models
{
    public class StudentBalance
    {
        [Key]
        public Guid Id { get; set; }

        [ForeignKey("Student")]
        public Guid StudentId { get; set; }

        [ForeignKey("Assignment")]
        public Guid AssignmentId { get; set; }
        
        public float Balance { get; set; }


        /* ─── Navigation references ─────────────────────── */
        public Student Student { get; set; } = null!;
        public Assignments Assignment { get; set; } = null!;
    }
}