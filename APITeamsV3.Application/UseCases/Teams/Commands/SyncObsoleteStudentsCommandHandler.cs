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

        public SyncObsoleteStudentsCommandHandler(
            ISmartDbContext context,
            IGraphClientFactory graphFactory,
            IGraphUserLookupService userLookupService,
            ITeamsLogOperativoRepository logRepository,
            ILogger<SyncObsoleteStudentsCommandHandler> logger)
        {
            _context = context;
            _graphFactory = graphFactory;
            _userLookupService = userLookupService;
            _logRepository = logRepository;
            _logger = logger;
        }

        public async Task<List<ObsoleteStudentDto>> Handle(SyncObsoleteStudentsCommand request, CancellationToken cancellationToken)
        {
            var obsoleteStudents = await (from tu in _context.TeamsUsuarios
                                          join te in _context.TeamsEquipos on tu.IdTeams equals te.IdTeamsGroup
                                          where te.IdSeccionSmart == request.IdSeccion
                                             && tu.Estado == "A"
                                             && tu.Tipo == "A"
                                             && te.EstadoTeam == "A"
                                             && !_context.TeamsProgramacionAlumnos
                                                    .Any(mpa => mpa.IdCurso == te.IdSeccionSmart
                                                             && mpa.CodigoAlumno == tu.CodigoAlumno
                                                             && mpa.Estado == "A")
                                          select new ObsoleteStudentDto
                                          {
                                              IdTeamsGroup = te.IdTeamsGroup,
                                              CodigoAlumno = tu.CodigoAlumno,
                                              EmailAlumno = tu.Email
                                          })
                                          .ToListAsync(cancellationToken);

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
                        localMember.UsuarioModificacion = 1;
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

                    await LogErrorAsync(
                        "Student",
                        student.CodigoAlumno,
                        $"Failed to remove obsolete student from Team {student.IdTeamsGroup}: {ex.Message}");
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

        private static bool IsAlreadyMissingError(Exception ex)
        {
            return ex.Message.Contains("404", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("resource not found", StringComparison.OrdinalIgnoreCase);
        }

        private async Task LogErrorAsync(string target, string reference, string message)
        {
            try
            {
                await _logRepository.LogAsync(new TeamsLogOperativo
                {
                    Tipo = "Error",
                    EntidadAfectada = target,
                    Referencia = reference,
                    Mensaje = message,
                    Fecha = DateTime.UtcNow,
                    Severidad = "High"
                });
            }
            catch
            {
                // Avoid recursive logging failures.
            }
        }
    }
}
