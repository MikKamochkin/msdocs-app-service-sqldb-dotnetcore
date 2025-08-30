using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace DotNetCoreSqlDb.Models
{
    public class Group
    {
        [Key]
        public Guid Id { get; set; }

        [DisplayName("Name")]
        [Required]
        public required string Name { get; set; }

        [DisplayName("IsActive")]
        public bool IsActive { get; set;} = false;

        public bool IsManualGroup { get; set; }



        // Navigation properties
        public virtual ICollection<StudentGroupComposition> StudentGroupCompositions { get; set; } = new List<StudentGroupComposition>();
        public virtual ICollection<Assignments> Assignments { get; set; } = new List<Assignments>();
    }
}