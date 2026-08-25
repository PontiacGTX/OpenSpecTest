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
    public record DeleteItemCommand(Guid Id) : IRequest;

    public class DeleteItemCommandHandler : IRequestHandler<DeleteItemCommand>
    {
        private readonly IApplicationDbContext _context;

        public DeleteItemCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(DeleteItemCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Items
                .FirstOrDefaultAsync(x => x.Id == request.Id && x.IsActive, cancellationToken);

            if (entity is null)
            {
                throw new KeyNotFoundException($"No se encontró la entidad Item con ID '{request.Id}'.");
            }

            // Soft Delete (Baja Lógica)
            entity.Deactivate();

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
