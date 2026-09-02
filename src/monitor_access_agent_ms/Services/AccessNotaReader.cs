using System.Data;
using System.Data.OleDb;
using monitor_access_agent_ms.Configuration;
using monitor_access_agent_ms.Models;

namespace monitor_access_agent_ms.Services;

public sealed class AccessNotaReader : IAccessNotaReader
{
    private static readonly string[] Providers =
    [
        "Microsoft.ACE.OLEDB.16.0",
        "Microsoft.ACE.OLEDB.12.0",
        "Microsoft.Jet.OLEDB.4.0"
    ];

    public async Task<IReadOnlyList<ResultadoLecturaPunto>> ObtenerUltimasNotasAsync(
        FuenteAccessOptions fuente,
        CancellationToken cancellationToken)
    {
        var resultados = await ConsultarBaseAsync(
            fuente.AccessPath, fuente.AccessPassword, fuente.Tabla, fuente.Puntos, cancellationToken);

        if (string.IsNullOrWhiteSpace(fuente.FallbackAccessPath))
            return resultados;

        var alternativos = await ConsultarBaseAsync(
            fuente.FallbackAccessPath, fuente.FallbackAccessPassword, fuente.FallbackTabla,
            fuente.Puntos, cancellationToken);
        var alternativoPorPunto = alternativos.ToDictionary(x => x.Punto);

        return resultados.Select(resultado =>
        {
            var alternativo = alternativoPorPunto[resultado.Punto];
            ResultadoLecturaPunto seleccionado;
            if (resultado.Nota is not null && alternativo.Nota is not null)
                seleccionado = resultado.Nota.Secuencial >= alternativo.Nota.Secuencial
                    ? resultado
                    : alternativo;
            else if (resultado.Nota is not null)
                seleccionado = resultado;
            else if (alternativo.Nota is not null)
                seleccionado = alternativo;
            else
                return new ResultadoLecturaPunto(resultado.Punto, null, new AggregateException(
                    "El punto no se encontró en NotaDiaria ni en Nota.",
                    resultado.Error!, alternativo.Error!));

            var errorNota = fuente.Tabla == "Nota"
                ? resultado.Error
                : fuente.FallbackTabla == "Nota"
                    ? alternativo.Error
                    : null;

            return errorNota is null
                ? seleccionado
                : new ResultadoLecturaPunto(resultado.Punto, seleccionado.Nota,
                    new InvalidOperationException(
                        "Falló la consulta de la base crítica con tabla Nota.", errorNota));
        }).ToArray();
    }

    private static async Task<IReadOnlyList<ResultadoLecturaPunto>> ConsultarBaseAsync(
        string accessPath,
        string accessPassword,
        string tabla,
        IReadOnlyCollection<PuntoOptions> puntos,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(accessPath))
                throw new FileNotFoundException("No se encontró la base Access configurada.", accessPath);

            await using var connection = await AbrirConexionAsync(accessPath, accessPassword, cancellationToken);
            var resultados = new List<ResultadoLecturaPunto>(puntos.Count);
            foreach (var punto in puntos)
            {
                try
                {
                    var nota = await ObtenerUltimaNotaAsync(connection, punto, tabla, cancellationToken);
                    resultados.Add(new ResultadoLecturaPunto(punto, nota, null));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    resultados.Add(new ResultadoLecturaPunto(punto, null, exception));
                }
            }
            return resultados;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return puntos.Select(punto => new ResultadoLecturaPunto(punto, null, exception)).ToArray();
        }
    }

    private static async Task<OleDbConnection> AbrirConexionAsync(
        string accessPath,
        string accessPassword,
        CancellationToken cancellationToken)
    {
        var errores = new List<Exception>();
        foreach (var provider in Providers)
        {
            var connectionString = new OleDbConnectionStringBuilder
            {
                Provider = provider,
                DataSource = accessPath
            };
            if (!string.IsNullOrWhiteSpace(accessPassword))
                connectionString["Jet OLEDB:Database Password"] = accessPassword;
            connectionString["Mode"] = "Share Deny None";

            var connection = new OleDbConnection(connectionString.ConnectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
                return connection;
            }
            catch (InvalidOperationException exception)
            {
                errores.Add(exception);
                await connection.DisposeAsync();
            }
        }

        throw new InvalidOperationException(
            "No hay un proveedor compatible de Access registrado. " +
            $"Se intentaron: {string.Join(", ", Providers)}.",
            new AggregateException(errores));
    }

    private static async Task<UltimaNota> ObtenerUltimaNotaAsync(
        OleDbConnection connection,
        PuntoOptions punto,
        string tabla,
        CancellationToken cancellationToken)
    {
        var tablaValidada = tabla switch
        {
            "Nota" => "Nota",
            "NotaDiaria" => "NotaDiaria",
            _ => throw new InvalidOperationException($"La tabla Access '{tabla}' no está permitida.")
        };

        var sql = $"""
            SELECT TOP 1 Trim(numfac), Trim(caja), fecha, hora
            FROM [{tablaValidada}]
            WHERE Trim(caja) = ?
              AND tipdoc = ?
              AND Len(Trim(numfac)) = 10
              AND Left(Trim(numfac), 3) = ?
            ORDER BY CLng(Mid(Trim(numfac), 4)) DESC
            """;

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@caja", OleDbType.VarWChar, 3).Value = punto.Caja;
        command.Parameters.Add("@tipdoc", OleDbType.SmallInt).Value = 3;
        command.Parameters.Add("@prefijo", OleDbType.VarWChar, 3).Value = punto.Caja;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (reader is null || !await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException(
                $"No se encontraron notas válidas para la caja {punto.Caja} en {tablaValidada}.");

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
