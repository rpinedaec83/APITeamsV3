using APITeamsV3.Application.Common.Graph;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using APITeamsV3.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncObsoleteStudentsCommandHandler : IRequestHandler<SyncObsoleteStudentsCommand, List<ObsoleteStudentDto>>
    {
        private readonly ISmartDbContext _context;
        private readonly IGraphClientFactory _graphFactory;
        private readonly IGraphUserLookupService _userLookupService;
        private readonly ITeamsLogOperativoRepository _logRepository;
        private readonly ILogger<SyncObsoleteStudentsCommandHandler> _logger;
        private readonly ICurrentUserService _currentUserService;

        public SyncObsoleteStudentsCommandHandler(
            ISmartDbContext context,
            IGraphClientFactory graphFactory,
            IGraphUserLookupService userLookupService,
            ITeamsLogOperativoRepository logRepository,
            ILogger<SyncObsoleteStudentsCommandHandler> logger,
            ICurrentUserService currentUserService)
        {
            _context = context;
            _graphFactory = graphFactory;
            _userLookupService = userLookupService;
            _logRepository = logRepository;
            _logger = logger;
            _currentUserService = currentUserService;
        }

        public async Task<List<ObsoleteStudentDto>> Handle(SyncObsoleteStudentsCommand request, CancellationToken cancellationToken)
        {
            var activeTeamGroups = await _context.TeamsEquipos
                .AsNoTracking()
                .Where(te => te.IdSeccionSmart == request.IdSeccion && te.EstadoTeam == "A")
                .Select(te => te.IdTeamsGroup)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (!activeTeamGroups.Any())
            {
                _logger.LogInformation(
                    "No active team groups were found for section {SectionId}.",
                    request.IdSeccion);
                return new List<ObsoleteStudentDto>();
            }

            var enrolledIdentifiers = await BuildEnrolledStudentIdentifiersAsync(request.IdSeccion, cancellationToken);

            var obsoleteStudents = await _context.TeamsUsuarios
                .AsNoTracking()
                .Where(tu => activeTeamGroups.Contains(tu.IdTeams) &&
                             tu.Estado == "A" &&
                             tu.Tipo == "A")
                .Select(tu => new ObsoleteStudentDto
                {
                    IdTeamsGroup = tu.IdTeams,
                    CodigoAlumno = tu.CodigoAlumno,
                    EmailAlumno = tu.Email
                })
                .ToListAsync(cancellationToken);

            obsoleteStudents = obsoleteStudents
                .Where(student => !IsStillEnrolled(student, enrolledIdentifiers))
                .ToList();

            obsoleteStudents = obsoleteStudents
                .GroupBy(s => $"{s.IdTeamsGroup}::{s.CodigoAlumno}", StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            if (!obsoleteStudents.Any())
            {
                _logger.LogInformation(
                    "No obsolete students were found for section {SectionId}.",
                    request.IdSeccion);
                return obsoleteStudents;
            }

            var graphClient = await _graphFactory.CreateClientAsync();
            var validGroupIds = await FilterValidGroupIdsAsync(
                graphClient,
                obsoleteStudents.Select(student => student.IdTeamsGroup).Distinct(StringComparer.OrdinalIgnoreCase),
                cancellationToken);

            obsoleteStudents = obsoleteStudents
                .Where(student => validGroupIds.Contains(student.IdTeamsGroup))
                .ToList();

            if (!obsoleteStudents.Any())
            {
                _logger.LogWarning(
                    "Obsolete-student sync aborted for section {SectionId} because no active Graph group could be validated from TeamsEquipos.",
                    request.IdSeccion);
                return obsoleteStudents;
            }

            var hasChanges = false;

            foreach (var student in obsoleteStudents)
            {
                try
                {
                    var userId = await ResolveUserIdAsync(graphClient, student, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(userId))
                    {
                        try
                        {
                            await graphClient.Groups[student.IdTeamsGroup].Members[userId].Ref.DeleteAsync(cancellationToken: cancellationToken);
                        }
                        catch (Exception ex) when (IsAlreadyMissingError(ex))
                        {
                            _logger.LogDebug(
                                "Student {StudentCode} was already absent from Team {TeamId}.",
                                student.CodigoAlumno,
                                student.IdTeamsGroup);
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Could not resolve Graph user for obsolete student {StudentCode} ({StudentEmail}) in Team {TeamId}. Local membership will still be inactivated.",
                            student.CodigoAlumno,
                            student.EmailAlumno,
                            student.IdTeamsGroup);
                    }

                    var localMembers = await _context.TeamsUsuarios
                        .Where(u => u.IdTeams == student.IdTeamsGroup
                                 && u.CodigoAlumno == student.CodigoAlumno
                                 && u.Tipo == "A"
                                 && u.Estado == "A")
                        .ToListAsync(cancellationToken);

                    foreach (var localMember in localMembers)
                    {
                        localMember.Estado = "I";
                        localMember.FechaModificacion = DateTime.UtcNow;
                        localMember.UsuarioModificacion = _currentUserService.UserIdInt ?? 1;
                        hasChanges = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to remove obsolete student {StudentCode} from Team {TeamId}.",
                        student.CodigoAlumno,
                        student.IdTeamsGroup);

                    await LogOperativoAsync(
                        "Error",
                        "Student",
                        student.CodigoAlumno,
                        $"Failed to remove obsolete student from Team {student.IdTeamsGroup}: {ex.Message}",
                        request.JobId);
                }
            }

            if (hasChanges)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            return obsoleteStudents;
        }

        private async Task<string?> ResolveUserIdAsync(
            Microsoft.Graph.GraphServiceClient graphClient,
            ObsoleteStudentDto student,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(student.EmailAlumno))
            {
                var user = await _userLookupService.FindUserAsync(graphClient, student.EmailAlumno, cancellationToken: cancellationToken);
                if (!string.IsNullOrWhiteSpace(user?.Id))
                {
                    return user.Id;
                }
            }

            var members = await graphClient.Groups[student.IdTeamsGroup].Members.GetAsync(
                requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = new[] { "id", "mail", "userPrincipalName", "displayName" };
                },
                cancellationToken);

            var normalizedCode = NormalizeLocalPart(student.CodigoAlumno);
            var normalizedEmail = NormalizeLocalPart(student.EmailAlumno);

            return members?.Value?
                .OfType<User>()
                .FirstOrDefault(user => MatchesStudent(user, normalizedCode, normalizedEmail))
                ?.Id;
        }

        private static bool MatchesStudent(User user, string normalizedCode, string normalizedEmail)
        {
            if (string.IsNullOrWhiteSpace(user.Id))
            {
                return false;
            }

            var mailLocalPart = NormalizeLocalPart(user.Mail);
            var upnLocalPart = NormalizeLocalPart(user.UserPrincipalName);

            return (!string.IsNullOrWhiteSpace(normalizedEmail) &&
                    (mailLocalPart == normalizedEmail || upnLocalPart == normalizedEmail))
                || (!string.IsNullOrWhiteSpace(normalizedCode) &&
                    (mailLocalPart == normalizedCode || upnLocalPart == normalizedCode));
        }

        private static string NormalizeLocalPart(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            var at = trimmed.IndexOf('@');
            return (at >= 0 ? trimmed.Substring(0, at) : trimmed).Trim().ToLowerInvariant();
        }

        private async Task<HashSet<string>> BuildEnrolledStudentIdentifiersAsync(int idSeccion, CancellationToken cancellationToken)
        {
            var enrolledStudents = await _context.Set<AlumnoCurso>()
                .AsNoTracking()
                .Include(ac => ac.Alumno)
                .Where(ac => ac.IdSeccion == idSeccion && ac.EsMatricula)
                .Select(ac => new
                {
                    Codigo = ac.Alumno != null ? ac.Alumno.Codigo : string.Empty,
                    EmailInstitucion = ac.Alumno != null ? ac.Alumno.EmailInstitucion : string.Empty,
                    EmailPersonal = ac.Alumno != null ? ac.Alumno.EmailPersonal : null
                })
                .ToListAsync(cancellationToken);

            var identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var student in enrolledStudents)
            {
                AddIdentifierIfPresent(identifiers, student.Codigo);
                AddIdentifierIfPresent(identifiers, student.EmailInstitucion);
                AddIdentifierIfPresent(identifiers, student.EmailPersonal);
            }

            return identifiers;
        }

        private static bool IsStillEnrolled(ObsoleteStudentDto student, ISet<string> enrolledIdentifiers)
        {
            var normalizedCode = NormalizeLocalPart(student.CodigoAlumno);
            if (!string.IsNullOrWhiteSpace(normalizedCode) && enrolledIdentifiers.Contains(normalizedCode))
            {
                return true;
            }

            var normalizedEmail = NormalizeLocalPart(student.EmailAlumno);
            return !string.IsNullOrWhiteSpace(normalizedEmail) && enrolledIdentifiers.Contains(normalizedEmail);
        }

        private static void AddIdentifierIfPresent(ISet<string> identifiers, string? value)
        {
            var normalized = NormalizeLocalPart(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                identifiers.Add(normalized);
            }
        }

        private static bool IsAlreadyMissingError(Exception ex)
        {
            return ex.Message.Contains("404", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("resource not found", StringComparison.OrdinalIgnoreCase);
        }

        private async Task LogOperativoAsync(string type, string target, string reference, string message, string? jobId)
        {
            try
            {
                await _logRepository.LogAsync(new TeamsLogOperativo
                {
                    Tipo = type,
                    EntidadAfectada = target,
                    Referencia = reference,
                    Mensaje = message,
                    JobId = jobId,
                    Fecha = DateTime.UtcNow,
                    Severidad = type == "Error" ? "High" : "Low"
                });
            }
            catch
            {
                // Avoid recursive logging failures.
            }
        }

        private async Task<HashSet<string>> FilterValidGroupIdsAsync(
            Microsoft.Graph.GraphServiceClient graphClient,
            IEnumerable<string> groupIds,
            CancellationToken cancellationToken)
        {
            var validGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var groupId in groupIds.Where(groupId => !string.IsNullOrWhiteSpace(groupId)))
            {
                if (await GraphGroupGuard.GroupExistsAsync(graphClient, groupId, cancellationToken))
                {
                    validGroupIds.Add(groupId);
                    continue;
                }

                await MarkGroupAsInconsistentAsync(groupId, cancellationToken);
            }

            return validGroupIds;
        }

        private async Task MarkGroupAsInconsistentAsync(string groupId, CancellationToken cancellationToken)
        {
            var affected = await _context.TeamsEquipos
                .Where(team => team.IdTeamsGroup == groupId && team.EstadoTeam == "A")
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(team => team.EstadoTeam, "I")
                        .SetProperty(team => team.FechaModificacion, DateTime.UtcNow)
                        .SetProperty(team => team.UsuarioModificacion, _currentUserService.UserIdInt ?? 1),
                    cancellationToken);

            if (affected > 0)
            {
                _logger.LogWarning(
                    "Graph group {GroupId} does not exist. Matching TeamsEquipos rows were marked inactive before syncing obsolete students.",
                    groupId);
            }
        }
    }
}
