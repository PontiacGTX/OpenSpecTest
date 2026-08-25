using MediatR;
using OpenSpec.Domain.Models;
using OpenSpec.Infraestructure.Repository.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSpec.Application.Command
{
    public record CreateItemCommand(
    string Name,
    string Description,
    decimal Price
    ) : IRequest<Guid>;

    public class CreateItemCommandHandler : IRequestHandler<CreateItemCommand, Guid>
    {
        private readonly IApplicationDbContext _context;

        public CreateItemCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Guid> Handle(CreateItemCommand request, CancellationToken cancellationToken)
        {
            var entity = new Item(request.Name, request.Description, request.Price);

            _context.Items.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return entity.Id;
        }

    }
}