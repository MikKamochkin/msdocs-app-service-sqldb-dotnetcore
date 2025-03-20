using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DotNetCoreSqlDb.Models
{
    public class Contact
    {
        [Key]
        public int ID { get; set; } // Primary Key for the contact record

        // Foreign key to associate the contact with a student
        [ForeignKey("Student")]
        public int StudentID { get; set; }

        [DisplayName("Type")]
        public string? Type { get; set; }

        [DisplayName("Value")]
        public string? Value { get; set; }

        [DisplayName("Invitation")]
        public bool? Invitation { get; set; }

        [DisplayName("Emergency")]
        public bool? Emergency { get; set; }

        [DisplayName("Money")]
        public bool? Money { get; set; }

        // Navigation property to the related Student
        public virtual Student? Student { get; set; }
    }
}
