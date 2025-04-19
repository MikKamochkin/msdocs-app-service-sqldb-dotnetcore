using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DotNetCoreSqlDb.Models
{
    public class StudentGroupComposition
    {
        [Key]
        public Guid Id { get; set; }

        [ForeignKey("Group")]
        public Guid GroupId { get; set; }
        public Group Group { get; set; }

        [ForeignKey("Student")]
        public Guid StudentId { get; set; }
        public Student Student { get; set; }

        [DisplayName("Use My Balance")]
        [Required]
        public required bool UseMyBalance { get; set; }
    }
} 