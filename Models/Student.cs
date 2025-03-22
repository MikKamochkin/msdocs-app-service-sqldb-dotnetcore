using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace DotNetCoreSqlDb.Models
{
    public class Student
    {
        public int ID { get; set; } // Primary Key

        [DisplayName("Name")]
        [Required]
        public required string Name { get; set; } 

        [DisplayName("Parents/Employer")]
        public string? ParentOrEmployer { get; set; }

        [DisplayName("Main Notes")]
        public string? MainNotes { get; set; }

        [DisplayName("Ongoing Notes")]
        public string? OngoingNotes { get; set; }

        [DisplayName("Created Date")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime CreatedDate { get; set; }

        // Navigation property for one-to-many relationship with Contact
        public virtual ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    }
}