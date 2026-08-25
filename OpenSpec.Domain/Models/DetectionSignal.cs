using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSpec.Domain.Models
{
    public record DetectionSignal(
        SignalType Type,
        double Weight,
        string Details,
        AlertSeverity Severity = AlertSeverity.Medium,
        double ScoreImpact = 0
    );
}
