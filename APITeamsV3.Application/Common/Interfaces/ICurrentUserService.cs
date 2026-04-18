namespace APITeamsV3.Application.Common.Interfaces
{
    public interface ICurrentUserService
    {
        /// <summary>
        /// Obtiene el identificador del usuario (Email o OID en MSAL).
        /// </summary>
        string? UserId { get; }

        /// <summary>
        /// Obtiene el nombre completo o principal del usuario.
        /// </summary>
        string? UserName { get; }

        /// <summary>
        /// Obtiene un identificador numérico aproximado para auditoría en bases de datos legacy.
        /// Retorna NULL si no se puede mapear.
        /// </summary>
        int? UserIdInt { get; }
    }
}
