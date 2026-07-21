using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Stats.Queries.GetDetailedReport;
using ClosedXML.Excel;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace APITeamsV3.Infrastructure.Services
{
    public class ReportExportJob : IReportExportJob
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ReportExportJob> _logger;
        private readonly ITenantProvider _tenantProvider;
        private readonly ICentralDbContext _centralDb;
        private readonly IEncryptionService _encryptionService;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;

        public ReportExportJob(
            IMediator mediator, 
            ILogger<ReportExportJob> logger,
            ITenantProvider tenantProvider,
            ICentralDbContext centralDb,
            IEncryptionService encryptionService,
            Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            _mediator = mediator;
            _logger = logger;
            _tenantProvider = tenantProvider;
            _centralDb = centralDb;
            _encryptionService = encryptionService;
            _env = env;
        }

        public async Task ExecuteExportAsync(GetDetailedReportQuery query, string companyKey, string jobId, string? executedBy = null)
        {
            try
            {
                var config = await _centralDb.CompanyConfigs.FirstOrDefaultAsync(c => c.CompanyKey == companyKey && c.IsActive);
                if (config == null) throw new InvalidOperationException($"Tenant '{companyKey}' not found or inactive.");
                
                _tenantProvider.SetTenant(new TenantContext
                {
                    CompanyId = config.Id,
                    CompanyKey = config.CompanyKey,
                    DisplayName = config.DisplayName ?? string.Empty,
                    ConnectionString = _encryptionService.Decrypt(config.SmartConnectionString),
                    TimeZoneId = config.TimeZoneId,
                    GraphTenantId = config.GraphTenantId,
                    GraphClientId = config.GraphClientId,
                    GraphClientSecret = config.GraphClientSecretRef
                });

                _logger.LogInformation($"Starting background export job {jobId} for company {companyKey}");
                
                // Override to get all records, this could be massive so ideally we should fetch all but avoid pagination
                query.PageNumber = 1;
                query.PageSize = 1000000; // max reasonable rows, ideally should iterate over pages if we don't want to load all in memory at once

                var result = await _mediator.Send(query);

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Reporte");

                // Headers
                worksheet.Cell(1, 1).Value = "División";
                worksheet.Cell(1, 2).Value = "Programa";
                worksheet.Cell(1, 3).Value = "Carrera";
                worksheet.Cell(1, 4).Value = "Periodo";
                worksheet.Cell(1, 5).Value = "Inicio";
                worksheet.Cell(1, 6).Value = "Fin";
                worksheet.Cell(1, 7).Value = "Sección";
                worksheet.Cell(1, 8).Value = "Turno";
                worksheet.Cell(1, 9).Value = "Curso";
                worksheet.Cell(1, 10).Value = "Sesiones";
                worksheet.Cell(1, 11).Value = "Docente Cód.";
                worksheet.Cell(1, 12).Value = "Docente Nombres";
                worksheet.Cell(1, 13).Value = "Docente Correo";
                worksheet.Cell(1, 14).Value = "Docente Celular";
                worksheet.Cell(1, 15).Value = "Sede";
                worksheet.Cell(1, 16).Value = "Categoría";
                worksheet.Cell(1, 17).Value = "Responsable";
                worksheet.Cell(1, 18).Value = "Alumno Celular";
                worksheet.Cell(1, 19).Value = "Alumno";
                worksheet.Cell(1, 20).Value = "Condición";
                worksheet.Cell(1, 21).Value = "Link";
                worksheet.Cell(1, 22).Value = "Horario";
                worksheet.Cell(1, 23).Value = "Migró a Teams";
                worksheet.Cell(1, 24).Value = "Fecha Creación Teams";

                var headerRange = worksheet.Range(1, 1, 1, 24);
                headerRange.Style.Font.Bold = true;

                int row = 2;
                foreach (var item in result.Data)
                {
                    worksheet.Cell(row, 1).Value = item.Division;
                    worksheet.Cell(row, 2).Value = item.Programa;
                    worksheet.Cell(row, 3).Value = item.Carrera;
                    worksheet.Cell(row, 4).Value = item.PeriodoCodigo;
                    worksheet.Cell(row, 5).Value = item.PeriodoInicio?.ToString("dd/MM/yyyy") ?? "";
                    worksheet.Cell(row, 6).Value = item.PeriodoFin?.ToString("dd/MM/yyyy") ?? "";
                    worksheet.Cell(row, 7).Value = item.Seccion;
                    worksheet.Cell(row, 8).Value = item.Turno;
                    worksheet.Cell(row, 9).Value = item.Curso;
                    worksheet.Cell(row, 10).Value = item.TotalSesiones;
                    worksheet.Cell(row, 11).Value = item.DocenteCodigo;
                    worksheet.Cell(row, 12).Value = item.DocenteNombres;
                    worksheet.Cell(row, 13).Value = item.DocenteCorreo;
                    worksheet.Cell(row, 14).Value = item.DocenteCelular;
                    worksheet.Cell(row, 15).Value = item.DocenteSedePrincipal;
                    worksheet.Cell(row, 16).Value = item.DocenteCategoria;
                    worksheet.Cell(row, 17).Value = item.DocenteTipoResponsable;
                    worksheet.Cell(row, 18).Value = item.AlumnoCelular;
                    worksheet.Cell(row, 19).Value = item.Alumno;
                    worksheet.Cell(row, 20).Value = item.AlumnoTipoCondicion;
                    worksheet.Cell(row, 21).Value = item.Link;
                    worksheet.Cell(row, 22).Value = item.DescripcionHorario;
                    worksheet.Cell(row, 23).Value = item.MigroTeams ? "Sí" : "No";
                    worksheet.Cell(row, 24).Value = item.FechaCreacionEquipoTeams?.ToString("dd/MM/yyyy HH:mm") ?? "";
                    row++;
                }

                var folderPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "exports");
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                var filePath = Path.Combine(folderPath, $"{jobId}.xlsx");
                workbook.SaveAs(filePath);

                _logger.LogInformation($"Export job {jobId} finished. Saved to {filePath}");
                
                // Write a ready file to indicate completion
                await File.WriteAllTextAsync(Path.Combine(folderPath, $"{jobId}.ready"), "done");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during export job {jobId}");
                var folderPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "exports");
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
                await File.WriteAllTextAsync(Path.Combine(folderPath, $"{jobId}.error"), ex.Message);
                throw;
            }
        }
    }
}
