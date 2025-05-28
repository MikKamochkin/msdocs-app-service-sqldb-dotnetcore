using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace DotNetCoreSqlDb.Models
{
    public class Teacher
    {
        [Key]
        public Guid Id { get; set; }

        [DisplayName("Name")]
        [Required]
        public required string Name { get; set; }

        [DisplayName("TimeZone")]
        public string? TimeZoneId { get; set; } = "Eastern Standard Time";

        [DisplayName("Email")]
        public string? Email { get; set; } = "";

        // Navigation property
        public virtual ICollection<Assignments> Assignments { get; set; } = new List<Assignments>();
    }
} 