using MediatR;
using System;

namespace APITeamsV3.Application.UseCases.Provisioning.Commands
{
    public enum SessionRosterSyncType
    {
        FullSync = 0, // Options 9 (Deactivate removed) and 10 (Add new for future)
        EventSync = 1 // Option 37 (Upsert specific event)
    }

    public class SyncSessionRosterCommand : IRequest<bool>
    {
        public int IdSeccion { get; set; }
        public SessionRosterSyncType Mode { get; set; }

        // Parameters for FullSync (Options 9, 10)
        public DateTime? FechaMaximaAgendas { get; set; }

        // Parameters for EventSync (Option 37)
        public string? IdTeamsGroup { get; set; }
        public string? IdEvento { get; set; }
        public int? NumeroReunion { get; set; }
        public int? IdHorario { get; set; }
        public string? CodigoSesion { get; set; }
        public DateTime? Fecha { get; set; }
        public int? Inicio { get; set; }
        public int? Fin { get; set; }
        public string? CodigoFacilitador { get; set; }
        public string? CorreoFacilitador { get; set; } // Propietario1
        public string? JoinUrl { get; set; }

        public SyncSessionRosterCommand() { }
    }
}
