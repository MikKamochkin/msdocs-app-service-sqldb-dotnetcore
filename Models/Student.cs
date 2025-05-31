using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace DotNetCoreSqlDb.Models
{
    public class Student
    {
        public Guid ID { get; set; } // Primary Key

        [DisplayName("Name")]
        [Required]
        public required string Name { get; set; } 

        [DisplayName("Parents/Employer")]
        public string? ParentOrEmployer { get; set; }

        [DisplayName("Main Notes")]
        public string? MainNotes { get; set; }

        /*[DisplayName("Ongoing Notes")]
        public string? OngoingNotes { get; set; }
        */

        [DisplayName("Source")]
        public string? Source { get; set;}

        [DisplayName("TimeZone")]
        public string? TimeZoneId { get; set;} = "Eastern Standard Time";

        [DisplayName("AccountingGroup")]
        public string AccountingGroup { get; set;} = "";

        [DisplayName("Created Date")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime CreatedDate { get; set; }

        [DisplayName("Status")]
        public string? Status { get; set; }

        // Navigation property for one-to-many relationship with Contact
        public virtual ICollection<Contact> Contacts { get; set; } = new List<Contact>();

         // Navigation property for one-to-many relationship with Note
        public virtual ICollection<Notes> Notes { get; set; } = new List<Notes>();

        // Navigation property for one-to-many relationship with StudentGroupComposition
        public virtual ICollection<StudentGroupComposition> StudentGroupCompositions { get; set; } = new List<StudentGroupComposition>();
    }
}