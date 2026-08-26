using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenSpec.Domain.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace OpenSpec.Application.Services
{
    public class HybridDetectionEngine
    {
        private readonly IChatCompletionService? _chatService;
        private readonly string _ollamaModel;
        private readonly string _ollamaEndpoint;

        public HybridDetectionEngine(
            string? ollamaEndpoint = "http://localhost:11434/v1",
            string ollamaModel = "llama3.2:3b")
        {
            _ollamaModel = ollamaModel;
            _ollamaEndpoint = ollamaEndpoint ?? string.Empty;

            Log.Information("Ollama configurado | endpoint={Endpoint} | modelo={Model}", _ollamaEndpoint, _ollamaModel);

            if (!string.IsNullOrEmpty(ollamaEndpoint))
            {
                // Construir Semantic Kernel apuntando al endpoint OpenAI-compatible de Ollama
                var builder = Kernel.CreateBuilder();
                builder.AddOpenAIChatCompletion(_ollamaModel, apiKey: "ollama", httpClient: new HttpClient
                {
                    BaseAddress = new Uri(ollamaEndpoint)
                });
                var kernel = builder.Build();
                _chatService = kernel.GetRequiredService<IChatCompletionService>();
            }
        }

        public async Task<(double Score, SeverityLevel Severity, List<string> Signals)> EvaluateSessionAsync(
            List<AuditEvent> sessionEvents,
            UserProfileBaseline baseline)
        {
            double totalScore = 0.0;
            var signals = new List<string>();
            var highestSeverity = SeverityLevel.INFO;

            foreach (var evt in sessionEvents)
            {
                // 1. REGLAS DETERMINÍSTICAS (Reglas Fijas)
                if (evt.ActionType == "AUDIT_CHANGE")
                {
                    signals.Add("AuditTamper (regla fija - sin umbral)");
                    totalScore = Math.Max(totalScore, 100.0);
                    highestSeverity = SeverityLevel.CRIT;
                }

                if (evt.StatementText.Contains("xp_cmdshell", StringComparison.OrdinalIgnoreCase))
                {
                    signals.Add("SensitiveSpExec(xp_cmdshell)");
                    totalScore = Math.Max(totalScore, 95.0);
                    highestSeverity = SeverityLevel.CRIT;
                }

                if (!baseline.KnownHosts.Contains(evt.ClientHost) && !baseline.IsColdStart(evt.EventTime))
                {
                    signals.Add($"UnknownHost({evt.ClientHost})");
                    totalScore += 25.0;
                }

                // 2. DETECCIÓN ESTADÍSTICA (Z-Score sobre volumen)
                if (!baseline.IsColdStart(evt.EventTime) && evt.RowsAffected > 0)
                {
                    double stdDev = baseline.StdDevRowsPerQuery <= 0.001 ? 10.0 : baseline.StdDevRowsPerQuery; // Evitar division por 0
                    double zScore = (evt.RowsAffected - baseline.AvgRowsPerQuery) / stdDev;

                    if (zScore > 3.0)
                    {
                        signals.Add($"VolumeAnomaly(+{zScore:F1}σ)");
                        totalScore += Math.Min(40.0, zScore * 10);
                    }
                }
            }

            // 3. DETECCIÓN SEMÁNTICA (Semantic Kernel + Ollama LLM Plugin)
            var suspiciousQueries = sessionEvents.Where(e => e.StatementText.Length > 20).Select(e => e.StatementText).Take(3).ToList();
            if (_chatService != null && suspiciousQueries.Any())
            {
                double semanticScore = await AnalyzeSqlWithOllamaAsync(suspiciousQueries);
                if (semanticScore > 0.6)
                {
                    signals.Add($"SemanticAnomalousQuery(confidence={semanticScore:F2})");
                    totalScore += semanticScore * 20.0;
                }
            }
            else if (suspiciousQueries.Any())
            {
                Log.Warning("Análisis Ollama no disponible | consultas pendientes: {QueryCount}", suspiciousQueries.Count);
            }

            // Ajustar Severidad
            if (totalScore >= 90.0) highestSeverity = SeverityLevel.CRIT;
            else if (totalScore >= 60.0) highestSeverity = SeverityLevel.ALERT;

            return (Math.Min(100.0, totalScore), highestSeverity, signals.Distinct().ToList());
        }

        private async Task<double> AnalyzeSqlWithOllamaAsync(List<string> sqlStatements)
        {
            try
            {
                var history = new ChatHistory();
                history.AddSystemMessage("Eres un analista de ciberseguridad experto en SQL Server. Analiza las siguientes sentencias SQL y responde ÚNICAMENTE con un número flotante entre 0.0 y 1.0 indicando la probabilidad de que sea una consulta maliciosa o de exfiltración evasiva.");
                history.AddUserMessage($"Queries a evaluar:\n{string.Join("\n", sqlStatements)}");

                var response = await _chatService!.GetChatMessageContentAsync(history);
                var content = response.Content?.Trim() ?? string.Empty;
                if (double.TryParse(content, System.Globalization.CultureInfo.InvariantCulture, out double result))
                {
                    result = Math.Clamp(result, 0.0, 1.0);
                    Log.Information("Análisis Ollama completado | modelo={Model} | consultas={QueryCount} | confianza={Confidence:F2}",
                        _ollamaModel, sqlStatements.Count, result);
                    return result;
                }

                var numericMatch = Regex.Match(content, @"(?<![\d.])(?:0(?:\.\d+)?|1(?:\.0+)?)(?![\d.])");
                if (numericMatch.Success && double.TryParse(numericMatch.Value, System.Globalization.CultureInfo.InvariantCulture, out result))
                {
                    Log.Information("Análisis Ollama completado | modelo={Model} | consultas={QueryCount} | confianza={Confidence:F2}",
                        _ollamaModel, sqlStatements.Count, result);
                    return result;
                }

                Log.Warning("Ollama respondió un valor no numérico | modelo={Model} | respuesta={Response}",
                    _ollamaModel, content);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "No se pudo consultar Ollama | endpoint={Endpoint} | modelo={Model}", _ollamaEndpoint, _ollamaModel);
            }
            return 0.0;
        }
    }
}
