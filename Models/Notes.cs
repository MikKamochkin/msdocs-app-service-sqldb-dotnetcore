using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DotNetCoreSqlDb.Models
{
    public class Notes
    {
        [Key]
        public int ID { get; set; } // Primary Key for the contact record

        // Foreign key to associate the contact with a student
        [ForeignKey("Student")]
        public int StudentID { get; set; }

        [DisplayName("Value")]
        public string? Value { get; set; }

        [DisplayName("Created Date")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime CreatedDate { get; set; }
        // Navigation property to the related Student
        public virtual Student? Student { get; set; }
    }
}
