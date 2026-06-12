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
                var graphClient = await CreateAgendaGraphClientAsync();

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
                    IsOnlineMeeting = true,
                    OnlineMeetingProvider = OnlineMeetingProviderType.TeamsForBusiness,
                    Attendees = requiredAttendees,
                    Recurrence = new PatternedRecurrence
                    {
                        AdditionalData = new Dictionary<string, object>
                        {
                            ["pattern"] = new Dictionary<string, object>
                            {
                                ["type"] = "weekly",
                                ["interval"] = 1,
                                ["daysOfWeek"] = recurrenceDays
                            },
                            ["range"] = new Dictionary<string, object>
                            {
                                ["type"] = "endDate",
                                ["startDate"] = recurrenceStart.ToString("yyyy-MM-dd"),
                                ["endDate"] = recurrenceEnd.ToString("yyyy-MM-dd")
                            }
                        }
                    }
                };

                var createdEvent = await CreateGroupEventWithRetryAsync(
                    graphClient,
                    request.TeamId,
                    newEvent,
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

                    var chatMessage = new ChatMessage
                    {
                        Body = new ItemBody
                        {
                            ContentType = BodyType.Html,
                            Content = $"<p>Se ha programado una nueva sesion de clase: <b>{request.Subject}</b></p><p><a href='{joinUrl}'>Unirse a la reunion de Microsoft Teams</a></p>"
                        }
                    };

                    await PostChannelMessageWithRetryAsync(
                        graphClient,
                        request.TeamId,
                        request.ChannelId,
                        chatMessage,
                        cancellationToken);
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

                Event? currentEvent = null;
                try
                {
                    currentEvent = await graphClient.Groups[request.TeamId].Events[request.EventId].GetAsync(
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
                        if (DateTime.TryParse(currentEvent.Start?.DateTime, out var currentStart))
                        {
                            if (Math.Abs((currentStart - request.Start.Value).TotalSeconds) > 1)
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
                        if (DateTime.TryParse(currentEvent.End?.DateTime, out var currentEnd))
                        {
                            if (Math.Abs((currentEnd - request.End.Value).TotalSeconds) > 1)
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
                        var currentEmails = currentEvent.Attendees?
                            .Select(a => a.EmailAddress?.Address)
                            .Where(e => !string.IsNullOrWhiteSpace(e))
                            .Select(e => e!.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
                            .ToList() ?? new List<string>();

                        var orderedRequestedEmails = requestedEmails
                            .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        if (!currentEmails.SequenceEqual(orderedRequestedEmails, StringComparer.OrdinalIgnoreCase))
                        {
                            hasChanges = true;
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
            try
            {
                var graphClient = await CreateAgendaGraphClientAsync();
                Exception? lastTransient = null;

                for (var attempt = 1; attempt <= PrimaryChannelMaxAttempts; attempt++)
                {
                    try
                    {
                        var primaryChannel = await graphClient.Teams[teamId].PrimaryChannel.GetAsync(cancellationToken: cancellationToken);
                        if (!string.IsNullOrWhiteSpace(primaryChannel?.Id))
                        {
                            return primaryChannel.Id;
                        }
                    }
                    catch (Exception ex) when (IsTransientGroupProvisioningError(ex))
                    {
                        lastTransient = ex;

                        if (attempt == PrimaryChannelMaxAttempts)
                        {
                            break;
                        }

                        var delay = GetRetryDelay(attempt);
                        _logger.LogWarning(
                            ex,
                            "Primary channel is not ready for Team {TeamId}. Attempt {Attempt}/{MaxAttempts}. Retrying in {Delay}s.",
                            teamId,
                            attempt,
                            PrimaryChannelMaxAttempts,
                            delay.TotalSeconds);

                        await Task.Delay(delay, cancellationToken);
                    }
                }

                // Fallback: query channel list and pick the best candidate.
                try
                {
                    var channelsResponse = await graphClient.Teams[teamId].Channels.GetAsync(cancellationToken: cancellationToken);
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
                    return first?.Id ?? string.Empty;
                }
                catch (Exception ex)
                {
                    if (lastTransient != null)
                    {
                        _logger.LogWarning(lastTransient, "Primary channel retries exhausted for Team {TeamId}.", teamId);
                    }

                    _logger.LogError(ex, "Error fetching channels list for team {TeamId}", teamId);
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching primary channel for team {TeamId}", teamId);
                return string.Empty;
            }
        }

        private async Task<Event?> CreateGroupEventWithRetryAsync(
            GraphServiceClient graphClient,
            string teamId,
            Event newEvent,
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
                    _logger.LogError(
                        ex,
                        "Access denied while creating agenda event for Team/Group {TeamId}. Verify Graph permissions for group calendar operations.",
                        teamId);
                    throw;
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

        private static bool IsAccessDeniedError(Exception ex)
        {
            var message = (ex.Message ?? string.Empty).ToLowerInvariant();
            var textMatch =
                message.Contains("access is denied") ||
                message.Contains("insufficient privileges") ||
                message.Contains("authorization_requestdenied");

            if (ex is ApiException apiException)
            {
                return apiException.ResponseStatusCode == 403 || textMatch;
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
                _logger.LogError(ex, "No se pudo crear cliente Graph delegado para agenda.");
                throw new InvalidOperationException(
                    "No se pudo autenticar agenda con cuenta tecnica delegada. Verificar AplicativosTeams activo y permisos delegados en Azure AD.",
                    ex);
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
