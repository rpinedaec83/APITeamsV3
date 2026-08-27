using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.Common.Interfaces
{
    public class TeamsMeetingRequest
    {
        public string TeamId { get; set; } = string.Empty;
        public string ChannelId { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string HtmlContent { get; set; } = string.Empty;
        public DateTime FirstOccurrenceStart { get; set; }
        public DateTime FirstOccurrenceEnd { get; set; }
        public DateTime RecurrenceStartDate { get; set; }
        public DateTime RecurrenceEndDate { get; set; }
        public IReadOnlyCollection<DayOfWeek> RecurrenceDays { get; set; } = Array.Empty<DayOfWeek>();
        public IReadOnlyCollection<string> RequiredAttendeeEmails { get; set; } = Array.Empty<string>();
        public IReadOnlyCollection<string> PresenterEmails { get; set; } = Array.Empty<string>();
    }

    public class TeamsMeetingResult
    {
        public string EventId { get; set; } = string.Empty;
        public string JoinUrl { get; set; } = string.Empty;
    }

    public class TeamsMeetingUpdateRequest
    {
        public string TeamId { get; set; } = string.Empty;
        public string EventId { get; set; } = string.Empty;
        public string JoinUrl { get; set; } = string.Empty;
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
        public IReadOnlyCollection<string> RequiredAttendeeEmails { get; set; } = Array.Empty<string>();
        public IReadOnlyCollection<string> PresenterEmails { get; set; } = Array.Empty<string>();
    }

    public interface ITeamsAgendaService
    {
        Task<TeamsMeetingResult> CreateRecurringChannelMeetingAsync(TeamsMeetingRequest request, CancellationToken cancellationToken = default);
        Task<TeamsMeetingResult> UpdateMeetingAsync(TeamsMeetingUpdateRequest request, CancellationToken cancellationToken = default);
        Task DeleteMeetingAsync(string teamId, string eventId);
        Task<string> GetPrimaryChannelIdAsync(string teamId, CancellationToken cancellationToken = default);
        Task AddCoorganizerAsync(string joinUrl, IReadOnlyCollection<string> teacherEmails, CancellationToken cancellationToken = default);
        Task EnsureTeacherCoorganizerForSectionAsync(int idSeccion, CancellationToken cancellationToken = default);
    }
}
