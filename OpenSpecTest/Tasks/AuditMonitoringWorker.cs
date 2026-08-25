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

        private async Task ProcessAuditBatchAsync(CancellationToken cancellationToken)
        {
            var events = (await _repo.FetchNewAuditEventsAsync(_lastCheck)).ToList();
            if (!events.Any()) return;

            _lastCheck = events.Max(e => e.EventTime);
            Log.Information("Procesando eventos desde: {LastCheckTimestamp:yyyy-MM-dd HH:mm:ss} (incremental)", _lastCheck);

            var groupedSessions = events.GroupBy(e => new { e.SessionId, e.PrincipalName });
            int alertsCount = 0, critCount = 0, alertCount = 0;

            foreach (var group in groupedSessions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sessionEvents = group.ToList();
                var principal = group.Key.PrincipalName;

                if (!_baselines.TryGetValue(principal, out var baseline))
                {
                    baseline = new UserProfileBaseline
                    {
                        PrincipalName = principal,
                        LearningWindowEnd = DateTime.UtcNow.AddDays(7)
                    };
                }

                var (score, severity, signals) = await _engine.EvaluateSessionAsync(sessionEvents, baseline);

                if (severity != SeverityLevel.INFO)
                {
                    alertsCount++;
                    if (severity == SeverityLevel.CRIT) critCount++;
                    if (severity == SeverityLevel.ALERT) alertCount++;

                    var sampleEvt = sessionEvents.First();
                    string countSuffix = sessionEvents.Count > 1 ? $" (agrupados, x{sessionEvents.Count})" : "";
                    var rawEventIds = sessionEvents.Take(3).Select(e => e.EventId);

                    string alertTemplate =
                        "\n{Severity} | score={Score:F0} | {Principal} | {ActionType} en {ObjectName} | host: {ClientHost}\n" +
                        " | señales : {Signals}\n" +
                        " | sesión  : #{SessionId} | eventos: {RawEventIds}{CountSuffix}\n" +
                        " | baseline: vol_avg={AvgRows} filas, known_host: {KnownHosts}";

                    object[] messageArgs = new object[]
                    {
                    severity,
                    score,
                    principal,
                    sampleEvt.ActionType,
                    sampleEvt.ObjectName,
                    sampleEvt.ClientHost,
                    string.Join(", ", signals),
                    group.Key.SessionId,
                    string.Join(", ", rawEventIds),
                    countSuffix,
                    baseline.AvgRowsPerQuery,
                    string.Join(",", baseline.KnownHosts)
                    };

                    if (severity == SeverityLevel.CRIT)
                    {
                        Log.Fatal(alertTemplate, messageArgs);
                    }
                    else
                    {
                        Log.Warning(alertTemplate, messageArgs);
                    }
                }
            }

            Log.Information("Resumen batch | Eventos procesados: {ProcessedEvents} | Alertas: {TotalAlerts} | CRIT: {CritAlerts} | ALERT: {AlertAlerts}",
                events.Count, alertsCount, critCount, alertCount);
        }
    }
}
