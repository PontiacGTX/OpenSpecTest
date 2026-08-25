using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSpec.Domain.Models
{
    public enum SeverityLevel { INFO, ALERT, CRIT }
    public enum AlertStatus { New, Reviewed, FalsePositive, Confirmed }

    public enum SignalType
    {
        AuditTamper,
        OffHours,
        UnknownHost,
        VolumeAnomaly,
        BruteForce,
        SensitiveDataAccess,
        UnusualRole
    }

    public enum AlertSeverity
    {
        Info,
        Low,
        Medium,
        High,
        Critical
    }
}
