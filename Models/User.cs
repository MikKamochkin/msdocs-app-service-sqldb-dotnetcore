using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DotNetCoreSqlDb.Models
{
    public class User
    {
        [Key]
        public Guid ID { get; set; } // Primary Key for the contact record

        [DisplayName("Username")]
        [Required]
        public required string Username { get; set; }

        /*[DisplayName("Password")]
        [Required]
        public required string Password { get; set; }*/

        [Required]
        public byte[] PasswordHash { get; set; } = null!;

        [Required]
        public byte[] PasswordSalt { get; set; } = null!;

        [Required]
        public bool MustChangePassword {get; set;} = true;

        [DisplayName("Role")]
        [Required]
        public required string Role { get; set; }
    }
}
