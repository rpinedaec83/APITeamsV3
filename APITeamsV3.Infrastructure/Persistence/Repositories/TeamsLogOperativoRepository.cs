using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;

namespace APITeamsV3.Infrastructure.Persistence.Repositories
{
    public class TeamsLogOperativoRepository : ITeamsLogOperativoRepository
    {
        private readonly ISmartDbContext _context;
        private readonly ILogger<TeamsLogOperativoRepository> _logger;
        private Dictionary<string, int>? _columnMaxLengths;

        public TeamsLogOperativoRepository(
            ISmartDbContext context,
            ILogger<TeamsLogOperativoRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogAsync(TeamsLogOperativo log)
        {
            var columnLengths = await GetColumnMaxLengthsAsync(CancellationToken.None);

            Normalize(log, columnLengths, aggressive: false);
            _context.Set<TeamsLogOperativo>().Add(log);

            try
            {
                await _context.SaveChangesAsync(System.Threading.CancellationToken.None);
            }
            catch (DbUpdateException ex) when (IsTruncationError(ex))
            {
                _logger.LogWarning(
                    ex,
                    "TeamsLogOperativo insert exceeded DB column lengths. Retrying with aggressive truncation.");

                try
                {
                    Normalize(log, columnLengths, aggressive: true);
                    await _context.SaveChangesAsync(System.Threading.CancellationToken.None);
                }
                catch (DbUpdateException ex2) when (IsTruncationError(ex2))
                {
                    _logger.LogWarning(
                        ex2,
                        "TeamsLogOperativo insert still exceeds DB column lengths after aggressive truncation. Dropping log row.");

                    if (_context is DbContext dbContext)
                    {
                        foreach (var entry in dbContext.ChangeTracker.Entries<TeamsLogOperativo>()
                                     .Where(e => e.State == EntityState.Added))
                        {
                            entry.State = EntityState.Detached;
                        }
                    }
                }
            }
        }

        private static bool IsTruncationError(DbUpdateException ex)
        {
            if (ex.InnerException is SqlException sqlEx)
            {
                return sqlEx.Number is 8152 or 2628;
            }

            var message = (ex.InnerException?.Message ?? ex.Message ?? string.Empty).ToLowerInvariant();
            return message.Contains("truncar") || message.Contains("truncate");
        }

        private async Task<Dictionary<string, int>> GetColumnMaxLengthsAsync(CancellationToken cancellationToken)
        {
            if (_columnMaxLengths != null)
            {
                return _columnMaxLengths;
            }

            try
            {
                const string sql = @"
SELECT
    COLUMN_NAME AS ColumnName,
    CAST(
        CASE
            WHEN CHARACTER_MAXIMUM_LENGTH IS NULL THEN 0
            ELSE CHARACTER_MAXIMUM_LENGTH
        END
    AS int) AS MaxLength
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = 'TeamsLogOperativo'
  AND COLUMN_NAME IN
  (
      'Tipo',
      'EntidadAfectada',
      'Referencia',
      'Severidad',
      'JobId',
      'Mensaje',
      'ContextoTecnico'
  );";

                var rows = await _context.Database
                    .SqlQueryRaw<ColumnLengthRow>(sql)
                    .ToListAsync(cancellationToken);

                _columnMaxLengths = rows
                    .Where(r => !string.IsNullOrWhiteSpace(r.ColumnName))
                    .ToDictionary(
                        keySelector: r => r.ColumnName!,
                        elementSelector: r => r.MaxLength,
                        comparer: StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not resolve TeamsLogOperativo column lengths from INFORMATION_SCHEMA. Using fallback limits.");
                _columnMaxLengths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }

            return _columnMaxLengths;
        }

        private static void Normalize(TeamsLogOperativo log, IReadOnlyDictionary<string, int> columnLengths, bool aggressive)
        {
            var defaultTextLimit = aggressive ? 250 : 2000;

            log.Tipo = TrimTo(log.Tipo, ResolveMaxLength(columnLengths, "Tipo", aggressive ? 12 : 50));
            log.EntidadAfectada = TrimTo(log.EntidadAfectada, ResolveMaxLength(columnLengths, "EntidadAfectada", aggressive ? 30 : 100));
            log.Referencia = TrimTo(log.Referencia, ResolveMaxLength(columnLengths, "Referencia", aggressive ? 50 : 100));
            log.Severidad = TrimTo(log.Severidad, ResolveMaxLength(columnLengths, "Severidad", aggressive ? 10 : 20));
            log.JobId = TrimTo(log.JobId, ResolveMaxLength(columnLengths, "JobId", aggressive ? 50 : 100));
            log.Mensaje = TrimTo(log.Mensaje, ResolveMaxLength(columnLengths, "Mensaje", defaultTextLimit));
            log.ContextoTecnico = TrimTo(log.ContextoTecnico, ResolveMaxLength(columnLengths, "ContextoTecnico", defaultTextLimit));
        }

        private static int ResolveMaxLength(IReadOnlyDictionary<string, int> columnLengths, string columnName, int fallback)
        {
            if (!columnLengths.TryGetValue(columnName, out var value))
            {
                return fallback;
            }

            // INFORMATION_SCHEMA returns -1 for MAX types.
            if (value <= 0)
            {
                return fallback;
            }

            return Math.Min(value, fallback);
        }

        private static string TrimTo(string? value, int maxLength)
        {
            if (maxLength <= 0)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim();
            return normalized.Length <= maxLength
                ? normalized
                : normalized[..maxLength];
        }

        private sealed class ColumnLengthRow
        {
            public string? ColumnName { get; set; }
            public int MaxLength { get; set; }
        }
    }
}
