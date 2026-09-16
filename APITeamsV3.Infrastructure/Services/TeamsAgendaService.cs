using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Infrastructure.Services
{
    public class TeamsAgendaService : ITeamsAgendaService
    {
        private const int PrimaryChannelMaxAttempts = 12;
        private const int GroupCalendarReadyMaxAttempts = 12;
        private const int CreateGroupEventMaxAttempts = 8;
        private const int PostChannelMessageMaxAttempts = 5;

        private readonly IGraphClientFactory _graphFactory;
        private readonly ITenantProvider _tenantProvider;
        private readonly ISmartDbContext _smartContext;
        private readonly ILogger<TeamsAgendaService> _logger;

        public TeamsAgendaService(
            IGraphClientFactory graphFactory,
            ITenantProvider tenantProvider,
            ISmartDbContext smartContext,
            ILogger<TeamsAgendaService> logger)
        {
            _graphFactory = graphFactory;
            _tenantProvider = tenantProvider;
            _smartContext = smartContext;
            _logger = logger;
        }

        public async Task<TeamsMeetingResult> CreateRecurringChannelMeetingAsync(TeamsMeetingRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var recurrenceDays = request.RecurrenceDays?
                    .Distinct()
                    .Select(ToGraphDayName)
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .Distinct()
                    .ToList() ?? new List<string>();

                if (recurrenceDays.Count == 0)
                {
                    recurrenceDays.Add(ToGraphDayName(request.FirstOccurrenceStart.DayOfWeek));
                }

                var startDate = request.FirstOccurrenceStart;
                var endDate = request.FirstOccurrenceEnd <= request.FirstOccurrenceStart
                    ? request.FirstOccurrenceStart.AddHours(1)
                    : request.FirstOccurrenceEnd;

                var recurrenceStart = request.RecurrenceStartDate.Date;
                var recurrenceEnd = request.RecurrenceEndDate.Date < recurrenceStart
                    ? recurrenceStart
                    : request.RecurrenceEndDate.Date;

                const int MaxAttendeesThreshold = 500;
                var attendeeEmailsList = request.RequiredAttendeeEmails?
                    .Where(email => !string.IsNullOrWhiteSpace(email))
                    .Select(email => email.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>();

                if (attendeeEmailsList.Count > MaxAttendeesThreshold)
                {
                    _logger.LogWarning(
                        "RequiredAttendeeEmails count ({Count}) exceeds threshold of {Threshold} for Team {TeamId}. Restricting attendees to presenters/teachers only to avoid Exchange timeout.",
                        attendeeEmailsList.Count,
                        MaxAttendeesThreshold,
                        request.TeamId);

                    var presenterSet = new HashSet<string>(request.PresenterEmails ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                    var filtered = attendeeEmailsList.Where(e => presenterSet.Contains(e)).ToList();
                    attendeeEmailsList = filtered.Count > 0 ? filtered : (request.PresenterEmails?.ToList() ?? new List<string>());
                }

                var requiredAttendees = attendeeEmailsList
                    .Select(email => new Attendee
                    {
                        EmailAddress = new EmailAddress { Address = email },
                        Type = AttendeeType.Required
                    })
                    .ToList();

                var timeZoneId = ResolveTimeZoneId();
                var graphClient = await CreateAgendaGraphClientAsync();

                var daysOfWeekList = request.RecurrenceDays?
                    .Distinct()
                    .Select(ToGraphDayOfWeek)
                    .Where(d => d.HasValue)
                    .ToList() ?? new List<Microsoft.Graph.Models.DayOfWeekObject?>();

                if (daysOfWeekList.Count == 0)
                {
                    daysOfWeekList.Add(ToGraphDayOfWeek(request.FirstOccurrenceStart.DayOfWeek) ?? Microsoft.Graph.Models.DayOfWeekObject.Monday);
                }

                var newEvent = new Event
                {
                    Subject = request.Subject,
                    Body = new ItemBody
                    {
                        ContentType = BodyType.Html,
                        Content = request.HtmlContent
                    },
                    Start = new DateTimeTimeZone
                    {
                        DateTime = startDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                        TimeZone = timeZoneId
                    },
                    End = new DateTimeTimeZone
                    {
                        DateTime = endDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                        TimeZone = timeZoneId
                    },
                    Recurrence = new PatternedRecurrence
                    {
                        Pattern = new RecurrencePattern
                        {
                            Type = RecurrencePatternType.Weekly,
                            Interval = 1,
                            DaysOfWeek = daysOfWeekList
                        },
                        Range = new RecurrenceRange
                        {
                            Type = RecurrenceRangeType.EndDate,
                            StartDate = new Date(recurrenceStart.Year, recurrenceStart.Month, recurrenceStart.Day),
                            EndDate = new Date(recurrenceEnd.Year, recurrenceEnd.Month, recurrenceEnd.Day)
                        }
                    },
                    Attendees = requiredAttendees,
                    IsOnlineMeeting = true,
                    OnlineMeetingProvider = OnlineMeetingProviderType.TeamsForBusiness,
                    Location = new Location
                    {
                        DisplayName = "Reunión de Microsoft Teams",
                        LocationType = LocationType.Default
                    }
                };

                var createdEvent = await CreateGroupEventWithRetryAsync(
                    graphClient,
                    request.TeamId,
                    newEvent,
                    request.PresenterEmails,
                    cancellationToken);

                var joinUrl = createdEvent?.OnlineMeeting?.JoinUrl
                    ?? createdEvent?.OnlineMeetingUrl
                    ?? createdEvent?.WebLink
                    ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(joinUrl))
                {
                    await TryPromotePresentersAsync(
                        graphClient,
                        joinUrl,
                        request.PresenterEmails,
                        cancellationToken);

                    try
                    {
                        var chatMessage = new ChatMessage
                        {
                            Body = new ItemBody
                            {
                                ContentType = BodyType.Html,
                                Content = $"<p>Se ha programado una nueva sesión de clase: <b>{request.Subject}</b></p><p><a href='{joinUrl}'>Unirse a la reunión de Microsoft Teams</a></p>"
                            }
                        };

                        await PostChannelMessageWithRetryAsync(
                            graphClient,
                            request.TeamId,
                            request.ChannelId,
                            chatMessage,
                            cancellationToken);
                    }
                    catch (Exception msgEx)
                    {
                        _logger.LogWarning(msgEx, "Could not post channel message announcement for Team {TeamId}, Channel {ChannelId}. Agenda was created successfully.", request.TeamId, request.ChannelId);
                    }
                }

                _logger.LogInformation(
                    "Created recurring channel meeting {EventId} for Team {TeamId} in channel {ChannelId}",
                    createdEvent?.Id,
                    request.TeamId,
                    request.ChannelId);

                return new TeamsMeetingResult
                {
                    EventId = createdEvent?.Id ?? string.Empty,
                    JoinUrl = joinUrl
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating recurring channel meeting in team {TeamId}", request.TeamId);
                throw;
            }
        }

        public async Task<TeamsMeetingResult> UpdateMeetingAsync(TeamsMeetingUpdateRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.TeamId))
                {
                    throw new InvalidOperationException("TeamId is required to update a meeting.");
                }

                if (string.IsNullOrWhiteSpace(request.EventId))
                {
                    throw new InvalidOperationException("EventId is required to update a meeting.");
                }

                var graphClient = await CreateAgendaGraphClientAsync();
                var timeZoneId = ResolveTimeZoneId();
                const int MaxAttendeesThreshold = 500;
                var requestedEmails = (request.RequiredAttendeeEmails ?? Array.Empty<string>())
                    .Where(email => !string.IsNullOrWhiteSpace(email))
                    .Select(email => email.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (requestedEmails.Count > MaxAttendeesThreshold)
                {
                    _logger.LogWarning(
                        "RequiredAttendeeEmails count ({Count}) exceeds threshold of {Threshold} for Event {EventId}. Restricting attendees to presenters/teachers only to avoid Exchange timeout.",
                        requestedEmails.Count,
                        MaxAttendeesThreshold,
                        request.EventId);

                    var presenterSet = new HashSet<string>(request.PresenterEmails ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                    var filtered = requestedEmails.Where(e => presenterSet.Contains(e)).ToList();
                    requestedEmails = filtered.Count > 0 ? filtered : (request.PresenterEmails?.ToList() ?? new List<string>());
                }

                var attendees = requestedEmails
                    .Select(email => new Attendee
                    {
                        EmailAddress = new EmailAddress { Address = email },
                        Type = AttendeeType.Required
                    })
                    .ToList();

                var groupEmail = string.Empty;
                try
                {
                    var group = await graphClient.Groups[request.TeamId].GetAsync(
                        requestConfiguration =>
                        {
                            requestConfiguration.QueryParameters.Select = new[] { "id", "mail" };
                        },
                        cancellationToken: cancellationToken);
                    groupEmail = group?.Mail;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load Group {TeamId} mail to filter attendees.", request.TeamId);
                }

                Event? currentEvent = null;
                try
                {
                    currentEvent = await graphClient.Groups[request.TeamId].Events[request.EventId].GetAsync(
                        requestConfiguration =>
                        {
                            requestConfiguration.Headers.Add("Prefer", $"outlook.timezone=\"{timeZoneId}\"");
                        },
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Could not load meeting {EventId} in Team {TeamId} to check for changes before update.",
                        request.EventId,
                        request.TeamId);
                }

                var hasChanges = false;

                if (currentEvent == null)
                {
                    hasChanges = true;
                }
                else
                {
                    if (request.Start.HasValue)
                    {
                        var currentStartUtc = ToUtcDateTime(currentEvent.Start?.DateTime, currentEvent.Start?.TimeZone);
                        var requestStartUtc = ToUtcDateTime(request.Start, timeZoneId);

                        if (currentStartUtc.HasValue && requestStartUtc.HasValue)
                        {
                            if (currentStartUtc.Value.TimeOfDay != requestStartUtc.Value.TimeOfDay ||
                                requestStartUtc.Value.Date > currentStartUtc.Value.Date)
                            {
                                hasChanges = true;
                            }
                        }
                        else
                        {
                            hasChanges = true;
                        }
                    }

                    if (request.End.HasValue && !hasChanges)
                    {
                        var currentEndUtc = ToUtcDateTime(currentEvent.End?.DateTime, currentEvent.End?.TimeZone);
                        var requestEndUtc = ToUtcDateTime(request.End, timeZoneId);

                        if (currentEndUtc.HasValue && requestEndUtc.HasValue)
                        {
                            if (currentEndUtc.Value.TimeOfDay != requestEndUtc.Value.TimeOfDay ||
                                requestEndUtc.Value.Date > currentEndUtc.Value.Date)
                            {
                                hasChanges = true;
                            }
                        }
                        else
                        {
                            hasChanges = true;
                        }
                    }

                    if (!hasChanges)
                    {
                        var organizerEmail = currentEvent.Organizer?.EmailAddress?.Address;

                        var filteredCurrentEmails = currentEvent.Attendees?
                            .Where(a => string.IsNullOrWhiteSpace(organizerEmail) || !string.Equals(a.EmailAddress?.Address, organizerEmail, StringComparison.OrdinalIgnoreCase))
                            .Select(a => a.EmailAddress?.Address)
                            .Where(e => !string.IsNullOrWhiteSpace(e))
                            .Select(e => e!.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList() ?? new List<string>();

                        var added = requestedEmails.Except(filteredCurrentEmails, StringComparer.OrdinalIgnoreCase).ToList();
                        var removed = filteredCurrentEmails.Except(requestedEmails, StringComparer.OrdinalIgnoreCase)
                            .Where(e => 
                                !e.EndsWith(".teams.ms", StringComparison.OrdinalIgnoreCase) &&
                                (string.IsNullOrWhiteSpace(groupEmail) || !string.Equals(e, groupEmail, StringComparison.OrdinalIgnoreCase))
                            )
                            .ToList();

                        if (added.Count > 0 || removed.Count > 0)
                        {
                            hasChanges = true;
                            _logger.LogInformation(
                                "Meeting {EventId} attendees changed. Added: {Added}, Removed: {Removed}",
                                request.EventId,
                                string.Join(", ", added),
                                string.Join(", ", removed));
                        }
                    }
                }

                if (hasChanges)
                {
                    var patch = new Event();
                    if (request.Start.HasValue)
                    {
                        patch.Start = new DateTimeTimeZone
                        {
                            DateTime = request.Start.Value.ToString("yyyy-MM-ddTHH:mm:ss"),
                            TimeZone = timeZoneId
                        };
                    }

                    if (request.End.HasValue)
                    {
                        patch.End = new DateTimeTimeZone
                        {
                            DateTime = request.End.Value.ToString("yyyy-MM-ddTHH:mm:ss"),
                            TimeZone = timeZoneId
                        };
                    }

                    patch.Attendees = attendees;

                    await graphClient.Groups[request.TeamId].Events[request.EventId].PatchAsync(
                        patch,
                        cancellationToken: cancellationToken);
                }
                else
                {
                    _logger.LogInformation(
                        "No changes detected for meeting {EventId} in Team {TeamId}, skipping patch.",
                        request.EventId,
                        request.TeamId);
                }

                var joinUrl = request.JoinUrl;
                if (string.IsNullOrWhiteSpace(joinUrl))
                {
                    if (currentEvent == null && hasChanges)
                    {
                        try
                        {
                            currentEvent = await graphClient.Groups[request.TeamId].Events[request.EventId].GetAsync(
                                requestConfiguration =>
                                {
                                    requestConfiguration.Headers.Add("Prefer", $"outlook.timezone=\"{timeZoneId}\"");
                                },
                                cancellationToken: cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(
                                ex,
                                "Could not reload meeting {EventId} in Team {TeamId} after update to resolve JoinUrl.",
                                request.EventId,
                                request.TeamId);
                        }
                    }

                    joinUrl = currentEvent?.OnlineMeeting?.JoinUrl
                        ?? currentEvent?.OnlineMeetingUrl
                        ?? currentEvent?.WebLink
                        ?? string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(joinUrl))
                {
                    await TryPromotePresentersAsync(
                        graphClient,
                        joinUrl,
                        request.PresenterEmails,
                        cancellationToken);
                }

                _logger.LogInformation(
                    "Updated meeting {EventId} for Team {TeamId}.",
                    request.EventId,
                    request.TeamId);

                return new TeamsMeetingResult
                {
                    EventId = request.EventId,
                    JoinUrl = joinUrl ?? string.Empty
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error updating meeting {EventId} in team {TeamId}",
                    request.EventId,
                    request.TeamId);
                throw;
            }
        }

        private async Task TryPromotePresentersAsync(
            GraphServiceClient graphClient,
            string joinUrl,
            IReadOnlyCollection<string> presenterEmails,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(joinUrl))
            {
                return;
            }

            try
            {
                var onlineMeeting = await FindOnlineMeetingByJoinUrlAsync(graphClient, joinUrl, cancellationToken);
                if (onlineMeeting?.Id == null)
                {
                    _logger.LogWarning("No onlineMeeting resource was found by JoinWebUrl to promote presenters.");
                    return;
                }

                var attendees = onlineMeeting.Participants?.Attendees?.ToList() ?? new List<MeetingParticipantInfo>();
                var teacherUserIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var changed = false;

                foreach (var email in (presenterEmails ?? Array.Empty<string>())
                             .Where(e => !string.IsNullOrWhiteSpace(e))
                             .Select(e => e.Trim())
                             .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var user = await graphClient.Users[email].GetAsync(
                        requestConfiguration =>
                        {
                            requestConfiguration.QueryParameters.Select = new[] { "id", "mail", "userPrincipalName", "displayName" };
                        },
                        cancellationToken);

                    if (string.IsNullOrWhiteSpace(user?.Id))
                    {
                        continue;
                    }

                    teacherUserIds.Add(user.Id);

                    var existing = attendees.FirstOrDefault(a =>
                        string.Equals(a.Identity?.User?.Id, user.Id, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a.Identity?.User?.DisplayName, user.DisplayName, StringComparison.OrdinalIgnoreCase));

                    if (existing == null)
                    {
                        attendees.Add(new MeetingParticipantInfo
                        {
                            Role = OnlineMeetingRole.Coorganizer,
                            Upn = user.UserPrincipalName ?? user.Mail ?? email,
                            Identity = new IdentitySet
                            {
                                User = new Identity
                                {
                                    Id = user.Id,
                                    DisplayName = user.DisplayName
                                }
                            }
                        });
                        changed = true;
                    }
                    else if (existing.Role != OnlineMeetingRole.Coorganizer || string.IsNullOrWhiteSpace(existing.Upn))
                    {
                        existing.Role = OnlineMeetingRole.Coorganizer;
                        existing.Upn = user.UserPrincipalName ?? user.Mail ?? email;
                        changed = true;
                    }
                }

                if (!changed)
                {
                    var needsSettingsPatch = onlineMeeting.AllowedPresenters != OnlineMeetingPresenters.RoleIsPresenter ||
                                             onlineMeeting.AllowRecording != true;

                    var organizerId = ExtractOidFromJoinUrl(joinUrl);
                    if (needsSettingsPatch)
                    {
                        await PatchOnlineMeetingWithFallbackAsync(
                            graphClient,
                            onlineMeeting.Id,
                            organizerId,
                            new OnlineMeeting
                            {
                                AllowedPresenters = OnlineMeetingPresenters.RoleIsPresenter,
                                AllowRecording = true
                            },
                            cancellationToken);

                        _logger.LogInformation(
                            "Set allowedPresenters=roleIsPresenter and allowRecording=true for onlineMeeting {MeetingId} (no attendee role changes applied).",
                            onlineMeeting.Id);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "OnlineMeeting {MeetingId} already has correct presenters and settings. No patch required.",
                            onlineMeeting.Id);
                    }
                    return;
                }

                var targetOrganizerId = ExtractOidFromJoinUrl(joinUrl);
                try
                {
                    await PatchOnlineMeetingWithFallbackAsync(
                        graphClient,
                        onlineMeeting.Id,
                        targetOrganizerId,
                        new OnlineMeeting
                        {
                            AllowedPresenters = OnlineMeetingPresenters.RoleIsPresenter,
                            AllowRecording = true,
                            Participants = new MeetingParticipants
                            {
                                Attendees = attendees
                            }
                        },
                        cancellationToken);

                    _logger.LogInformation(
                        "Promoted configured teachers as co-organizers and set allowedPresenters=roleIsPresenter/allowRecording=true for onlineMeeting {MeetingId}.",
                        onlineMeeting.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Could not assign co-organizer role for meeting {MeetingId}. Falling back to presenter role.",
                        onlineMeeting.Id);

                    foreach (var attendee in attendees)
                    {
                        var attendeeUserId = attendee.Identity?.User?.Id;
                        if (!string.IsNullOrWhiteSpace(attendeeUserId) &&
                            teacherUserIds.Contains(attendeeUserId) &&
                            attendee.Role != OnlineMeetingRole.Presenter)
                        {
                            attendee.Role = OnlineMeetingRole.Presenter;
                        }
                    }

                    await PatchOnlineMeetingWithFallbackAsync(
                        graphClient,
                        onlineMeeting.Id,
                        targetOrganizerId,
                        new OnlineMeeting
                        {
                            AllowedPresenters = OnlineMeetingPresenters.RoleIsPresenter,
                            AllowRecording = true,
                            Participants = new MeetingParticipants
                            {
                                Attendees = attendees
                            }
                        },
                        cancellationToken);

                    _logger.LogInformation(
                        "Promoted configured teachers as presenters (co-organizer fallback) and set allowedPresenters=roleIsPresenter/allowRecording=true for onlineMeeting {MeetingId}.",
                        onlineMeeting.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not promote teachers as presenters/co-organizers for meeting.");
            }
        }

        private static string ExtractOidFromJoinUrl(string joinUrl)
        {
            try
            {
                var decoded = System.Uri.UnescapeDataString(joinUrl);
                int oidKeyIdx = decoded.IndexOf("\"Oid\"", StringComparison.OrdinalIgnoreCase);
                if (oidKeyIdx == -1) return "";

                int colonIdx = decoded.IndexOf(":", oidKeyIdx);
                if (colonIdx == -1) return "";

                int startQuote = decoded.IndexOf("\"", colonIdx);
                if (startQuote == -1) return "";

                int endQuote = decoded.IndexOf("\"", startQuote + 1);
                if (endQuote == -1) return "";

                return decoded.Substring(startQuote + 1, endQuote - startQuote - 1);
            }
            catch
            {
                return "";
            }
        }

        private async Task<OnlineMeeting?> FindOnlineMeetingByJoinUrlAsync(
            GraphServiceClient graphClient,
            string joinUrl,
            CancellationToken cancellationToken)
        {
            var escapedJoinUrl = joinUrl.Replace("'", "''");
            var organizerId = ExtractOidFromJoinUrl(joinUrl);

            if (!string.IsNullOrWhiteSpace(organizerId))
            {
                try
                {
                    var meetingResponse = await graphClient.Users[organizerId].OnlineMeetings.GetAsync(
                        requestConfiguration =>
                        {
                            requestConfiguration.QueryParameters.Filter = $"JoinWebUrl eq '{escapedJoinUrl}'";
                        },
                        cancellationToken);

                    var meeting = meetingResponse?.Value?.FirstOrDefault();
                    if (meeting?.Id != null)
                    {
                        return meeting;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to query meeting by JoinWebUrl via /users/{OrganizerId}/onlineMeetings.", organizerId);
                }
            }

            try
            {
                var meetingResponse = await graphClient.Me.OnlineMeetings.GetAsync(
                    requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Filter = $"JoinWebUrl eq '{escapedJoinUrl}'";
                    },
                    cancellationToken);

                var meeting = meetingResponse?.Value?.FirstOrDefault();
                if (meeting?.Id != null)
                {
                    return meeting;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query meeting by JoinWebUrl via /me/onlineMeetings. Trying fallback.");
            }

            try
            {
                var meetingResponse = await graphClient.Communications.OnlineMeetings.GetAsync(
                    requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Filter = $"JoinWebUrl eq '{escapedJoinUrl}'";
                    },
                    cancellationToken);

                return meetingResponse?.Value?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query meeting by JoinWebUrl via /communications/onlineMeetings.");
                return null;
            }
        }

        private async Task PatchOnlineMeetingWithFallbackAsync(
            GraphServiceClient graphClient,
            string meetingId,
            string? organizerId,
            OnlineMeeting patch,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(organizerId))
            {
                try
                {
                    await graphClient.Users[organizerId].OnlineMeetings[meetingId].PatchAsync(
                        patch,
                        cancellationToken: cancellationToken);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to patch meeting {MeetingId} via /users/{OrganizerId}/onlineMeetings. Trying fallbacks.",
                        meetingId,
                        organizerId);
                }
            }

            try
            {
                await graphClient.Me.OnlineMeetings[meetingId].PatchAsync(
                    patch,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to patch meeting {MeetingId} via /me/onlineMeetings. Trying fallback /communications/onlineMeetings.",
                    meetingId);

                await graphClient.Communications.OnlineMeetings[meetingId].PatchAsync(
                    patch,
                    cancellationToken: cancellationToken);
            }
        }

        public async Task DeleteMeetingAsync(string teamId, string eventId)
        {
            if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(eventId))
            {
                return;
            }

            try
            {
                var graphClient = await CreateAgendaGraphClientAsync();
                await graphClient.Groups[teamId].Events[eventId].DeleteAsync();
                _logger.LogInformation("Deleted meeting {EventId} from Team {TeamId}", eventId, teamId);
            }
            catch (Exception ex)
            {
                var msg = ex.ToString().ToLowerInvariant();
                if (msg.Contains("itemnotfound") || msg.Contains("not found") || msg.Contains("404"))
                {
                    _logger.LogInformation("Meeting {EventId} was already removed from Team {TeamId}.", eventId, teamId);
                    return;
                }

                _logger.LogWarning(ex, "Could not delete meeting {EventId} via delegated client. Retrying via App-Only client...", eventId, teamId);
                try
                {
                    var appClient = await _graphFactory.CreateClientAsync();
                    await appClient.Groups[teamId].Events[eventId].DeleteAsync();
                    _logger.LogInformation("Deleted meeting {EventId} from Team {TeamId} via App-Only client.", eventId, teamId);
                }
                catch (Exception appEx)
                {
                    var appMsg = appEx.ToString().ToLowerInvariant();
                    if (appMsg.Contains("itemnotfound") || appMsg.Contains("not found") || appMsg.Contains("404"))
                    {
                        _logger.LogInformation("Meeting {EventId} was already removed from Team {TeamId}.", eventId, teamId);
                        return;
                    }
                    _logger.LogWarning(appEx, "App-Only client also failed to delete meeting {EventId} from Team {TeamId}.", eventId, teamId);
                    throw;
                }
            }
        }

        public async Task AddCoorganizerAsync(string joinUrl, IReadOnlyCollection<string> teacherEmails, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(joinUrl) || teacherEmails == null || teacherEmails.Count == 0)
            {
                return;
            }

            var graphClient = await CreateAgendaGraphClientAsync();
            await TryPromotePresentersAsync(graphClient, joinUrl, teacherEmails, cancellationToken);
        }

        public async Task EnsureTeacherCoorganizerForSectionAsync(int idSeccion, CancellationToken cancellationToken = default)
        {
            try
            {
                var seccion = await _smartContext.Set<Seccion>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.IdSeccion == idSeccion, cancellationToken);

                if (seccion == null) return;

                var seccionHorarios = await _smartContext.Set<SeccionHorario>()
                    .AsNoTracking()
                    .Where(sh => sh.IdSeccion == idSeccion && !string.IsNullOrEmpty(sh.UrlClaseVirtual))
                    .ToListAsync(cancellationToken);

                if (seccionHorarios.Count == 0) return;

                var teacherEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(seccion.EmailFacilitador))
                {
                    teacherEmails.Add(seccion.EmailFacilitador.Trim());
                }

                try
                {
                    var horariosEmails = await _smartContext.Database
                        .SqlQueryRaw<string>("SELECT DISTINCT CorreoFacilitador FROM TeamsHorarios WITH (NOLOCK) WHERE IdCurso = {0} AND CorreoFacilitador IS NOT NULL AND CorreoFacilitador <> ''", idSeccion)
                        .ToListAsync(cancellationToken);

                    foreach (var email in horariosEmails)
                    {
                        if (!string.IsNullOrWhiteSpace(email))
                        {
                            teacherEmails.Add(email.Trim());
                        }
                    }
                }
                catch
                {
                    // Fallback to seccion.EmailFacilitador if TeamsHorarios query is unavailable
                }

                if (teacherEmails.Count == 0) return;

                // 1. Sync Team Group Owners & Calendar Event Attendees if Team exists
                var team = await _smartContext.Set<TeamEntity>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.IdSeccionSmart == idSeccion && t.EstadoTeam == "A", cancellationToken);

                if (team != null && !string.IsNullOrWhiteSpace(team.IdTeamsGroup))
                {
                    try
                    {
                        var graphClient = await _graphFactory.CreateClientAsync();

                        // Add teacher(s) as Team Group Owners
                        foreach (var email in teacherEmails)
                        {
                            try
                            {
                                var user = await graphClient.Users[email].GetAsync(cancellationToken: cancellationToken);
                                if (user?.Id != null)
                                {
                                    var ownerRef = new ReferenceCreate
                                    {
                                        OdataId = $"https://graph.microsoft.com/v1.0/users/{user.Id}"
                                    };
                                    await graphClient.Groups[team.IdTeamsGroup].Owners.Ref.PostAsync(ownerRef, cancellationToken: cancellationToken);
                                }
                            }
                            catch
                            {
                                // User may already be an owner or member
                            }
                        }

                        // Add teacher(s) as Required Attendees in Calendar Events
                        foreach (var sh in seccionHorarios)
                        {
                            if (!string.IsNullOrWhiteSpace(sh.IdEvento))
                            {
                                try
                                {
                                    var currentEvent = await graphClient.Groups[team.IdTeamsGroup].Events[sh.IdEvento].GetAsync(cancellationToken: cancellationToken);
                                    if (currentEvent != null)
                                    {
                                        var attendeesList = currentEvent.Attendees?.ToList() ?? new List<Attendee>();
                                        bool attendeeAdded = false;

                                        foreach (var email in teacherEmails)
                                        {
                                            if (!attendeesList.Any(a => string.Equals(a.EmailAddress?.Address, email, StringComparison.OrdinalIgnoreCase)))
                                            {
                                                attendeesList.Add(new Attendee
                                                {
                                                    EmailAddress = new EmailAddress { Address = email },
                                                    Type = AttendeeType.Required
                                                });
                                                attendeeAdded = true;
                                            }
                                        }

                                        if (attendeeAdded)
                                        {
                                            await graphClient.Groups[team.IdTeamsGroup].Events[sh.IdEvento].PatchAsync(new Event
                                            {
                                                Attendees = attendeesList
                                            }, cancellationToken: cancellationToken);
                                        }
                                    }
                                }
                                catch
                                {
                                    // Ignore individual event patch failures
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to sync Group Owners or Calendar Event Attendees for section {SectionId}.", idSeccion);
                    }
                }

                // 2. Promote Co-Organizers in OnlineMeeting
                var joinUrls = seccionHorarios
                    .Select(sh => sh.UrlClaseVirtual!)
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var joinUrl in joinUrls)
                {
                    await AddCoorganizerAsync(joinUrl, teacherEmails.ToList(), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring teacher co-organizer for section {SectionId}.", idSeccion);
            }
        }

        public async Task<string> GetPrimaryChannelIdAsync(string teamId, CancellationToken cancellationToken = default)
        {
            // 1. Try App-Only client (Client Credentials with Group.ReadWrite.All) first for highest reliability
            try
            {
                var appGraphClient = await _graphFactory.CreateClientAsync();

                try
                {
                    var primaryChannel = await appGraphClient.Teams[teamId].PrimaryChannel.GetAsync(cancellationToken: cancellationToken);
                    if (!string.IsNullOrWhiteSpace(primaryChannel?.Id))
                    {
                        return primaryChannel.Id;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "App-Only PrimaryChannel.GetAsync failed for Team {TeamId}. Trying channels list.", teamId);
                }

                try
                {
                    var channelsResponse = await appGraphClient.Teams[teamId].Channels.GetAsync(cancellationToken: cancellationToken);
                    var channels = channelsResponse?.Value ?? new List<Channel>();

                    var byName = channels.FirstOrDefault(c =>
                        string.Equals(c.DisplayName, "General", StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrWhiteSpace(byName?.Id))
                    {
                        return byName.Id;
                    }

                    var byStandardType = channels.FirstOrDefault(c => c.MembershipType == ChannelMembershipType.Standard);
                    if (!string.IsNullOrWhiteSpace(byStandardType?.Id))
                    {
                        return byStandardType.Id;
                    }

                    var first = channels.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(first?.Id))
                    {
                        return first.Id;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "App-Only Channels.GetAsync failed for Team {TeamId}.", teamId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "App-Only Graph client creation failed for Team {TeamId}.", teamId);
            }

            // 2. Fallback to Delegated client
            try
            {
                var delegatedClient = await CreateAgendaGraphClientAsync();

                try
                {
                    var primaryChannel = await delegatedClient.Teams[teamId].PrimaryChannel.GetAsync(cancellationToken: cancellationToken);
                    if (!string.IsNullOrWhiteSpace(primaryChannel?.Id))
                    {
                        return primaryChannel.Id;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Delegated PrimaryChannel.GetAsync failed for Team {TeamId}.", teamId);
                }

                try
                {
                    var channelsResponse = await delegatedClient.Teams[teamId].Channels.GetAsync(cancellationToken: cancellationToken);
                    var channels = channelsResponse?.Value ?? new List<Channel>();

                    var byName = channels.FirstOrDefault(c =>
                        string.Equals(c.DisplayName, "General", StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrWhiteSpace(byName?.Id))
                    {
                        return byName.Id;
                    }

                    var first = channels.FirstOrDefault();
                    return first?.Id ?? string.Empty;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Delegated Channels.GetAsync failed for Team {TeamId}", teamId);
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delegated Graph client creation failed for Team {TeamId}", teamId);
                return string.Empty;
            }
        }

        private async Task<Event?> CreateGroupEventWithRetryAsync(
            GraphServiceClient graphClient,
            string teamId,
            Event newEvent,
            IEnumerable<string>? presenterEmails,
            CancellationToken cancellationToken)
        {
            await EnsureGroupCalendarReadyAsync(graphClient, teamId, cancellationToken);
            await EnsureTechnicalAccountIsGroupOwnerAsync(teamId, cancellationToken);

            Exception? lastException = null;

            for (var attempt = 1; attempt <= CreateGroupEventMaxAttempts; attempt++)
            {
                try
                {
                    return await graphClient.Groups[teamId].Events.PostAsync(newEvent, cancellationToken: cancellationToken);
                }
                catch (Exception ex) when (IsAccessDeniedError(ex))
                {
                    _logger.LogWarning(
                        ex,
                        "Access denied for group calendar {TeamId}. Retrying with delegated client for technical account (Attempt {Attempt}/{MaxAttempts})...",
                        teamId,
                        attempt,
                        CreateGroupEventMaxAttempts);
                    
                    try
                    {
                        var delegatedClient = await _graphFactory.CreateDelegatedClientAsync();
                        return await delegatedClient.Groups[teamId].Events.PostAsync(newEvent, cancellationToken: cancellationToken);
                    }
                    catch (Exception delEx) when (IsTransientGroupProvisioningError(delEx))
                    {
                        lastException = delEx;
                    }
                    catch (Exception delEx)
                    {
                        _logger.LogError(delEx, "Delegated client failed to post event to group calendar {TeamId}.", teamId);
                        throw;
                    }
                }
                catch (Exception ex) when (IsTransientGroupProvisioningError(ex))
                {
                    lastException = ex;
                }

                if (lastException != null)
                {
                    if (attempt == CreateGroupEventMaxAttempts)
                    {
                        break;
                    }

                    var delay = GetRetryDelay(attempt);
                    _logger.LogWarning(
                        lastException,
                        "Group calendar is not ready yet for Team/Group {TeamId}. Attempt {Attempt}/{MaxAttempts}. Retrying in {Delay}s.",
                        teamId,
                        attempt,
                        CreateGroupEventMaxAttempts,
                        delay.TotalSeconds);

                    await Task.Delay(delay, cancellationToken);
                }
            }

            if (lastException != null)
            {
                throw lastException;
            }

            return await graphClient.Groups[teamId].Events.PostAsync(newEvent, cancellationToken: cancellationToken);
        }

        private async Task PostChannelMessageWithRetryAsync(
            GraphServiceClient graphClient,
            string teamId,
            string channelId,
            ChatMessage chatMessage,
            CancellationToken cancellationToken)
        {
            Exception? lastException = null;

            for (var attempt = 1; attempt <= PostChannelMessageMaxAttempts; attempt++)
            {
                try
                {
                    await graphClient.Teams[teamId].Channels[channelId].Messages.PostAsync(chatMessage, cancellationToken: cancellationToken);
                    return;
                }
                catch (Exception ex) when (IsTransientChannelProvisioningError(ex))
                {
                    lastException = ex;

                    if (attempt == PostChannelMessageMaxAttempts)
                    {
                        break;
                    }

                    var delay = GetRetryDelay(attempt);
                    _logger.LogWarning(
                        ex,
                        "Channel message post not ready for Team {TeamId}, Channel {ChannelId}. Attempt {Attempt}/{MaxAttempts}. Retrying in {Delay}s.",
                        teamId,
                        channelId,
                        attempt,
                        PostChannelMessageMaxAttempts,
                        delay.TotalSeconds);

                    await Task.Delay(delay, cancellationToken);
                }
            }

            if (lastException != null)
            {
                throw lastException;
            }
        }

        private async Task EnsureGroupCalendarReadyAsync(
            GraphServiceClient graphClient,
            string teamId,
            CancellationToken cancellationToken)
        {
            await EnsureTechnicalAccountIsGroupOwnerAsync(teamId, cancellationToken);

            Exception? lastTransient = null;

            for (var attempt = 1; attempt <= GroupCalendarReadyMaxAttempts; attempt++)
            {
                try
                {
                    await graphClient.Teams[teamId].GetAsync(cancellationToken: cancellationToken);

                    // Do not query /groups/{id}/calendar here because some tenants block this read
                    // even when event creation is allowed. Keep readiness check lightweight.
                    await graphClient.Groups[teamId].GetAsync(cancellationToken: cancellationToken);
                    return;
                }
                catch (Exception ex) when (IsAccessDeniedError(ex))
                {
                    _logger.LogWarning(
                        ex,
                        "Skipping group calendar readiness check for Team/Group {TeamId} due to access denied. Continuing with event creation.",
                        teamId);
                    return;
                }
                catch (Exception ex) when (IsTransientGroupProvisioningError(ex))
                {
                    lastTransient = ex;

                    if (attempt == GroupCalendarReadyMaxAttempts)
                    {
                        break;
                    }

                    var delay = GetRetryDelay(attempt);
                    _logger.LogWarning(
                        ex,
                        "Team/Group {TeamId} is still provisioning (calendar not ready). Attempt {Attempt}/{MaxAttempts}. Retrying in {Delay}s.",
                        teamId,
                        attempt,
                        GroupCalendarReadyMaxAttempts,
                        delay.TotalSeconds);

                    await Task.Delay(delay, cancellationToken);
                }
            }

            if (lastTransient != null)
            {
                throw lastTransient;
            }
        }

        private async Task EnsureTechnicalAccountIsGroupOwnerAsync(string teamId, CancellationToken cancellationToken)
        {
            try
            {
                var appClient = await _graphFactory.CreateClientAsync();
                var techAccountEmail = await GetTechnicalAccountEmailAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(techAccountEmail))
                {
                    _logger.LogWarning("No technical account email found in AplicativosTeams to ensure ownership for team {TeamId}.", teamId);
                    return;
                }

                var techUser = await appClient.Users[techAccountEmail].GetAsync(
                    requestConfiguration => requestConfiguration.QueryParameters.Select = ["id"],
                    cancellationToken: cancellationToken);

                if (!string.IsNullOrWhiteSpace(techUser?.Id))
                {
                    var body = new Microsoft.Graph.Models.ReferenceCreate
                    {
                        OdataId = $"https://graph.microsoft.com/v1.0/users/{techUser.Id}"
                    };

                    try
                    {
                        await appClient.Groups[teamId].Owners.Ref.PostAsync(body, cancellationToken: cancellationToken);
                        _logger.LogInformation("Added technical account {Email} as Owner of group {TeamId}.", techAccountEmail, teamId);
                    }
                    catch
                    {
                        // Already owner or conflict - ignore
                    }

                    try
                    {
                        await appClient.Groups[teamId].Members.Ref.PostAsync(body, cancellationToken: cancellationToken);
                        _logger.LogInformation("Added technical account {Email} as Member of group {TeamId}.", techAccountEmail, teamId);
                    }
                    catch
                    {
                        // Already member or conflict - ignore
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not ensure technical account as owner/member of group {TeamId}. Continuing.", teamId);
            }
        }

        private static bool IsAccessDeniedError(Exception ex)
        {
            var fullText = (ex.ToString() ?? string.Empty).ToLowerInvariant();
            var textMatch =
                fullText.Contains("access is denied") ||
                fullText.Contains("insufficient privileges") ||
                fullText.Contains("authorization_requestdenied") ||
                fullText.Contains("aadsts") ||
                fullText.Contains("invalid_grant") ||
                fullText.Contains("usernamepasswordcredential") ||
                fullText.Contains("multi-factor authentication") ||
                fullText.Contains("authenticationfailedexception");

            if (ex is ApiException apiException)
            {
                return apiException.ResponseStatusCode is 401 or 403 || textMatch;
            }

            return textMatch;
        }

        private static bool IsTransientGroupProvisioningError(Exception ex)
        {
            var message = (ex.Message ?? string.Empty).ToLowerInvariant();
            var fullText = (ex.ToString() ?? string.Empty).ToLowerInvariant();

            var textMatch =
                (message.Contains("requested group") && message.Contains("invalid")) ||
                (message.Contains("group") && message.Contains("invalid")) ||
                (message.Contains("resource") && message.Contains("not found")) ||
                message.Contains("failed to execute msgraph backend request") ||
                message.Contains("does not exist") ||
                message.Contains("mailbox") ||
                message.Contains("not ready") ||
                (fullText.Contains("requested group") && fullText.Contains("invalid"));

            if (textMatch)
            {
                return true;
            }

            if (ex is ApiException apiException)
            {
                return apiException.ResponseStatusCode is 400 or 404 or 409 or 429 or 503;
            }

            return false;
        }

        private static bool IsTransientChannelProvisioningError(Exception ex)
        {
            var message = (ex.Message ?? string.Empty).ToLowerInvariant();
            var textMatch =
                (message.Contains("resource") && message.Contains("not found")) ||
                message.Contains("does not exist") ||
                message.Contains("not ready");

            if (ex is ApiException apiException)
            {
                return apiException.ResponseStatusCode is 400 or 404 or 409 or 429 or 503
                    && textMatch;
            }

            return textMatch;
        }

        private static TimeSpan GetRetryDelay(int attempt)
        {
            var seconds = Math.Min(5 * attempt, 45);
            return TimeSpan.FromSeconds(seconds);
        }

        private async Task<GraphServiceClient> CreateAgendaGraphClientAsync()
        {
            try
            {
                return await _graphFactory.CreateDelegatedClientAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo crear cliente Graph delegado para agenda (posible MFA o credenciales). Usando App-Only fallback...");
                return await _graphFactory.CreateClientAsync();
            }
        }

        private string ResolveTimeZoneId()
        {
            try
            {
                var timeZone = _tenantProvider.GetCurrentTenant().TimeZoneId;
                if (!string.IsNullOrWhiteSpace(timeZone))
                {
                    return timeZone;
                }
            }
            catch
            {
                // If no tenant context is available, use Lima timezone fallback.
            }

            return "SA Pacific Standard Time";
        }
        private DateTime? ToUtcDateTime(string? dateTimeStr, string? timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(dateTimeStr))
            {
                return null;
            }

            if (!DateTime.TryParse(dateTimeStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedDateTime))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(timeZoneId) || string.Equals(timeZoneId, "UTC", StringComparison.OrdinalIgnoreCase))
            {
                return DateTime.SpecifyKind(parsedDateTime, DateTimeKind.Utc);
            }

            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                var unspecifiedDateTime = DateTime.SpecifyKind(parsedDateTime, DateTimeKind.Unspecified);
                return TimeZoneInfo.ConvertTimeToUtc(unspecifiedDateTime, tz);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not resolve timezone {TimeZoneId} for date conversion. Treating as UTC.", timeZoneId);
                return DateTime.SpecifyKind(parsedDateTime, DateTimeKind.Utc);
            }
        }

        private DateTime? ToUtcDateTime(DateTime? localDateTime, string? timeZoneId)
        {
            if (!localDateTime.HasValue)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(timeZoneId) || string.Equals(timeZoneId, "UTC", StringComparison.OrdinalIgnoreCase))
            {
                return DateTime.SpecifyKind(localDateTime.Value, DateTimeKind.Utc);
            }

            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                var unspecifiedDateTime = DateTime.SpecifyKind(localDateTime.Value, DateTimeKind.Unspecified);
                return TimeZoneInfo.ConvertTimeToUtc(unspecifiedDateTime, tz);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not resolve timezone {TimeZoneId} for request datetime conversion. Treating as UTC.", timeZoneId);
                return DateTime.SpecifyKind(localDateTime.Value, DateTimeKind.Utc);
            }
        }

        private static Microsoft.Graph.Models.DayOfWeekObject? ToGraphDayOfWeek(System.DayOfWeek day) =>
            day switch
            {
                System.DayOfWeek.Sunday => Microsoft.Graph.Models.DayOfWeekObject.Sunday,
                System.DayOfWeek.Monday => Microsoft.Graph.Models.DayOfWeekObject.Monday,
                System.DayOfWeek.Tuesday => Microsoft.Graph.Models.DayOfWeekObject.Tuesday,
                System.DayOfWeek.Wednesday => Microsoft.Graph.Models.DayOfWeekObject.Wednesday,
                System.DayOfWeek.Thursday => Microsoft.Graph.Models.DayOfWeekObject.Thursday,
                System.DayOfWeek.Friday => Microsoft.Graph.Models.DayOfWeekObject.Friday,
                System.DayOfWeek.Saturday => Microsoft.Graph.Models.DayOfWeekObject.Saturday,
                _ => Microsoft.Graph.Models.DayOfWeekObject.Monday
            };

        private static string ToGraphDayName(System.DayOfWeek day) =>
            day switch
            {
                System.DayOfWeek.Sunday => "sunday",
                System.DayOfWeek.Monday => "monday",
                System.DayOfWeek.Tuesday => "tuesday",
                System.DayOfWeek.Wednesday => "wednesday",
                System.DayOfWeek.Thursday => "thursday",
                System.DayOfWeek.Friday => "friday",
                System.DayOfWeek.Saturday => "saturday",
                _ => "monday"
            };
        private async Task<string?> GetTechnicalAccountEmailAsync(CancellationToken cancellationToken)
        {
            try
            {
                var tenant = _tenantProvider.GetCurrentTenant();
                var graphTenantId = (tenant.GraphTenantId ?? string.Empty).Trim();

                var appAccount = await _smartContext.AplicativosTeams
                    .AsNoTracking()
                    .Where(a => a.Activo == "A" && (string.IsNullOrEmpty(graphTenantId) || a.TenantId == graphTenantId))
                    .OrderBy(a => a.IdAplicativo)
                    .FirstOrDefaultAsync(cancellationToken);

                if (appAccount != null && !string.IsNullOrWhiteSpace(appAccount.UsernameApp))
                {
                    return appAccount.UsernameApp.Trim();
                }

                var fallbackAccount = await _smartContext.AplicativosTeams
                    .AsNoTracking()
                    .Where(a => a.Activo == "A")
                    .OrderBy(a => a.IdAplicativo)
                    .FirstOrDefaultAsync(cancellationToken);

                if (fallbackAccount != null && !string.IsNullOrWhiteSpace(fallbackAccount.UsernameApp))
                {
                    return fallbackAccount.UsernameApp.Trim();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve technical account email from AplicativosTeams.");
            }

            return null;
        }
    }
}
