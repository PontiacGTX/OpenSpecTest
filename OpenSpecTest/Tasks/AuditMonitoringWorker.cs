using OpenSpec.Application.Services;
using OpenSpec.Domain.Models;
using OpenSpec.Infraestructure.Repository;
using Serilog;

namespace OpenSpec.API.Tasks
{
    public class AuditMonitoringWorker : BackgroundService
    {
        private readonly SqlAuditRepository _repo;
        private readonly HybridDetectionEngine _engine;
        private readonly Dictionary<string, UserProfileBaseline> _baselines;
        private DateTime _lastCheck = DateTime.UtcNow.AddMinutes(-30);

        public AuditMonitoringWorker(SqlAuditRepository repo, HybridDetectionEngine engine)
        {
            _repo = repo;
            _engine = engine;

            // Baselines simulados en memoria
            _baselines = new Dictionary<string, UserProfileBaseline>
            {
                ["analyst_user"] = new UserProfileBaseline
                {
                    PrincipalName = "analyst_user",
                    LearningWindowEnd = DateTime.UtcNow.AddDays(-1),
                    AvgRowsPerQuery = 42,
                    StdDevRowsPerQuery = 15,
                    KnownHosts = new HashSet<string> { "10.0.0.x" }
                },
                ["sa"] = new UserProfileBaseline
                {
                    PrincipalName = "sa",
                    LearningWindowEnd = DateTime.UtcNow.AddDays(-1),
                    KnownHosts = new HashSet<string> { "127.0.0.1" }
                }
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Cargar el histórico de aprendizaje antes de informar el número real de perfiles.
            _lastCheck = DateTime.UtcNow.AddDays(-7);

            try
            {
                await LoadDatabaseProfilesAsync(stoppingToken);
                await ProcessAuditBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error durante la carga inicial del histórico de auditoría.");
            }

            if (!stoppingToken.IsCancellationRequested)
                _lastCheck = DateTime.UtcNow;

            Log.Information("Motor de monitoreo iniciado | Ventana aprendizaje: {LearningWindow} | Usuarios perfilados: {ProfiledUsersCount}", "7d", _baselines.Count);

            // PeriodicTimer reemplaza el while(true) + Task.Delay en .NET 6/7/8
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ProcessAuditBatchAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    Log.Information("Deteniendo el worker de monitoreo de auditoría de forma segura...");
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error no controlado durante la ingesta o evaluación de eventos de auditoría.");
                }
            }
        }

        private async Task LoadDatabaseProfilesAsync(CancellationToken cancellationToken)
        {
            var principals = await _repo.FetchDatabasePrincipalsAsync();

            foreach (var principal in principals)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_baselines.ContainsKey(principal))
                    continue;

                _baselines[principal] = CreateLearningBaseline(principal);
            }

            Log.Information("Perfiles cargados desde SQL Server | Usuarios encontrados: {DatabaseUsers} | Perfiles totales: {ProfiledUsersCount}",
                principals.Count(), _baselines.Count);
        }

        private static UserProfileBaseline CreateLearningBaseline(string principal)
        {
            return new UserProfileBaseline
            {
                PrincipalName = principal,
                LearningWindowEnd = DateTime.UtcNow.AddDays(7),
                NormalWorkingHoursStart = new TimeSpan(8, 0, 0),
                NormalWorkingHoursEnd = new TimeSpan(19, 0, 0)
            };
        }

        private async Task ProcessAuditBatchAsync(CancellationToken cancellationToken)
        {
            var events = (await _repo.FetchNewAuditEventsAsync(_lastCheck)).ToList();
            if (!events.Any()) return;

            _lastCheck = events.Max(e => e.EventTime);
            Log.Information("Procesando eventos desde: {LastCheckTimestamp:yyyy-MM-dd HH:mm:ss} (incremental)", _lastCheck);

            var groupedSessions = events.GroupBy(e => new { e.SessionId, e.PrincipalName });
            int alertsCount = 0, critCount = 0, alertCount = 0;
            var analyzedPrincipals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in groupedSessions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sessionEvents = group.ToList();
                var principal = group.Key.PrincipalName;
                analyzedPrincipals.Add(principal);

                if (!_baselines.TryGetValue(principal, out var baseline))
                {
                    baseline = CreateLearningBaseline(principal);

                    _baselines[principal] = baseline;
                    Log.Information("Nuevo perfil en aprendizaje | Usuario: {Principal} | Ventana hasta: {LearningWindowEnd:yyyy-MM-dd HH:mm:ss}",
                        principal, baseline.LearningWindowEnd);
                }

                var (score, severity, signals) = await _engine.EvaluateSessionAsync(sessionEvents, baseline);
                if (UpdateBaselineDuringLearning(baseline, sessionEvents))
                {
                    Log.Information("INFO | baseline actualizado | {Principal} | ventana deslizante recalculada | vol_avg={AvgRows:F0} filas | hosts={KnownHostsCount} | objetos={FrequentTablesCount}",
                        principal, baseline.AvgRowsPerQuery, baseline.KnownHosts.Count, baseline.FrequentTables.Count);
                }

                if (severity != SeverityLevel.INFO)
                {
                    alertsCount++;
                    if (severity == SeverityLevel.CRIT) critCount++;
                    if (severity == SeverityLevel.ALERT) alertCount++;

                    LogAlert(severity, score, principal, group.Key.SessionId, sessionEvents, signals, baseline);
                }
            }

            Log.Information("Resumen batch | Eventos procesados: {ProcessedEvents} | Alertas: {TotalAlerts} | CRIT: {CritAlerts} | ALERT: {AlertAlerts}",
                events.Count, alertsCount, critCount, alertCount);
            Log.Information("Usuarios analizados en batch: {AnalyzedUsers} | Perfiles disponibles: {ProfiledUsersCount}",
                analyzedPrincipals.Count, _baselines.Count);
        }

        private static void LogAlert(
            SeverityLevel severity,
            double score,
            string principal,
            int sessionId,
            IReadOnlyCollection<AuditEvent> sessionEvents,
            IReadOnlyCollection<string> signals,
            UserProfileBaseline baseline)
        {
            var sampleEvent = sessionEvents.First();
            var eventIds = sessionEvents.Take(3).Select(e => e.EventId).ToList();
            var eventTrace = string.Join(", ", eventIds);
            if (sessionEvents.Count > eventIds.Count)
                eventTrace += $"... (agrupados, x{sessionEvents.Count})";

            var description = sampleEvent.ActionType switch
            {
                "AUDIT_CHANGE" => "AUDIT_CHANGE - SQL Server Audit alterada",
                "SENSITIVE_SP" => "SENSITIVE_SP - procedimiento sensible",
                _ => $"{sampleEvent.ActionType} en {sampleEvent.ObjectName} ({sampleEvent.RowsAffected:N0} filas)"
            };

            var alertTemplate =
                "{Severity} | score={Score:F0} | {Principal} | {Description} | host: {ClientHost}\n" +
                " | señales: {Signals}\n" +
                " | sesión: #{SessionId} | eventos: {EventTrace}\n" +
                " | baseline: vol_avg={AvgRows:F0} filas, known_host: {KnownHosts}";

            var writeLevel = severity == SeverityLevel.CRIT
                ? Serilog.Events.LogEventLevel.Fatal
                : Serilog.Events.LogEventLevel.Warning;

            Log.Write(writeLevel, alertTemplate,
                severity,
                score,
                principal,
                description,
                sampleEvent.ClientHost,
                string.Join(", ", signals),
                sessionId,
                eventTrace,
                baseline.AvgRowsPerQuery,
                string.Join(",", baseline.KnownHosts));
        }

        private static bool UpdateBaselineDuringLearning(
            UserProfileBaseline baseline,
            IReadOnlyCollection<AuditEvent> sessionEvents)
        {
            if (!sessionEvents.Any(e => baseline.IsColdStart(e.EventTime))) return false;

            var rows = sessionEvents.Where(e => e.RowsAffected >= 0).Select(e => (double)e.RowsAffected).ToList();
            if (rows.Count > 0)
            {
                baseline.AvgRowsPerQuery = rows.Average();
                baseline.StdDevRowsPerQuery = rows.Count > 1
                    ? Math.Sqrt(rows.Sum(row => Math.Pow(row - baseline.AvgRowsPerQuery, 2)) / rows.Count)
                    : 0;
            }

            foreach (var auditEvent in sessionEvents)
            {
                if (!string.IsNullOrWhiteSpace(auditEvent.ClientHost))
                    baseline.KnownHosts.Add(auditEvent.ClientHost);

                if (!string.IsNullOrWhiteSpace(auditEvent.ObjectName))
                    baseline.FrequentTables.Add(auditEvent.ObjectName);

                var hour = auditEvent.EventTime.Hour;
                if (!baseline.CommonAccessHours.Contains(hour))
                    baseline.CommonAccessHours.Add(hour);
            }

            return true;
        }
    }
}
