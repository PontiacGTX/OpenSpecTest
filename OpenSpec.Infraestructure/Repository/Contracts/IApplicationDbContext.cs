using Microsoft.EntityFrameworkCore;
using OpenSpec.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSpec.Infraestructure.Repository.Contracts
{
    public interface IApplicationDbContext
    {
        DbSet<Item> Items { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    }
}
