using System;
using System.ComponentModel.DataAnnotations;

namespace DotNetCoreSqlDb.Models
{
    public class TwilioLessonRemindersLog
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string Body { get; set; } = null!;

        [Required]
        public DateTime DateTime { get; set; }

        [Required]
        public string Number { get; set; } = null!;
        public Guid? ScheduleId { get; set; }
    }
}
