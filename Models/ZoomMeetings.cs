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

        // ── second meeting slot ─────────────────────────────────────────────

        [DisplayName("JoinUrl2")]
        public string? JoinUrl2 { get; set; } = string.Empty;

        [DisplayName("MeetingId2")]
        public string? MeetingId2 { get; set; } = string.Empty;

        [DisplayName("MeetingPassword2")]
        public string? MeetingPassword2 { get; set; } = string.Empty;

        [DisplayName("IsBusy2")]
        public bool IsBusy2 { get; set; } = false;

        [DisplayName("StartTime2")]
        public DateTime? StartTime2 { get; set; }

        [DisplayName("Duration2")]
        public int? Duration2 { get; set; }

        [ForeignKey("Schedule2")]
        public Guid? ScheduleId2 { get; set; }

        [DisplayName("UUid2")]
        public string? UUid2 { get; set; } = string.Empty;

        public virtual Schedule? Schedule2 { get; set; }

    }
} 