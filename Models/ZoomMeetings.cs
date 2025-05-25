using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DotNetCoreSqlDb.Models
{
    public class ZoomMeetings
    {
        [Key]
        public Guid Id { get; set; }

        [DisplayName("ZoomId")]
        public string ZoomId { get; set; } = "";

        [DisplayName("Email")]
        public string Email { get; set; } = "";

        /*[DisplayName("Pmi")]
        public string Pmi { get; set; } = "";*/
        
        [DisplayName("JoinUrl")]
        public string? JoinUrl { get; set; } = "";

        [DisplayName("MeetingId")]
        public string? MeetingId { get; set; } = "";

        [DisplayName("MeetingPassword")]
        public string? MeetingPassword { get; set; } = "";

        [DisplayName("IsBusy")]
        public bool IsBusy { get; set; } = false;

        [DisplayName("StartTime")]
        public DateTime? StartTime { get; set; }

        [DisplayName("Duration")]
        public int? Duration { get; set; } = 0;

        [ForeignKey("Schedule")]
        public Guid? ScheduleId { get; set; }

        [DisplayName("UUid")]
        public string? UUid { get; set; } = "";
        
        public virtual Schedule? Schedule { get; set; }

    }
} 