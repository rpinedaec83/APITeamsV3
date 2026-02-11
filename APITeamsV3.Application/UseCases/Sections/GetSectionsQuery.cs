using APITeamsV3.Application.UseCases.Sections;
using MediatR;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Sections
{
    public class GetSectionsQuery : IRequest<List<SectionDetailDto>>
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
