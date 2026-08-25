using MediatR;
using Microsoft.EntityFrameworkCore;
using OpenSpec.Infraestructure.Repository.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSpec.Application.Command
{
    public record UpdateItemCommand(
    Guid Id,
    string Name,
    string Description,
    decimal Price
) : IRequest;

    public class UpdateItemCommandHandler : IRequestHandler<UpdateItemCommand>
    {
        private readonly IApplicationDbContext _context;

        public UpdateItemCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(UpdateItemCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Items
                .FirstOrDefaultAsync(x => x.Id == request.Id && x.IsActive, cancellationToken);

            if (entity is null)
            {
                throw new KeyNotFoundException($"No se encontró la entidad Item con ID '{request.Id}'.");
            }

            entity.Update(request.Name, request.Description, request.Price);

            await  _context.SaveChangesAsync(cancellationToken);
        }
    }
}
