using MediatR;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public record SyncSectionMembersCommand(int IdSeccion, string? ExecutedBy = null) : IRequest<SyncSectionMembersResult>;

    public class SyncSectionMembersResult
    {
        public bool Success { get; set; }
        public int FacilitatorsProcessed { get; set; }
        public int StudentsAddedProcessed { get; set; }
        public int StudentsRemovedProcessed { get; set; }
        public int StudentsVerifiedInGraph { get; set; }
        public string Summary { get; set; } = string.Empty;
    }
}
