using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSpec.Domain.Models
{
    public class AlertRecord
    {
        public Guid AlertId { get; set; } = Guid.NewGuid();
        public int SessionId { get; set; }
        public string PrincipalName { get; set; } = string.Empty;
        public string ClientHost { get; set; } = string.Empty;
        public double CompositeScore { get; set; }
        public SeverityLevel Severity { get; set; }
        public AlertStatus Status { get; set; } = AlertStatus.New;
        public List<string> TriggeredSignals { get; set; } = new();
        public List<string> RawEventIds { get; set; } = new();
        public int GroupedEventCount { get; set; } = 1;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string Description { get; set; } = string.Empty;
    }
}
