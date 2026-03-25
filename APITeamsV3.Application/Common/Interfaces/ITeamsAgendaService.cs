using System;
using System.Threading.Tasks;

namespace APITeamsV3.Application.Common.Interfaces
{
    public interface ITeamsAgendaService
    {
        Task<string> CreateChannelMeetingAsync(string teamId, string channelId, string subject, string content, DateTime start, DateTime end);
        Task DeleteMeetingAsync(string teamId, string eventId);
        Task<string> GetPrimaryChannelIdAsync(string teamId);
    }
}
