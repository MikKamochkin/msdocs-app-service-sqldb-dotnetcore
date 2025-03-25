using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DotNetCoreSqlDb.Models
{
    public class User
    {
        [Key]
        public int ID { get; set; } // Primary Key for the contact record

        [DisplayName("Name")]
        public string? Name { get; set; }

        [DisplayName("Password")]
        public string? Password { get; set; }

        [DisplayName("Role")]
        public string? Role { get; set; }
    }
}
