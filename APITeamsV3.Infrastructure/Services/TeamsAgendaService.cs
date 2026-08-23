using APITeamsV3.Application.Common.Interfaces;
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
        private readonly ILogger<TeamsAgendaService> _logger;

        public TeamsAgendaService(
            IGraphClientFactory graphFactory,
            ITenantProvider tenantProvider,
            ILogger<TeamsAgendaService> logger)
        {
            _graphFactory = graphFactory;
            _tenantProvider = tenantProvider;
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

                var requiredAttendees = request.RequiredAttendeeEmails?
                    .Where(email => !string.IsNullOrWhiteSpace(email))
                    .Select(email => email.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(email => new Attendee
                    {
                        EmailAddress = new EmailAddress { Address = email },
                        Type = AttendeeType.Required
                    })
                    .ToList() ?? new List<Attendee>();

                var timeZoneId = ResolveTimeZoneId();
                var appClient = await _graphFactory.CreateClientAsync();

                var daysOfWeekList = request.RecurrenceDays?
                    .Distinct()
                    .Select(ToGraphDayOfWeek)
                    .Where(d => d.HasValue)
                    .ToList() ?? new List<Microsoft.Graph.Models.DayOfWeekObject?>();

                if (daysOfWeekList.Count == 0)
                {
                    daysOfWeekList.Add(ToGraphDayOfWeek(request.FirstOccurrenceStart.DayOfWeek) ?? Microsoft.Graph.Models.DayOfWeekObject.Monday);
                }

                string? joinUrl = null;

                try
                {
                    var techAccountEmail = "app.teams.idatpe@idat.pe";
                    var techUser = await appClient.Users[techAccountEmail].GetAsync(
                        requestConfiguration => requestConfiguration.QueryParameters.Select = ["id"],
                        cancellationToken: cancellationToken);

                    if (!string.IsNullOrWhiteSpace(techUser?.Id))
                    {
                        var onlineMeetingRequest = new Microsoft.Graph.Models.OnlineMeeting
                        {
                            Subject = request.Subject,
                            StartDateTime = startDate,
                            EndDateTime = endDate
                        };
                        var createdMeeting = await appClient.Users[techUser.Id].OnlineMeetings.PostAsync(onlineMeetingRequest, cancellationToken: cancellationToken);
                        joinUrl = createdMeeting?.JoinWebUrl;
                        _logger.LogInformation("Successfully created OnlineMeeting under technical account {TechEmail}. JoinUrl: {JoinUrl}", techAccountEmail, joinUrl);
                    }
                }
                catch (Exception meetingEx)
                {
                    _logger.LogWarning(meetingEx, "Could not create OnlineMeeting for technical account via App-Only client.");
                }

                var groupEventBody = request.HtmlContent;
                if (!string.IsNullOrWhiteSpace(joinUrl))
                {
                    groupEventBody = $"<p><a href=\"{joinUrl}\" target=\"_blank\"><b>Unirse a la reunión de Microsoft Teams</b></a></p><br/>" + request.HtmlContent;
                }

                var newEvent = new Event
                {
                    Subject = request.Subject,
                    Body = new ItemBody
                    {
                        ContentType = BodyType.Html,
                        Content = groupEventBody
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
                    }
                };

                var createdEvent = await CreateGroupEventWithRetryAsync(
                    appClient,
                    request.TeamId,
                    newEvent,
                    request.PresenterEmails,
                    cancellationToken);

                if (string.IsNullOrWhiteSpace(joinUrl))
                {
                    joinUrl = createdEvent?.OnlineMeeting?.JoinUrl
                        ?? createdEvent?.OnlineMeetingUrl
                        ?? createdEvent?.WebLink
                        ?? string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(joinUrl))
                {
                    await TryPromotePresentersAsync(
                        appClient,
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
                                Content = $"<p>Se ha programado una nueva sesion de clase: <b>{request.Subject}</b></p><p><a href='{joinUrl}'>Unirse a la reunion de Microsoft Teams</a></p>"
                            }
                        };

                        await PostChannelMessageWithRetryAsync(
                            appClient,
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
                var requestedEmails = (request.RequiredAttendeeEmails ?? Array.Empty<string>())
                    .Where(email => !string.IsNullOrWhiteSpace(email))
                    .Select(email => email.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

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
                    else if (existing.Role != OnlineMeetingRole.Coorganizer)
                    {
                        existing.Role = OnlineMeetingRole.Coorganizer;
                        changed = true;
                    }
                }

                if (!changed)
                {
                    var needsSettingsPatch = onlineMeeting.AllowedPresenters != OnlineMeetingPresenters.RoleIsPresenter ||
                                             onlineMeeting.AllowRecording != true;

                    if (needsSettingsPatch)
                    {
                        await PatchOnlineMeetingWithFallbackAsync(
                            graphClient,
                            onlineMeeting.Id,
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

                try
                {
                    await PatchOnlineMeetingWithFallbackAsync(
                        graphClient,
                        onlineMeeting.Id,
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

        private async Task<OnlineMeeting?> FindOnlineMeetingByJoinUrlAsync(
            GraphServiceClient graphClient,
            string joinUrl,
            CancellationToken cancellationToken)
        {
            var escapedJoinUrl = joinUrl.Replace("'", "''");

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
            OnlineMeeting patch,
            CancellationToken cancellationToken)
        {
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
            try
            {
                var graphClient = await CreateAgendaGraphClientAsync();
                await graphClient.Groups[teamId].Events[eventId].DeleteAsync();
                _logger.LogInformation("Deleted meeting {EventId} from Team {TeamId}", eventId, teamId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting meeting {EventId} in team {TeamId}", eventId, teamId);
                throw;
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
                        "Access denied for group calendar {TeamId}. Attempting App-Only fallback...",
                        teamId);
                    try
                    {
                        var appClient = await _graphFactory.CreateClientAsync();
                        return await appClient.Groups[teamId].Events.PostAsync(newEvent, cancellationToken: cancellationToken);
                    }
                    catch (Exception appEx) when (IsAccessDeniedError(appEx))
                    {
                        _logger.LogWarning(appEx, "App-Only group calendar creation also received Access Denied for Team {TeamId}.", teamId);

                        var teacherEmail = presenterEmails?
                            .Where(e => !string.IsNullOrWhiteSpace(e))
                            .Select(e => e.Trim())
                            .FirstOrDefault();

                        if (!string.IsNullOrWhiteSpace(teacherEmail))
                        {
                            try
                            {
                                _logger.LogInformation("Attempting fallback to primary teacher user calendar ({TeacherEmail})...", teacherEmail);
                                var appClient = await _graphFactory.CreateClientAsync();
                                return await appClient.Users[teacherEmail].Events.PostAsync(newEvent, cancellationToken: cancellationToken);
                            }
                            catch (Exception teacherEx)
                            {
                                _logger.LogError(teacherEx, "Teacher calendar fallback also failed for {TeacherEmail}.", teacherEmail);
                            }
                        }

                        throw;
                    }
                }
                catch (Exception ex) when (IsTransientGroupProvisioningError(ex))
                {
                    lastException = ex;

                    if (attempt == CreateGroupEventMaxAttempts)
                    {
                        break;
                    }

                    var delay = GetRetryDelay(attempt);
                    _logger.LogWarning(
                        ex,
                        "Group calendar is not ready for Team/Group {TeamId}. Attempt {Attempt}/{MaxAttempts}. Retrying in {Delay}s.",
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
                var techAccountEmail = "app.teams.idatpe@idat.pe";

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
            var textMatch =
                (message.Contains("requested group") && message.Contains("invalid")) ||
                (message.Contains("resource") && message.Contains("not found")) ||
                (message.Contains("group") && message.Contains("invalid")) ||
                message.Contains("failed to execute msgraph backend request") ||
                message.Contains("does not exist") ||
                message.Contains("mailbox") ||
                message.Contains("not ready");

            if (ex is ApiException apiException)
            {
                return apiException.ResponseStatusCode is 400 or 404 or 409 or 429 or 503
                    && textMatch;
            }

            return textMatch;
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
    }
}
