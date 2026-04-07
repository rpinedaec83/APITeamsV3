using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;

namespace APITeamsV3.Infrastructure.Services
{
    public class NamingService : INamingService
    {
        public string GetMailNickname(Seccion seccion)
        {
            // Format: {ProductoCodigo}.{IdCurricula}.{IdCurso}.{Periodo}-{CodigoSeccion}
            // Example: 0324.313.1610.2025-I-01425.26.00184
            //
            // "CodigoSeccion" must prefer Seccion.Codigo.
            // If it is unavailable, we fall back to GrupoCodigo and finally to IdSeccion.

            var sb = new StringBuilder();
            sb.Append($"{SanitizeComponent(seccion.ProductoCodigo)}.");
            sb.Append($"{SanitizeComponent(seccion.IdCurricula.ToString())}.");
            sb.Append($"{SanitizeComponent(seccion.IdCurso.ToString())}.");
            sb.Append($"{SanitizeComponent(seccion.CodigoPeriodo)}-");
            sb.Append($"{SanitizeComponent(GetSectionCode(seccion))}");

            var nickname = sb.ToString();
            
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
            var raw = $"{seccion.CursoNombre} [{seccion.ProductoNombre}][{GetSectionCode(seccion)}]";
            
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

        private static string GetSectionCode(Seccion seccion)
        {
            if (!string.IsNullOrWhiteSpace(seccion.Codigo))
            {
                return seccion.Codigo.Trim();
            }

            if (!string.IsNullOrWhiteSpace(seccion.GrupoCodigo))
            {
                return seccion.GrupoCodigo.Trim();
            }

            return seccion.IdSeccion.ToString();
        }

        private static string SanitizeComponent(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return new string(value.Trim().Where(ch => char.IsLetterOrDigit(ch) || ch == '.' || ch == '-').ToArray());
        }
    }
}
