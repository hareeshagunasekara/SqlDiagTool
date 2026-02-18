using Microsoft.Data.SqlClient;

namespace SqlDiagTool.Checks;

// Shared connection and query execution; one place for reuse across checks.
internal static class SqlHelper
{
    public static async Task<List<string[]>> RunQueryAsync(string connectionString, string sql, int commandTimeoutSeconds = 10)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = commandTimeoutSeconds };
        var rows = new List<string[]>();
        await using var reader = await cmd.ExecuteReaderAsync();
        var colCount = reader.FieldCount;
        while (await reader.ReadAsync())
        {
            var row = new string[colCount];
            for (var i = 0; i < colCount; i++)
                row[i] = reader.IsDBNull(i) ? "" : (reader.GetValue(i)?.ToString() ?? "");
            rows.Add(row);
        }
        return rows;
    }
}
