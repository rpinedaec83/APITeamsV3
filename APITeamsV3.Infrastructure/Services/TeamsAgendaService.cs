using APITeamsV3.Application.Common.Interfaces;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace APITeamsV3.Infrastructure.Services
{
    public class TeamsAgendaService : ITeamsAgendaService
    {
        private readonly IGraphClientFactory _graphFactory;
        private readonly ILogger<TeamsAgendaService> _logger;

        public TeamsAgendaService(IGraphClientFactory graphFactory, ILogger<TeamsAgendaService> logger)
        {
            _graphFactory = graphFactory;
            _logger = logger;
        }

        public async Task<string> CreateChannelMeetingAsync(string teamId, string channelId, string subject, string content, DateTime start, DateTime end)
        {
            try
            {
                var graphClient = await _graphFactory.CreateClientAsync();

                // 1. Create the Event in the Group Calendar (This ensures SharePoint storage for recordings)
                var newEvent = new Event
                {
                    Subject = subject,
                    Body = new ItemBody
                    {
                        ContentType = BodyType.Html,
                        Content = content
                    },
                    Start = new DateTimeTimeZone
                    {
                        DateTime = start.ToString("o"),
                        TimeZone = "UTC"
                    },
                    End = new DateTimeTimeZone
                    {
                        DateTime = end.ToString("o"),
                        TimeZone = "UTC"
                    },
                    IsOnlineMeeting = true,
                    OnlineMeetingProvider = OnlineMeetingProviderType.TeamsForBusiness
                };

                // POST to /groups/{teamId}/events
                var createdEvent = await graphClient.Groups[teamId].Events.PostAsync(newEvent);
                
                // 2. Post a message to the channel to ensure visibility in the 'Posts' tab
                // This simulates the behavior of 'Schedule a meeting' from the channel.
                var joinUrl = createdEvent.OnlineMeeting?.JoinUrl;
                if (!string.IsNullOrEmpty(joinUrl))
                {
                    var chatMessage = new ChatMessage
                    {
                        Body = new ItemBody
                        {
                            ContentType = BodyType.Html,
                            Content = $"<p>Se ha programado una nueva sesión de clase: <b>{subject}</b></p><p><a href='{joinUrl}'>Unirse a la reunión de Microsoft Teams</a></p>"
                        }
                    };
                    await graphClient.Teams[teamId].Channels[channelId].Messages.PostAsync(chatMessage);
                }

                _logger.LogInformation($"Created channel meeting {createdEvent.Id} for Team {teamId} and posted notification to channel {channelId}");
                return createdEvent.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating channel meeting in team {teamId}");
                throw;
            }
        }

        public async Task DeleteMeetingAsync(string teamId, string eventId)
        {
            try
            {
                var graphClient = await _graphFactory.CreateClientAsync();
                await graphClient.Groups[teamId].Events[eventId].DeleteAsync();
                _logger.LogInformation($"Deleted meeting {eventId} from Team {teamId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting meeting {eventId} in team {teamId}");
                throw;
            }
        }

        public async Task<string> GetPrimaryChannelIdAsync(string teamId)
        {
            try
            {
                var graphClient = await _graphFactory.CreateClientAsync();
                var primaryChannel = await graphClient.Teams[teamId].PrimaryChannel.GetAsync();
                return primaryChannel?.Id ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching primary channel for team {teamId}");
                return string.Empty;
            }
        }
    }
}
