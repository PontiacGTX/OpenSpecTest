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
    
            public record GetItemsQuery(
           int PageNumber = 1,
           int PageSize = 10,
           string? SearchTerm = null
         ) : IRequest<PagedResult<ItemDto>>;

        public class GetItemsQueryHandler : IRequestHandler<GetItemsQuery, PagedResult<ItemDto>>
        {
            private readonly IApplicationDbContext _context;

            public GetItemsQueryHandler(IApplicationDbContext context)
            {
                _context = context;
            }

            public async Task<PagedResult<ItemDto>> Handle(GetItemsQuery request, CancellationToken cancellationToken)
            {
                var query = _context.Items.AsNoTracking().Where(x => x.IsActive);

                if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                {
                    var term = request.SearchTerm.Trim().ToLower();
                    query = query.Where(x => x.Name.ToLower().Contains(term) || x.Description.ToLower().Contains(term));
                }

                var totalCount = await query.CountAsync(cancellationToken);

                var items = await query
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .Skip((request.PageNumber - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .Select(x => new ItemDto(x.Id, x.Name, x.Description, x.Price, x.IsActive, x.CreatedAtUtc))
                    .ToListAsync(cancellationToken);

                return new PagedResult<ItemDto>(items, request.PageNumber, request.PageSize, totalCount);
            }
        }
    }

