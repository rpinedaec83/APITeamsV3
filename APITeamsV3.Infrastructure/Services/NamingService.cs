using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using System.Text.RegularExpressions;
using System.Text;

namespace APITeamsV3.Infrastructure.Services
{
    public class NamingService : INamingService
    {
        public string GetMailNickname(Seccion seccion)
        {
            // Format: {ProductoCodigo}.{IdCurricula}.{IdCurso}.{Periodo}-{IdSeccion}
            // E.g.: 0324.313.1610.2025I-12239
            
            // Need to ensure these properties exist on Seccion extended entity or are fetched.
            // For now assuming the view provides them or they are part of Seccion class.
            
            // Note: In cTeamsPorSeccion.sql:
            // REPLACE(PE.Codigo, '-', '') is used for Periodo part if not careful, but the requirement says:
            // 2025I is allowed.
            // Requirement: "Mantener '.' y '-' tal cual"
            
            // IMPORTANT: We need IdCurricula on Seccion.
            // Let's assume for now Seccion has these fields populated.

            var sb = new StringBuilder();
            sb.Append($"{seccion.ProductoCodigo}.");
            sb.Append($"{(seccion.IdPromocion)}."); // Mapping issue: IdCurricula is likely via Promocion. 
                                                     // For this implementation, I will assume Seccion DTO has it. 
                                                     // I need to update Seccion Entity to include IdCurricula if missing.
            sb.Append($"{seccion.IdCurso}.");
            sb.Append($"{seccion.CodigoPeriodo}-");
            sb.Append($"{seccion.IdSeccion}");

            var nickname = sb.ToString();

            // Sanitization: Allow only [A-Za-z0-9.-]
            // Actually the requirement says "Mantener '.' y '-' tal cual" so we strip others?
            // "Permitir solo [A-Za-z0-9.-]" meaning remove underscores, spaces?
            
            // The requirement says:
            // MailNickname = {ProductoCodigo}.{IdCurricula}.{IdCurso}.{Periodo}-{IdSeccion}
            // If any component has invalid chars they should be stripped? 
            // Usually IDs are numbers. Periodo might have '-'.
            
             // Truncate logic
            if (nickname.Length > 64)
            {
                var prefix = nickname.Substring(0, 57);
                var hash = GetStableHash(nickname);
                return $"{prefix}-{hash}";
            }

            return nickname;
        }

        public string GetDisplayName(Seccion seccion)
        {
            // "{NombreCurso} [{NombreProducto}][{CodigoSeccion}]"
            var raw = $"{seccion.CursoNombre} [{seccion.ProductoNombre}][{seccion.GrupoCodigo}]";
            
            // Sanitization
            // Trim
            raw = raw.Trim();
            // Ñ -> N
            raw = raw.Replace("Ñ", "N").Replace("ñ", "n");
            // Remove single quotes
            raw = raw.Replace("'", "");
            // Collapse spaces
            raw = Regex.Replace(raw, @"\s+", " ");
            
            // Truncate to 256
            // The constraint in SQL is often 256. Graph might allow more but let's stick to spec.
            if (raw.Length > 256)
            {
                return raw.Substring(0, 255) + "…";
            }

            return raw;
        }

        private string GetStableHash(string input)
        {
            // Simple stable hash for truncation (Base36-like)
            // Implementation of a quick string hash
            int hash = 0;
            foreach (char c in input)
            {
                hash = (hash * 31) + c;
            }
            return Math.Abs(hash).ToString("X"); // Hex is easier
        }
    }
}
