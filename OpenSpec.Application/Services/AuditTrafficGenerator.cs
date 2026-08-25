using Microsoft.Data.SqlClient;
using OpenSpec.Domain.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSpec.Application.Services
{
    public class AuditTrafficGenerator
    {
        private readonly string _connectionString;

        public AuditTrafficGenerator(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task RunScenarioAsync(TrafficScenario scenario, int iterations = 5, CancellationToken ct = default)
        {
            Log.Information("Iniciando Harness de Generación C# | Escenario: {Scenario} | Iteraciones: {Iterations}", scenario, iterations);

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            for (int i = 0; i < iterations; i++)
            {
                if (ct.IsCancellationRequested) break;

                switch (scenario)
                {
                    case TrafficScenario.NormalAnalystActivity:
                        await ExecuteNormalQueryAsync(conn);
                        break;

                    case TrafficScenario.DataExfiltrationAttempt:
                        await ExecuteExfiltrationQueryAsync(conn);
                        break;

                    case TrafficScenario.UnusualHostAccess:
                        await ExecuteUnusualHostQueryAsync();
                        break;

                    case TrafficScenario.SqlInjectionProbe:
                        await ExecuteSqlInjectionAttemptAsync(conn);
                        break;

                    case TrafficScenario.FullSimulation:
                        if (i % 4 == 0)
                            await ExecuteExfiltrationQueryAsync(conn);
                        else
                            await ExecuteNormalQueryAsync(conn);
                        break;
                }

                await Task.Delay(500, ct); // Pausa entre peticiones
            }

            Log.Information("Harness de Generación completado exitosamente.");
        }

        private static async Task ExecuteNormalQueryAsync(SqlConnection conn)
        {
            // Simula consulta habitual de analista (40-50 filas)
            string sql = "SELECT TOP 42 * FROM sys.objects WHERE type = 'U';";
            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) { /* Consumir buffer */ }
        }

        private static async Task ExecuteExfiltrationQueryAsync(SqlConnection conn)
        {
            // Simula exfiltración masiva de datos (10,000+ filas o vistas sensibles)
            string sql = "SELECT TOP 10000 o1.name, o2.name FROM sys.all_objects o1 CROSS JOIN sys.all_objects o2;";
            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) { /* Consumir buffer */ }
        }

        private async Task ExecuteUnusualHostQueryAsync()
        {
            // Para simular un Host o App Name distinto, abrimos una conexión cambiando la propiedad Workstation ID o Application Name en el ConnectionString
            var builder = new SqlConnectionStringBuilder(_connectionString)
            {
                //WorkStationID = "UNTRUSTED-NODE-99",
                ApplicationName = "UnsanctionedDBeaverClient"
            };

            using var conn = new SqlConnection(builder.ConnectionString);
            await conn.OpenAsync();

            string sql = "SELECT TOP 10 * FROM sys.tables;";
            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) { /* Consumir buffer */ }
        }

        private static async Task ExecuteSqlInjectionAttemptAsync(SqlConnection conn)
        {
            // Patronessospechosos que disparan señales semánticas/de reglas
            string sql = "SELECT * FROM sys.tables WHERE name = 'users' OR '1'='1'; -- UNION SELECT NULL, @@version";
            try
            {
                using var cmd = new SqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) { }
            }
            catch
            {
                // Ocultar error provocado intencionalmente para la auditoría
            }
        }
    }
}