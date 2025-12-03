using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;


namespace DotNetCoreSqlDb.Models
{
    public class ZoomMeetingLog
    {
        [Key]
        public Guid Id { get; set; }

        [ForeignKey("Schedule")]
        public Guid? ScheduleId { get; set; }

        [ForeignKey("ZoomMeeting")]
        public Guid? ZoomMeetingId { get; set; }

        //From ZoomMeetings table:
        //----------------------------------------------------------------------------------------

        [DisplayName("ZoomId")]
        public string? ZoomId { get; set; } = "";

        [DisplayName("Email")]
        public string? Email { get; set; } = "";

        [DisplayName("JoinUrl")]
        public string? JoinUrl { get; set; } = "";

        [DisplayName("StartTime")]
        public DateTime? StartTime { get; set; }

        [DisplayName("EndTime")]
        public DateTime? EndTime { get; set; }

        [DisplayName("Duration")]
        public int? Duration { get; set; } = 0;

        //----------------------------------------------------------------------------------------
        public DateTime? DateTime { get; set; }

        public Schedule Schedule { get; set; } = null!;

        public ZoomMeetings ZoomMeeting { get; set; } = null!;
    }
}