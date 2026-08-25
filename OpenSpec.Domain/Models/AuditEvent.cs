using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSpec.Domain.Models
{
    public record AuditEvent
    {
        public string EventId { get; init; } = Guid.NewGuid().ToString();
        public DateTime EventTime { get; init; }
        public string PrincipalName { get; init; } = string.Empty;
        public string ClientHost { get; init; } = string.Empty;
        public string ApplicationName { get; init; } = string.Empty;
        public string ActionType { get; init; } = string.Empty; // SELECT, INSERT, DDL, AUDIT_CHANGE
        public string ObjectSchema { get; init; } = string.Empty;
        public string ObjectName { get; init; } = string.Empty;
        public long RowsAffected { get; init; }
        public bool Succeeded { get; init; }
        public string StatementText { get; init; } = string.Empty;
        public int SessionId { get; init; }
        public DateTime Timestamp { get; set; }
    }
}
