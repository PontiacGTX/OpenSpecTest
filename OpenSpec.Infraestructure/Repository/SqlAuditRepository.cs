
using Dapper;
using Microsoft.Data.SqlClient;
using OpenSpec.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSpec.Infraestructure.Repository
{
    public class SqlAuditRepository
    {
        private readonly string _connectionString;

        public SqlAuditRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<IEnumerable<AuditEvent>> FetchNewAuditEventsAsync(DateTime lastFetchedTime)
        {
            using var connection = new SqlConnection(_connectionString);

            // Consulta nativa sobre los archivos de auditoria de SQL Server
            string sql = @"
            SELECT 
                CAST(event_time AS DATETIME) AS EventTime,
                server_principal_name AS PrincipalName,
                client_ip AS ClientHost,
                application_name AS ApplicationName,
                action_id AS ActionType,
                schema_name AS ObjectSchema,
                object_name AS ObjectName,
                affected_rows AS RowsAffected,
                succeeded AS Succeeded,
                statement AS StatementText,
                session_id AS SessionId
            FROM sys.fn_get_audit_file('/var/opt/mssql/audit/*', DEFAULT, DEFAULT)
            WHERE event_time > @LastFetchedTime
            ORDER BY event_time ASC;";

            var rawEvents = await connection.QueryAsync(sql, new { LastFetchedTime = lastFetchedTime });

            return rawEvents.Select(e => new AuditEvent
            {
                EventId = Guid.NewGuid().ToString(),
                EventTime = e.EventTime,
                Timestamp = e.EventTime,
                PrincipalName = e.PrincipalName ?? "UNKNOWN",
                ClientHost = string.IsNullOrWhiteSpace((string)e.ClientHost) ? "127.0.0.1" : (string)e.ClientHost,
                ApplicationName = e.ApplicationName ?? "GenericClient",
                ActionType = NormalizeAction((string)e.ActionType, (string)e.StatementText),
                ObjectSchema = e.ObjectSchema ?? "dbo",
                ObjectName = e.ObjectName ?? string.Empty,
                RowsAffected = e.RowsAffected ?? 0,
                Succeeded = e.Succeeded ?? true,
                StatementText = e.StatementText ?? string.Empty,
                SessionId = e.SessionId ?? 0
            });
        }

        public async Task<IEnumerable<string>> FetchDatabasePrincipalsAsync()
        {
            using var connection = new SqlConnection(_connectionString);

            const string sql = @"
                SELECT name
                FROM sys.database_principals
                WHERE type IN ('S', 'E', 'X')
                  AND name NOT IN ('dbo', 'guest', 'INFORMATION_SCHEMA', 'sys')
                ORDER BY name;";

            return await connection.QueryAsync<string>(sql);
        }

        private static string NormalizeAction(string actionId, string statement)
        {
            if (statement?.Contains("AUDIT", StringComparison.OrdinalIgnoreCase) == true)
                return "AUDIT_CHANGE";
            if (statement?.Contains("xp_cmdshell", StringComparison.OrdinalIgnoreCase) == true)
                return "SENSITIVE_SP";

            return actionId switch
            {
                "SL" => "SELECT",
                "IN" => "INSERT",
                "UP" => "UPDATE",
                "DL" => "DELETE",
                _ => "OTHER"
            };
        }
    }
}
