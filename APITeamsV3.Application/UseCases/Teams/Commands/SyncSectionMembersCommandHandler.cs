using APITeamsV3.Application.Common.Graph;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncSectionMembersCommandHandler : IRequestHandler<SyncSectionMembersCommand, SyncSectionMembersResult>
    {
        private readonly ISmartDbContext _context;
        private readonly IGraphClientFactory _graphFactory;
        private readonly IMediator _mediator;
        private readonly ILogger<SyncSectionMembersCommandHandler> _logger;

        public SyncSectionMembersCommandHandler(
            ISmartDbContext context,
            IGraphClientFactory graphFactory,
            IMediator mediator,
            ILogger<SyncSectionMembersCommandHandler> logger)
        {
            _context = context;
            _graphFactory = graphFactory;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<SyncSectionMembersResult> Handle(SyncSectionMembersCommand request, CancellationToken cancellationToken)
        {
            var activeTeam = await _context.TeamsEquipos
                .FirstOrDefaultAsync(
                    team => team.IdSeccionSmart == request.IdSeccion && team.EstadoTeam == "A",
                    cancellationToken);

            if (activeTeam == null)
            {
                return new SyncSectionMembersResult
                {
                    Success = false,
                    Summary = "La sección no tiene un equipo activo. Usa Aprovisionar Equipo en Teams."
                };
            }

            var graphClient = await _graphFactory.CreateClientAsync();
            if (!await GraphGroupGuard.GroupExistsAsync(graphClient, activeTeam.IdTeamsGroup, cancellationToken))
            {
                await MarkTeamAsInconsistentAsync(activeTeam, cancellationToken);

                return new SyncSectionMembersResult
                {
                    Success = false,
                    Summary = $"El Team local de la seccion {request.IdSeccion} apunta al grupo {activeTeam.IdTeamsGroup}, pero ese grupo no existe en Graph. El registro local fue marcado como inactivo."
                };
            }

            var facilitatorChanges = await _mediator.Send(new SyncTeamFacilitatorsCommand(request.IdSeccion), cancellationToken);
            var missingStudents = await _mediator.Send(new SyncMissingStudentsCommand(request.IdSeccion), cancellationToken);
            var obsoleteStudents = await _mediator.Send(new SyncObsoleteStudentsCommand(request.IdSeccion), cancellationToken);
            var studentsVerifiedInGraph = await RefreshStudentMembershipCacheFromGraphAsync(
                request.IdSeccion,
                activeTeam.IdTeamsGroup,
                cancellationToken);

            return new SyncSectionMembersResult
            {
                Success = true,
                FacilitatorsProcessed = facilitatorChanges.Count,
                StudentsAddedProcessed = missingStudents.Count,
                StudentsRemovedProcessed = obsoleteStudents.Count,
                StudentsVerifiedInGraph = studentsVerifiedInGraph,
                Summary =
                    $"Sincronización de miembros finalizada. " +
                    $"Facilitadores procesados: {facilitatorChanges.Count}. " +
                    $"Candidatos de alta: {missingStudents.Count}. " +
                    $"Candidatos de baja: {obsoleteStudents.Count}. " +
                    $"Alumnos verificados en Graph y actualizados en BD: {studentsVerifiedInGraph}."
            };
        }

        private async Task MarkTeamAsInconsistentAsync(TeamEntity team, CancellationToken cancellationToken)
        {
            if (team.EstadoTeam == "I")
            {
                return;
            }

            team.EstadoTeam = "I";
            team.FechaModificacion = DateTime.UtcNow;
            team.UsuarioModificacion = 1;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Section {SectionId} references Graph group {GroupId}, but that group does not exist. Team was marked inactive locally.",
                team.IdSeccionSmart,
                team.IdTeamsGroup);
        }

        private async Task<int> RefreshStudentMembershipCacheFromGraphAsync(
            int idSeccion,
            string groupId,
            CancellationToken cancellationToken)
        {
            var graphClient = await _graphFactory.CreateClientAsync();
            var graphMemberIdentifiers = await GetGraphMemberIdentifiersAsync(graphClient, groupId, cancellationToken);

            var enrolledStudents = await _context.Set<AlumnoCurso>()
                .AsNoTracking()
                .Include(ac => ac.Alumno)
                .Where(ac => ac.IdSeccion == idSeccion && ac.EsMatricula)
                .Select(ac => new EnrolledStudentSnapshot
                {
                    Codigo = ac.Alumno != null ? ac.Alumno.Codigo : string.Empty,
                    Nombre = ac.Alumno != null ? ac.Alumno.Nombre : string.Empty,
                    EmailInstitucional = ac.Alumno != null ? ac.Alumno.EmailInstitucion : string.Empty,
                    EmailAlternativo = ac.Alumno != null ? ac.Alumno.EmailPersonal : null
                })
                .Where(student => !string.IsNullOrWhiteSpace(student.Codigo))
                .ToListAsync(cancellationToken);

            var confirmedStudents = enrolledStudents
                .Where(student => student.CandidateIdentifiers.Any(graphMemberIdentifiers.Contains))
                .GroupBy(student => student.Codigo, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            var localStudentMembers = await _context.TeamsUsuarios
                .Where(member => member.IdTeams == groupId && member.Tipo == "A")
                .ToListAsync(cancellationToken);

            foreach (var student in confirmedStudents)
            {
                var existingMember = localStudentMembers.FirstOrDefault(member => MatchesStudent(member, student));
                if (existingMember == null)
                {
                    existingMember = new TeamMember
                    {
                        IdTeams = groupId,
                        CodigoAlumno = student.Codigo,
                        Nombres = student.Nombre,
                        Apellidos = string.Empty,
                        Email = student.PreferredEmail,
                        Tipo = "A",
                        Estado = "A",
                        FechaCreacion = DateTime.UtcNow,
                        UsuarioCreacion = 1
                    };

                    await _context.TeamsUsuarios.AddAsync(existingMember, cancellationToken);
                    localStudentMembers.Add(existingMember);
                    continue;
                }

                existingMember.CodigoAlumno = student.Codigo;
                existingMember.Nombres = string.IsNullOrWhiteSpace(student.Nombre) ? existingMember.Nombres : student.Nombre;
                existingMember.Email = student.PreferredEmail;
                existingMember.Estado = "A";
                existingMember.FechaModificacion = DateTime.UtcNow;
                existingMember.UsuarioModificacion = 1;
            }

            var confirmedCodes = new HashSet<string>(
                confirmedStudents.Select(student => student.Codigo).Where(code => !string.IsNullOrWhiteSpace(code)),
                StringComparer.OrdinalIgnoreCase);

            var confirmedIdentifiers = new HashSet<string>(
                confirmedStudents.SelectMany(student => student.CandidateIdentifiers),
                StringComparer.OrdinalIgnoreCase);

            foreach (var localMember in localStudentMembers.Where(member => member.Estado == "A"))
            {
                var localCode = NormalizeIdentifier(localMember.CodigoAlumno);
                var localEmail = NormalizeIdentifier(localMember.Email);
                var isConfirmed = confirmedCodes.Contains(localCode) ||
                                  (!string.IsNullOrWhiteSpace(localEmail) && confirmedIdentifiers.Contains(localEmail));

                if (isConfirmed)
                {
                    continue;
                }

                localMember.Estado = "I";
                localMember.FechaModificacion = DateTime.UtcNow;
                localMember.UsuarioModificacion = 1;
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Graph membership cache refreshed for section {SectionId}. Confirmed students: {ConfirmedCount}.",
                idSeccion,
                confirmedStudents.Count);

            return confirmedStudents.Count;
        }

        private static async Task<HashSet<string>> GetGraphMemberIdentifiersAsync(
            GraphServiceClient graphClient,
            string groupId,
            CancellationToken cancellationToken)
        {
            var identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var response = await graphClient.Groups[groupId].Members.GetAsync(
                requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = ["id", "mail", "userPrincipalName", "mailNickname", "displayName"];
                },
                cancellationToken);

            while (response != null)
            {
                foreach (var user in (response.Value ?? []).OfType<User>())
                {
                    AddIdentifierIfPresent(identifiers, user.Mail);
                    AddIdentifierIfPresent(identifiers, user.UserPrincipalName);
                    AddIdentifierIfPresent(identifiers, user.MailNickname);

                    foreach (var token in ExtractDisplayNameIdentifiers(user.DisplayName))
                    {
                        AddIdentifierIfPresent(identifiers, token);
                    }
                }

                if (string.IsNullOrWhiteSpace(response.OdataNextLink))
                {
                    break;
                }

                response = await graphClient.Groups[groupId].Members
                    .WithUrl(response.OdataNextLink)
                    .GetAsync(cancellationToken: cancellationToken);
            }

            return identifiers;
        }

        private static bool MatchesStudent(TeamMember member, EnrolledStudentSnapshot student)
        {
            var memberCode = NormalizeIdentifier(member.CodigoAlumno);
            if (!string.IsNullOrWhiteSpace(memberCode) &&
                student.CandidateIdentifiers.Contains(memberCode))
            {
                return true;
            }

            var memberEmail = NormalizeIdentifier(member.Email);
            return !string.IsNullOrWhiteSpace(memberEmail) &&
                   student.CandidateIdentifiers.Contains(memberEmail);
        }

        private static void AddIdentifierIfPresent(ISet<string> identifiers, string? value)
        {
            var normalized = NormalizeIdentifier(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                identifiers.Add(normalized);
            }
        }

        private static IEnumerable<string> ExtractDisplayNameIdentifiers(string? displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                yield break;
            }

            foreach (Match match in Regex.Matches(displayName, "[A-Za-z0-9]+"))
            {
                var token = match.Value;
                if (token.Any(char.IsDigit))
                {
                    yield return token;
                }
            }
        }

        private static string NormalizeIdentifier(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            var at = trimmed.IndexOf('@');
            return (at >= 0 ? trimmed.Substring(0, at) : trimmed).Trim().ToLowerInvariant();
        }

        private sealed class EnrolledStudentSnapshot
        {
            public string Codigo { get; init; } = string.Empty;
            public string Nombre { get; init; } = string.Empty;
            public string EmailInstitucional { get; init; } = string.Empty;
            public string? EmailAlternativo { get; init; }

            public string PreferredEmail => !string.IsNullOrWhiteSpace(EmailInstitucional)
                ? EmailInstitucional.Trim()
                : (EmailAlternativo ?? string.Empty).Trim();

            public HashSet<string> CandidateIdentifiers
            {
                get
                {
                    var identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    AddIfPresent(Codigo);
                    AddIfPresent(EmailInstitucional);
                    AddIfPresent(EmailAlternativo);
                    return identifiers;

                    void AddIfPresent(string? value)
                    {
                        var normalized = NormalizeIdentifier(value);
                        if (!string.IsNullOrWhiteSpace(normalized))
                        {
                            identifiers.Add(normalized);
                        }
                    }
                }
            }
        }
    }
}
