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
        public string? ParentOrEmployer { get; set; }

        [DisplayName("Main Notes")]
        public string? MainNotes { get; set; }

        [DisplayName("Ongoing Notes")]
        public string? OngoingNotes { get; set; }

        // Contact Info:
        [DisplayName("Email")]
        [DataType(DataType.EmailAddress)]
        public string? Email { get; set; }

        [DisplayName("Phone Number")]
        [DataType(DataType.PhoneNumber)]
        public string? PhoneNumber { get; set; }

        [DisplayName("Facebook")]
        public string? Facebook { get; set; }

        [DisplayName("Instagram")]
        public string? Instagram { get; set; }

        [DisplayName("Telegram")]
        public string? Telegram { get; set; }

        [DisplayName("Twitter")]
        public string? Twitter { get; set; }

        [DisplayName("Created Date")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime CreatedDate { get; set; }
    }
}
