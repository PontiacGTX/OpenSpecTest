using OpenSpec.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSpec.Application.Services
{
    public class ThresholdRuleEngine
    {
        public List<DetectionSignal> Evaluate(AuditEvent auditEvent, UserProfileBaseline? userBaseline)
        {
            var signals = new List<DetectionSignal>();

            // Regla fija: Alteración de Auditoría
            if (auditEvent.ActionType.Equals("AUDIT_CHANGE", StringComparison.OrdinalIgnoreCase) ||
                auditEvent.StatementText.Contains("STATE = OFF", StringComparison.OrdinalIgnoreCase))
            {
                signals.Add(new DetectionSignal(
                    SignalType.AuditTamper,
                    Weight: 100,
                    Details: "Inactivación o alteración de SQL Server Audit",
                    Severity: AlertSeverity.Critical,
                    ScoreImpact: 100
                ));
            }

            // Regla de Horario Inusual
            if (userBaseline != null)
            {
                var timeOfDay = auditEvent.Timestamp.TimeOfDay;
                if (timeOfDay < userBaseline.NormalWorkingHoursStart || timeOfDay > userBaseline.NormalWorkingHoursEnd)
                {
                    signals.Add(new DetectionSignal(
                        SignalType.OffHours,
                        Weight: 25,
                        Details: $"Acceso fuera de horario habitual ({auditEvent.Timestamp:HH:mm:ss})",
                        Severity: AlertSeverity.Medium,
                        ScoreImpact: 25
                    ));
                }

                // Regla de Host Desconocido
                if (!string.IsNullOrEmpty(auditEvent.ClientHost) &&
                    !userBaseline.KnownHosts.Contains(auditEvent.ClientHost))
                {
                    signals.Add(new DetectionSignal(
                        SignalType.UnknownHost,
                        Weight: 30,
                        Details: $"Acceso desde IP/Host no registrado: {auditEvent.ClientHost}",
                        Severity: AlertSeverity.High,
                        ScoreImpact: 30
                    ));
                }
            }

            return signals;
        }
    }
}
