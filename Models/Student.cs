using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace DotNetCoreSqlDb.Models
{
    public class Student
    {
        public int ID { get; set; } // Primary Key

        [DisplayName("Name")]
        [Required]
        public required string Name { get; set; } 

        [DisplayName("Parents/Employer")]
        public string ParentOrEmployer { get; set; } = string.Empty;

        [DisplayName("Main Notes")]
        public string MainNotes { get; set; } = string.Empty;

        [DisplayName("Ongoing Notes")]
        public string OngoingNotes { get; set; } = string.Empty;

        // Contact Info:
        [DisplayName("Email")]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; } = string.Empty;

        [DisplayName("Phone Number")]
        [DataType(DataType.PhoneNumber)]
        public string PhoneNumber { get; set; } = string.Empty;

        [DisplayName("Created Date")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime CreatedDate { get; set; }
    }
}
