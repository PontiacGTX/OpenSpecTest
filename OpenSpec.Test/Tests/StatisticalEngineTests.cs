using FluentAssertions;
using OpenSpec.Application.Services;
using OpenSpec.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace OpenSpec.Test.Tests
{
    public class RuleEngineTests
    {
        private readonly ThresholdRuleEngine _ruleEngine;

        public RuleEngineTests()
        {
            _ruleEngine = new ThresholdRuleEngine();
        }

        [Fact]
        public void Evaluate_ShouldTriggerCriticalAlert_WhenAuditIsDisabled()
        {
            // Arrange: Evento de alteración de auditoría (AUDIT_CHANGE) por usuario 'sa'
            var auditEvent = new AuditEvent
            {
                EventId = "evt-8901",
                Timestamp = DateTime.UtcNow,
                PrincipalName = "sa",
                ActionType = "AUDIT_CHANGE",
                StatementText = "ALTER SERVER AUDIT [DataAudit] WITH (STATE = OFF);",
                Succeeded = true
            };

            // Act
            var signals = _ruleEngine.Evaluate(auditEvent, userBaseline: null);

            // Assert
            signals.Should().ContainSingle(s => s.Type == SignalType.AuditTamper)
                   .Which.Severity.Should().Be(AlertSeverity.Critical);
        }

        [Theory]
        [InlineData("2026-08-09 03:15:00", true, "Acceso de madrugada debe disparar OffHours")]
        [InlineData("2026-08-09 14:00:00", false, "Acceso en horario laboral no debe disparar OffHours")]
        public void Evaluate_ShouldDetectOffHoursAccess_BasedOnTimestamp(string timestampStr, bool shouldTrigger, string reason)
        {
            // Arrange
            var eventTime = DateTime.Parse(timestampStr);
            var auditEvent = new AuditEvent
            {
                EventId = "evt-101",
                Timestamp = eventTime,
                PrincipalName = "analyst_user",
                ActionType = "SELECT",
                ClientHost = "10.0.0.15"
            };

            var userBaseline = new UserProfileBaseline
            {
                PrincipalName = "analyst_user",
                NormalWorkingHoursStart = new TimeSpan(8, 0, 0),
                NormalWorkingHoursEnd = new TimeSpan(19, 0, 0)
            };

            // Act
            var signals = _ruleEngine.Evaluate(auditEvent, userBaseline);

            // Assert
            if (shouldTrigger)
            {
                signals.Should().Contain(s => s.Type == SignalType.OffHours, reason);
            }
            else
            {
                signals.Should().NotContain(s => s.Type == SignalType.OffHours, reason);
            }
        }

        [Fact]
        public void Evaluate_ShouldTriggerUnknownHost_WhenHostIsNotInBaseline()
        {
            // Arrange
            var auditEvent = new AuditEvent 
            {
                EventId = "evt-102",
                Timestamp = DateTime.UtcNow,
                PrincipalName = "analyst_user",
                ClientHost = "192.168.1.99" // Host no registrado previamente
            };

            var userBaseline = new UserProfileBaseline
            {
                PrincipalName = "analyst_user",
                KnownHosts = new HashSet<string> { "10.0.0.5", "10.0.0.6" }
            };

            // Act
            var signals = _ruleEngine.Evaluate(auditEvent, userBaseline);

            // Assert
            signals.Should().ContainSingle(s => s.Type == SignalType.UnknownHost)
                   .Which.Details.Should().Contain("192.168.1.99");
        }
    }
}
