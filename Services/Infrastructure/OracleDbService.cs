using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;

namespace JoSystem.Services
{
    public static class OracleDbService
    {
        public static async Task<List<T>> QueryAsync<T>(
            string connectionName,
            string sql,
            System.Func<IDataRecord, T> map,
            params OracleParameter[] parameters)
        {
            var config = MultiDbService.GetConnection(connectionName);
            if (config == null || string.IsNullOrWhiteSpace(config.ConnectionString))
            {
                throw new System.InvalidOperationException($"Connection {connectionName} not configured.");
            }

            var result = new List<T>();
            using var conn = new OracleConnection(config.ConnectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (parameters != null && parameters.Length > 0)
            {
                cmd.Parameters.AddRange(parameters);
            }

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(map(reader));
            }

            return result;
        }
    }
}

