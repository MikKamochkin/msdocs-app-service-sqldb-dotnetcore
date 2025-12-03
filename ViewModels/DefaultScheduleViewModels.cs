using System;
using System.Collections.Generic;

namespace DotNetCoreSqlDb.ViewModels
{
    public class TeacherVm
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class AssignmentOptionVm
    {
        public Guid Id { get; set; }
        public string GroupName { get; set; } = "";
    }

    public class DefaultScheduleCellVm
    {
        public Guid? Id { get; set; }                 // DefaultSchedule.Id
        public Guid TeacherId { get; set; }
        public Guid? AssignmentId { get; set; }
        public string? StudentName { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public TimeSpan TimeOfDay { get; set; }
    }

    public class DefaultSchedulePageVm
    {
        public List<TeacherVm> Teachers { get; set; } = new();
        public List<TimeSpan> Times { get; set; } = new();
        public List<DayOfWeek> Days { get; set; } = new();
        public List<DefaultScheduleCellVm> Cells { get; set; } = new();

        // For each teacher, which students (assignments) they can choose in a cell
        public Dictionary<Guid, List<AssignmentOptionVm>> TeacherAssignments { get; set; } = new();
    }

    public class UpdateDefaultCellDto
    {
        public Guid TeacherId { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public string Time { get; set; } = "";        // "HH:mm"
        public Guid? AssignmentId { get; set; }       // null/empty = clear cell
    }
}
