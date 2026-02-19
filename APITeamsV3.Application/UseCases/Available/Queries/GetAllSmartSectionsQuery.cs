using MediatR;
using APITeamsV3.Domain.Entities;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Available.Queries
{
    public class GetAllSmartSectionsQuery : IRequest<List<Seccion>>
    {
    }
}
