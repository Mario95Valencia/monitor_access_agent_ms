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
        var tipos = fuente.TiposDocumentoEfectivos
            .Concat(fuente.FallbackTiposDocumentoEfectivos)
            .Where(TiposDocumentoElectronico.EstaImplementado)
            .Distinct(StringComparer.Ordinal);
        var resultados = new List<ResultadoLecturaPunto>();

        foreach (var tipo in tipos)
        {
            IReadOnlyList<ResultadoLecturaPunto>? principales = null;
            IReadOnlyList<ResultadoLecturaPunto>? alternativos = null;

            if (fuente.Soporta(tipo))
                principales = await ConsultarTipoAsync(
                    fuente.AccessPath, fuente.AccessPassword, fuente.Tabla,
                    fuente.GuiaNumeroCampo, fuente.GuiaFechaCampo,
                    fuente.Puntos, tipo, cancellationToken);

            if (fuente.FallbackSoporta(tipo) && !string.IsNullOrWhiteSpace(fuente.FallbackAccessPath))
                alternativos = await ConsultarTipoAsync(
                    fuente.FallbackAccessPath, fuente.FallbackAccessPassword, fuente.FallbackTabla,
                    fuente.GuiaNumeroCampo, fuente.GuiaFechaCampo,
                    fuente.Puntos, tipo, cancellationToken);

            resultados.AddRange(CombinarResultados(
                fuente, tipo, principales, alternativos));
        }

        return resultados;
    }

    private static Task<IReadOnlyList<ResultadoLecturaPunto>> ConsultarTipoAsync(
        string accessPath,
        string accessPassword,
        string tablaFactura,
        string guiaNumeroCampo,
        string guiaFechaCampo,
        IReadOnlyCollection<PuntoOptions> puntos,
        string tipoDocumento,
        CancellationToken cancellationToken) => tipoDocumento switch
        {
            TiposDocumentoElectronico.Factura => ConsultarBaseAsync(
                accessPath, accessPassword, tablaFactura, puntos, tipoDocumento, cancellationToken),
            TiposDocumentoElectronico.NotaCredito or TiposDocumentoElectronico.NotaDebito =>
                ConsultarNotasCreditoAsync(
                    accessPath, accessPassword, puntos, tipoDocumento, cancellationToken),
            TiposDocumentoElectronico.GuiaRemision => ConsultarGuiasAsync(
                accessPath, accessPassword, guiaNumeroCampo, guiaFechaCampo,
                puntos, cancellationToken),
            TiposDocumentoElectronico.Retencion => ConsultarRetencionesAsync(
                accessPath, accessPassword, puntos, cancellationToken),
            _ => throw new InvalidOperationException(
                $"El lector del tipo documental {tipoDocumento} todavía no está implementado.")
        };

    private static IReadOnlyList<ResultadoLecturaPunto> CombinarResultados(
        FuenteAccessOptions fuente,
        string tipoDocumento,
        IReadOnlyList<ResultadoLecturaPunto>? principales,
        IReadOnlyList<ResultadoLecturaPunto>? alternativos)
    {
        var principalPorPunto = principales?.ToDictionary(x => x.Punto);
        var alternativoPorPunto = alternativos?.ToDictionary(x => x.Punto);

        return fuente.Puntos.Select(punto =>
        {
            ResultadoLecturaPunto? principal = null;
            ResultadoLecturaPunto? alternativo = null;
            principalPorPunto?.TryGetValue(punto, out principal);
            alternativoPorPunto?.TryGetValue(punto, out alternativo);

            var seleccionado = SeleccionarMayor(principal, alternativo);
            if (seleccionado?.Nota is null)
            {
                var errores = new[] { principal?.Error, alternativo?.Error }
                    .OfType<Exception>()
                    .ToArray();
                if (errores.Length == 0 &&
                    tipoDocumento is TiposDocumentoElectronico.NotaCredito or
                        TiposDocumentoElectronico.NotaDebito or
                        TiposDocumentoElectronico.GuiaRemision or
                        TiposDocumentoElectronico.Retencion)
                    return new ResultadoLecturaPunto(punto, tipoDocumento, null, null);

                var error = errores.Length switch
                {
                    0 => new InvalidOperationException(
                        $"No se encontraron documentos tipo {tipoDocumento} para la caja {punto.Caja}."),
                    1 => errores[0],
                    _ => new AggregateException(
                        $"No fue posible obtener el documento tipo {tipoDocumento}.", errores)
                };
                return new ResultadoLecturaPunto(punto, tipoDocumento, null, error);
            }

            // Para factura se mantiene la regla histórica: la ruta cuya tabla
            // es Nota es crítica. Para NC/ND la ruta alternativa central es
            // crítica cuando fue configurada para ese tipo.
            var errorCritico = tipoDocumento == TiposDocumentoElectronico.Factura
                ? fuente.Tabla == "Nota"
                    ? principal?.Error
                    : fuente.FallbackTabla == "Nota"
                        ? alternativo?.Error
                        : null
                : fuente.FallbackSoporta(tipoDocumento)
                    ? alternativo?.Error
                    : principal?.Error;

            return errorCritico is null
                ? seleccionado
                : new ResultadoLecturaPunto(punto, tipoDocumento, seleccionado.Nota,
                    new InvalidOperationException(
                        $"Falló la fuente crítica del documento tipo {tipoDocumento}.", errorCritico));
        }).ToArray();
    }

    private static ResultadoLecturaPunto? SeleccionarMayor(
        ResultadoLecturaPunto? principal,
        ResultadoLecturaPunto? alternativo)
    {
        if (principal?.Nota is not null && alternativo?.Nota is not null)
            return principal.Nota.Secuencial >= alternativo.Nota.Secuencial
                ? principal
                : alternativo;
        return principal?.Nota is not null ? principal : alternativo;
    }

    private static async Task<IReadOnlyList<ResultadoLecturaPunto>> ConsultarBaseAsync(
        string accessPath,
        string accessPassword,
        string tabla,
        IReadOnlyCollection<PuntoOptions> puntos,
        string tipoDocumento,
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
                    resultados.Add(new ResultadoLecturaPunto(punto, tipoDocumento, nota, null));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    resultados.Add(new ResultadoLecturaPunto(punto, tipoDocumento, null, exception));
                }
            }
            return resultados;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return puntos.Select(punto =>
                new ResultadoLecturaPunto(punto, tipoDocumento, null, exception)).ToArray();
        }
    }

    private static async Task<IReadOnlyList<ResultadoLecturaPunto>> ConsultarNotasCreditoAsync(
        string accessPath,
        string accessPassword,
        IReadOnlyCollection<PuntoOptions> puntos,
        string tipoDocumento,
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
                    var documento = await ObtenerUltimaNotaCreditoAsync(
                        connection, punto, tipoDocumento, cancellationToken);
                    resultados.Add(new ResultadoLecturaPunto(punto, tipoDocumento, documento, null));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    resultados.Add(new ResultadoLecturaPunto(punto, tipoDocumento, null, exception));
                }
            }
            return resultados;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return puntos.Select(punto =>
                new ResultadoLecturaPunto(punto, tipoDocumento, null, exception)).ToArray();
        }
    }

    private static async Task<IReadOnlyList<ResultadoLecturaPunto>> ConsultarGuiasAsync(
        string accessPath,
        string accessPassword,
        string numeroCampo,
        string fechaCampo,
        IReadOnlyCollection<PuntoOptions> puntos,
        CancellationToken cancellationToken)
    {
        ValidarCamposGuia(numeroCampo, fechaCampo);
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
                    var documento = await ObtenerUltimaGuiaAsync(
                        connection, punto, numeroCampo, fechaCampo, cancellationToken);
                    resultados.Add(new ResultadoLecturaPunto(
                        punto, TiposDocumentoElectronico.GuiaRemision, documento, null));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    resultados.Add(new ResultadoLecturaPunto(
                        punto, TiposDocumentoElectronico.GuiaRemision, null, exception));
                }
            }
            return resultados;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return puntos.Select(punto => new ResultadoLecturaPunto(
                punto, TiposDocumentoElectronico.GuiaRemision, null, exception)).ToArray();
        }
    }

    private static async Task<IReadOnlyList<ResultadoLecturaPunto>> ConsultarRetencionesAsync(
        string accessPath,
        string accessPassword,
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
                    var documento = await ObtenerUltimaRetencionAsync(
                        connection, punto, cancellationToken);
                    resultados.Add(new ResultadoLecturaPunto(
                        punto, TiposDocumentoElectronico.Retencion, documento, null));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    resultados.Add(new ResultadoLecturaPunto(
                        punto, TiposDocumentoElectronico.Retencion, null, exception));
                }
            }
            return resultados;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return puntos.Select(punto => new ResultadoLecturaPunto(
                punto, TiposDocumentoElectronico.Retencion, null, exception)).ToArray();
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

    private static async Task<UltimaNota?> ObtenerUltimaNotaCreditoAsync(
        OleDbConnection connection,
        PuntoOptions punto,
        string tipoDocumento,
        CancellationToken cancellationToken)
    {
        var id = tipoDocumento switch
        {
            TiposDocumentoElectronico.NotaCredito => "NC",
            TiposDocumentoElectronico.NotaDebito => "ND",
            _ => throw new InvalidOperationException(
                $"El tipo {tipoDocumento} no corresponde a NotaCredito.")
        };

        const string sql = """
            SELECT TOP 1 Trim(numnota), Trim(caja), fecha
            FROM [NotaCredito]
            WHERE UCase(Trim(id)) = ?
              AND Trim(caja) = ?
              AND Len(Trim(numnota)) = 10
              AND Left(Trim(numnota), 3) = ?
            ORDER BY CLng(Mid(Trim(numnota), 4)) DESC
            """;

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@id", OleDbType.VarWChar, 5).Value = id;
        command.Parameters.Add("@caja", OleDbType.VarWChar, 10).Value = punto.Caja;
        command.Parameters.Add("@prefijo", OleDbType.VarWChar, 3).Value = punto.Caja;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (reader is null || !await reader.ReadAsync(cancellationToken))
            return null;

        var numeroDocumento = reader.GetString(0).Trim();
        if (!TryObtenerSecuencial(numeroDocumento, punto.Caja, out var secuencial))
            throw new InvalidDataException(
                $"El número de documento '{numeroDocumento}' no tiene el formato esperado.");

        var fecha = reader.IsDBNull(2) ? DateTime.Today : reader.GetDateTime(2);
        return new UltimaNota(numeroDocumento, reader.GetString(1).Trim(), secuencial, fecha);
    }

    private static async Task<UltimaNota?> ObtenerUltimaGuiaAsync(
        OleDbConnection connection,
        PuntoOptions punto,
        string numeroCampo,
        string fechaCampo,
        CancellationToken cancellationToken)
    {
        ValidarCamposGuia(numeroCampo, fechaCampo);
        var sql = $"""
            SELECT [{numeroCampo}], [{fechaCampo}]
            FROM [GuiaRemision]
            WHERE [{numeroCampo}] Is Not Null
            """;

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        UltimaNota? mayor = null;
        while (reader is not null && await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(0))
                continue;

            var numeroDocumento = Convert.ToString(reader.GetValue(0))?.Trim();
            if (!TryObtenerSecuencialGuia(
                    numeroDocumento, numeroCampo, punto.Serie, punto.Caja, out var secuencial))
                continue;

            var fecha = reader.IsDBNull(1) ? DateTime.Today : Convert.ToDateTime(reader.GetValue(1));
            if (mayor is null || secuencial > mayor.Secuencial)
            {
                var numeroNormalizado = numeroCampo == "numGuia"
                    ? $"{punto.Serie}{secuencial:D9}"
                    : numeroDocumento!;
                mayor = new UltimaNota(numeroNormalizado, punto.Caja, secuencial, fecha);
            }
        }

        return mayor;
    }

    private static async Task<UltimaNota?> ObtenerUltimaRetencionAsync(
        OleDbConnection connection,
        PuntoOptions punto,
        CancellationToken cancellationToken)
    {
        var establecimiento = punto.Serie[..3];
        var puntoEmision = punto.Serie[3..];
        const string sql = """
            SELECT TOP 1 Trim(Secuencial), Max(Fecha)
            FROM [CgRetenciones]
            WHERE Trim(nestablecimiento) = ?
              AND Trim(puntoemision) = ?
              AND Secuencial Is Not Null
              AND IsNumeric(Trim(Secuencial))
            GROUP BY Trim(Secuencial)
            ORDER BY CLng(Trim(Secuencial)) DESC
            """;

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@establecimiento", OleDbType.VarWChar, 3).Value = establecimiento;
        command.Parameters.Add("@puntoEmision", OleDbType.VarWChar, 3).Value = puntoEmision;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (reader is null || !await reader.ReadAsync(cancellationToken))
            return null;

        var valorSecuencial = reader.GetString(0).Trim();
        if (!long.TryParse(valorSecuencial, out var secuencial))
            throw new InvalidDataException(
                $"El secuencial de retención '{valorSecuencial}' no es numérico.");

        var fecha = reader.IsDBNull(1) ? DateTime.Today : reader.GetDateTime(1);
        var numeroDocumento = $"{punto.Serie}{secuencial:D9}";
        return new UltimaNota(numeroDocumento, punto.Caja, secuencial, fecha);
    }

    private static void ValidarCamposGuia(string numeroCampo, string fechaCampo)
    {
        if (numeroCampo is not ("numGuia" or "numFac"))
            throw new InvalidOperationException(
                "GuiaNumeroCampo debe ser numGuia o numFac.");
        if (fechaCampo is not ("fechaEmisionDocSustento" or
            "fechaIniTransporte" or "fechaFinTransporte"))
            throw new InvalidOperationException(
                "GuiaFechaCampo no corresponde a una fecha permitida de GuiaRemision.");
    }

    internal static bool TryObtenerSecuencialElectronico(
        string? numeroDocumento,
        string serie,
        string caja,
        out long secuencial)
    {
        secuencial = 0;
        if (string.IsNullOrWhiteSpace(numeroDocumento))
            return false;

        var digitos = new string(numeroDocumento.Where(char.IsDigit).ToArray());
        if (digitos.Length == 15 && digitos.StartsWith(serie, StringComparison.Ordinal))
            return long.TryParse(digitos[6..], out secuencial);

        return TryObtenerSecuencial(digitos, caja, out secuencial);
    }

    internal static bool TryObtenerSecuencialGuia(
        string? numeroDocumento,
        string numeroCampo,
        string serie,
        string caja,
        out long secuencial)
    {
        secuencial = 0;
        if (string.IsNullOrWhiteSpace(numeroDocumento))
            return false;

        var digitos = new string(numeroDocumento.Where(char.IsDigit).ToArray());
        if (numeroCampo == "numGuia" && digitos.Length is >= 1 and <= 9)
            return long.TryParse(digitos, out secuencial);

        return TryObtenerSecuencialElectronico(numeroDocumento, serie, caja, out secuencial);
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
