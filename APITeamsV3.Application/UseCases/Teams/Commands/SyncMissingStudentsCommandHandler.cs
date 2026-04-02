using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using APITeamsV3.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncMissingStudentsCommandHandler : IRequestHandler<SyncMissingStudentsCommand, List<MissingStudentDto>>
    {
        private readonly ISmartDbContext _context;
        private readonly IGraphClientFactory _graphFactory;
        private readonly ILogger<SyncMissingStudentsCommandHandler> _logger;

        public SyncMissingStudentsCommandHandler(
            ISmartDbContext context,
            IGraphClientFactory graphFactory,
            ILogger<SyncMissingStudentsCommandHandler> logger)
        {
            _context = context;
            _graphFactory = graphFactory;
            _logger = logger;
        }

        public async Task<List<MissingStudentDto>> Handle(SyncMissingStudentsCommand request, CancellationToken cancellationToken)
        {
            // Step 1: Query Delta DIRECTLY from Academic Source of Truth (Views/Main Tables)
            // We no longer populate staging tables (TeamsProgramacionAlumnos) before syncing.
            
            var missingStudents = await (from ac in _context.Set<AlumnoCurso>()
                                         join al in _context.Set<Alumno>() on ac.IdAlumno equals al.IdAlumno
                                         join te in _context.TeamsEquipos on ac.IdSeccion equals te.IdSeccionSmart
                                         where ac.IdSeccion == request.IdSeccion
                                            && ac.EsMatricula == true // Active Enrollments only
                                            && te.EstadoTeam == "A"
                                            && !string.IsNullOrEmpty(al.EmailInstitucion)
                                            && !_context.TeamsUsuarios.Any(tu => tu.IdTeams == te.IdTeamsGroup 
                                                                             && tu.CodigoAlumno == al.Codigo 
                                                                             && tu.Tipo == "A"
                                                                             && tu.Estado == "A")
                                         select new MissingStudentDto
                                         {
                                             IdTeamsGroup = te.IdTeamsGroup,
                                             CodigoAlumno = al.Codigo,
                                             NombresAlumno = al.Nombre,
                                             ApellidosAlumno = "", // Alumno view often returns composite names
                                             EmailAlumno = al.EmailInstitucion
                                         })
                                         .Distinct()
                                         .ToListAsync(cancellationToken);

            if (!missingStudents.Any())
            {
                _logger.LogInformation($"All students for section {request.IdSeccion} are already synchronized from Academic Source.");
                return missingStudents;
            }

            // Step 3: Actuation (Graph API + DB Persistence)
            var graphClient = await _graphFactory.CreateClientAsync();
            var syncedCount = 0;

            foreach (var student in missingStudents)
            {
                try
                {
                    _logger.LogDebug($"Syncing student {student.CodigoAlumno} ({student.EmailAlumno}) to Team {student.IdTeamsGroup}");

                    // 3.1 Resolve Azure AD User ID
                    var user = await graphClient.Users[student.EmailAlumno].GetAsync(cancellationToken: cancellationToken);
                    if (user == null || string.IsNullOrEmpty(user.Id))
                    {
                        _logger.LogWarning($"Student {student.EmailAlumno} not found in Azure AD. Skipping...");
                        continue;
                    }

                    // 3.2 Add Member to Group/Team
                    var requestBody = new Microsoft.Graph.Models.ReferenceCreate
                    {
                        OdataId = $"https://graph.microsoft.com/v1.0/users/{user.Id}",
                    };

                    try 
                    {
                        await graphClient.Groups[student.IdTeamsGroup].Members.Ref.PostAsync(requestBody, cancellationToken: cancellationToken);
                    }
                    catch (Exception graphEx) when (graphEx.Message.Contains("already exist", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug($"Student {student.EmailAlumno} is already a member of the group ({graphEx.Message}).");
                    }

                    // 3.3 Persistence in TeamsUsuarios
                    var userEntity = new TeamMember
                    {
                        IdTeams = student.IdTeamsGroup,
                        CodigoAlumno = student.CodigoAlumno,
                        Nombres = student.NombresAlumno,
                        Apellidos = student.ApellidosAlumno,
                        Email = student.EmailAlumno,
                        Tipo = "A", // Alumno
                        Estado = "A", // Activo
                        FechaCreacion = DateTime.UtcNow,
                        UsuarioCreacion = 1 // System/Automation ID
                    };

                    await _context.TeamsUsuarios.AddAsync(userEntity, cancellationToken);
                    syncedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to sync student {student.EmailAlumno} for section {request.IdSeccion}.");
                }
            }

            if (syncedCount > 0)
            {
                try 
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation($"Successfully synchronized {syncedCount} missing students for section {request.IdSeccion}.");
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, $"Failed to persist student sync status in DB for section {request.IdSeccion}.");
                }
            }

            return missingStudents;
        }
    }
}
