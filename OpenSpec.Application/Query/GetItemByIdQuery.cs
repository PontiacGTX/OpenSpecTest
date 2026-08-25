using MediatR;
using Microsoft.EntityFrameworkCore;
using OpenSpec.Domain.Models;
using OpenSpec.Infraestructure.Repository.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSpec.Application.Query
{
    public record GetItemByIdQuery(Guid Id) : IRequest<ItemDto?>;

    public class GetItemByIdQueryHandler : IRequestHandler<GetItemByIdQuery, ItemDto?>
    {
        private readonly IApplicationDbContext _context;

        public GetItemByIdQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ItemDto?> Handle(GetItemByIdQuery request, CancellationToken cancellationToken)
        {
            return await _context.Items
                .AsNoTracking()
                .Where(x => x.Id == request.Id && x.IsActive)
                .Select(x => new ItemDto(x.Id, x.Name, x.Description, x.Price, x.IsActive, x.CreatedAtUtc))
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
