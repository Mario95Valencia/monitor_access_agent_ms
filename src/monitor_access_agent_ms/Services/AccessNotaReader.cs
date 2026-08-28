using System.Data;
using System.Data.OleDb;
using monitor_access_agent_ms.Configuration;
using monitor_access_agent_ms.Models;

namespace monitor_access_agent_ms.Services;

public sealed class AccessNotaReader : IAccessNotaReader
{
    public async Task<IReadOnlyList<ResultadoLecturaPunto>> ObtenerUltimasNotasAsync(
        FuenteAccessOptions fuente,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(fuente.AccessPath))
            throw new FileNotFoundException("No se encontró la base Access configurada.", fuente.AccessPath);

        var connectionString = new OleDbConnectionStringBuilder
        {
            Provider = "Microsoft.ACE.OLEDB.16.0",
            DataSource = fuente.AccessPath
        };
        if (!string.IsNullOrWhiteSpace(fuente.AccessPassword))
            connectionString["Jet OLEDB:Database Password"] = fuente.AccessPassword;
        connectionString["Mode"] = "Share Deny None";

        await using var connection = new OleDbConnection(connectionString.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var resultados = new List<ResultadoLecturaPunto>(fuente.Puntos.Count);
        foreach (var punto in fuente.Puntos)
        {
            try
            {
                var nota = await ObtenerUltimaNotaAsync(connection, punto, cancellationToken);
                resultados.Add(new ResultadoLecturaPunto(punto, nota, null));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                resultados.Add(new ResultadoLecturaPunto(punto, null, exception));
            }
        }

        return resultados;
    }

    private static async Task<UltimaNota> ObtenerUltimaNotaAsync(
        OleDbConnection connection,
        PuntoOptions punto,
        CancellationToken cancellationToken)
    {

        const string sql = """
            SELECT TOP 1 Trim(numfac), Trim(caja), fecha, hora
            FROM Nota
            WHERE Trim(caja) = ?
              AND tipdoc = ?
              AND Len(Trim(numfac)) = 10
              AND Left(Trim(numfac), 3) = ?
            ORDER BY CLng(Mid(Trim(numfac), 4)) DESC
            """;

        await using var command = new OleDbCommand(sql, connection);
        // En OleDb los parámetros son posicionales, independientemente de su nombre.
        command.Parameters.Add("@caja", OleDbType.VarWChar, 3).Value = punto.Caja;
        command.Parameters.Add("@tipdoc", OleDbType.SmallInt).Value = 3;
        command.Parameters.Add("@prefijo", OleDbType.VarWChar, 3).Value = punto.Caja;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (reader is null || !await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException($"No se encontraron notas válidas para la caja {punto.Caja}.");

        var numeroDocumento = reader.GetString(0).Trim();
        if (!TryObtenerSecuencial(numeroDocumento, punto.Caja, out var secuencial))
            throw new InvalidDataException($"El número de documento '{numeroDocumento}' no tiene el formato esperado.");

        var fecha = reader.IsDBNull(2) ? DateTime.Today : reader.GetDateTime(2).Date;
        var hora = reader.IsDBNull(3) ? TimeSpan.Zero : reader.GetDateTime(3).TimeOfDay;
        return new UltimaNota(numeroDocumento, reader.GetString(1).Trim(), secuencial, fecha.Add(hora));
    }

    internal static bool TryObtenerSecuencial(string? numeroDocumento, string caja, out long secuencial)
    {
        secuencial = 0;
        return numeroDocumento is not null && numeroDocumento.Length == 10 &&
               numeroDocumento.StartsWith(caja, StringComparison.Ordinal) &&
               numeroDocumento.All(char.IsDigit) &&
               long.TryParse(numeroDocumento[3..], out secuencial);
    }
}
