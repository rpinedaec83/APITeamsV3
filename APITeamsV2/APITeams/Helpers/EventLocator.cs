using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Graph;
using OfficeOpenXml.Drawing.Chart;


namespace APITeams.Helpers
{
    public static class EventLocator
    {
        public class LocatedEvent
        {
            public string UserIdOrUpn { get; set; } = default;
            public Event Event { get; set; } = default;
        }

        /// <summary>
        /// Intenta resolver en qué buzón (usuario) existe un Event por su Id.
        /// Estrategia: /me -> candidatos (organizador/asistentes) -> (opcional) por joinUrl.
        /// </summary>
        public static async Task<LocatedEvent> ResolveUserEventByIdAsync(
            GraphServiceClient graphClient,
            string eventId,
            IEnumerable<string> candidateUserEmails = null,
            string joinWebUrl = null)
        {
            // 1) Probar en /me
            try
            {
                var meEvt = await graphClient.Me.Events[eventId].Request().GetAsync();
                if (meEvt != null)
                    return new LocatedEvent { UserIdOrUpn = "me", Event = meEvt };
            }
            catch { /* 404/Forbidden -> continuar */ }

            // 2) Probar con candidatos (organizador y/o asistentes)
            if (candidateUserEmails != null)
            {
                foreach (var email in candidateUserEmails.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var ev = await graphClient.Users[email].Events[eventId].Request().GetAsync();
                        if (ev != null)
                            return new LocatedEvent { UserIdOrUpn = email, Event = ev };
                    }
                    catch { /* continuar */ }
                }
            }

            // 3) (Opcional) Resolver por joinUrl -> organizer -> buscar el evento
            if (!string.IsNullOrWhiteSpace(joinWebUrl))
            {
                try
                {
                    // Obtener onlineMeeting para conocer el organizador
                    var q = await graphClient.Communications.OnlineMeetings
                    .Request()
            .Filter($"JoinWebUrl eq '{joinWebUrl}'")
            .Top(1)
            .GetAsync()
            .ConfigureAwait(false);

                    var om = q.CurrentPage.FirstOrDefault();
                    var organizer = om.Participants.Organizer.Upn ?? om.Participants.Organizer.AdditionalData?["id"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(organizer))
                    {
                        // El ID del onlineMeeting no es igual al EventId del calendario.
                        // Buscamos el evento del organizador en una ventana alrededor de la fecha.
                        var start = om.StartDateTime?.AddHours(-12) ?? DateTimeOffset.UtcNow.AddDays(-7);
                        var end = om.EndDateTime?.AddHours(+12) ?? DateTimeOffset.UtcNow.AddDays(+7);

                        var view = await graphClient.Users[organizer].CalendarView.Request()
                            .GetAsync();

                        var match = view.CurrentPage
                            .FirstOrDefault(e =>
                                (e.OnlineMeeting?.JoinUrl ?? e.OnlineMeetingUrl) == joinWebUrl
                                || string.Equals(e.Subject, om.Subject, StringComparison.OrdinalIgnoreCase));

                        if (match != null)
                            return new LocatedEvent { UserIdOrUpn = organizer, Event = match };
                    }
                }
                catch { /* continuar */ }
            }

            return null; // no se pudo localizar
        }



        public static async Task DeleteUserEventAsync(
            GraphServiceClient graphClient,
            string userIdOrUpn,
            string eventId)
        {
            try
            {
                await graphClient.Me.Calendar.Events[eventId].Request().DeleteAsync();
            }
            catch (Exception ex)
            {

                Console.WriteLine(ex.Message);
            }
        }
    }
}
