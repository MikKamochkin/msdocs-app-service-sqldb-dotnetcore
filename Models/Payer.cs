using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace DotNetCoreSqlDb.Models
{
    public class Payer
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        [ForeignKey("Student")]
        public Guid StudentId { get; set; }

        // Navigation properties
        public virtual Student? Student { get; set; } = null!;
    }
}