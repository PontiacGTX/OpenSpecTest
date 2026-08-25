using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSpec.Domain.Models
{
    public class Item
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public decimal Price { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public DateTime? LastModifiedAtUtc { get; private set; }

        // Constructor privado para EF Core
        private Item() { }

        public Item(string name, string description, decimal price)
        {
            Id = Guid.NewGuid();
            Name = GuardAgainstEmpty(name, nameof(name));
            Description = description ?? string.Empty;
            Price = GuardAgainstNegative(price, nameof(price));
            IsActive = true;
            CreatedAtUtc = DateTime.UtcNow;
        }

        public void Update(string name, string description, decimal price)
        {
            Name = GuardAgainstEmpty(name, nameof(name));
            Description = description ?? string.Empty;
            Price = GuardAgainstNegative(price, nameof(price));
            LastModifiedAtUtc = DateTime.UtcNow;
        }

        public void Deactivate()
        {
            IsActive = false;
            LastModifiedAtUtc = DateTime.UtcNow;
        }

        private static string GuardAgainstEmpty(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("El valor no puede estar vacío.", paramName);
            return value.Trim();
        }

        private static decimal GuardAgainstNegative(decimal value, string paramName)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(paramName, "El precio no puede ser negativo.");
            return value;
        }
    }

    public record ItemDto(
        Guid Id,
        string Name,
        string Description,
        decimal Price,
        bool IsActive,
        DateTime CreatedAtUtc
    );

    public record PagedResult<T>(
        IReadOnlyCollection<T> Items,
        int PageNumber,
        int PageSize,
        int TotalCount
    )
    {
        public bool HasNextPage => PageNumber * PageSize < TotalCount;
        public bool HasPreviousPage => PageNumber > 1;
    }
   
}
