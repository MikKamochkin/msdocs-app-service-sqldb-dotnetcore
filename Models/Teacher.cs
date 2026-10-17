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

        [StringLength(200)]
        public string? StreetAddress { get; set; }
        [StringLength(100)]
        public string? City { get; set; }
        [StringLength(100)]
        public string? ProvinceOrState { get; set; }
        [StringLength(30)]
        public string? PostalCode { get; set; }
        [StringLength(100)]
        public string? Country { get; set; }
        [StringLength(50)]
        public string? PhoneNumber { get; set; }
        [StringLength(254)]
        public string? ETransferEmail { get; set; }

        public bool? EnableInvoicing { get; set; } = false;

        // Navigation property
        public virtual ICollection<Assignments> Assignments { get; set; } = new List<Assignments>();
    }
} 