using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using monitor_access_agent_ms.Configuration;
using monitor_access_agent_ms.Models;
using monitor_access_agent_ms.Services;

var raiz = Path.Combine(Path.GetTempPath(), "gap-reenvio-tests-" + Guid.NewGuid().ToString("N"));
var origen = Path.Combine(raiz, "origen");
var destino = Path.Combine(raiz, "destino");
Directory.CreateDirectory(origen);
Directory.CreateDirectory(destino);

try
{
    await PruebaArchivoExactoAsync();
    await PruebaAmbiguedadAsync();
    await PruebaTipoIncorrectoAsync();
    await PruebaCopiaSinSobrescribirAsync();
    await PruebaContratoTomarAsync();
    await PruebaContratoResultadoAsync();
    Console.WriteLine("OK: 6 pruebas de reenvío superadas.");
    return 0;
}
finally
{
    if (Directory.Exists(raiz))
        Directory.Delete(raiz, true);
}

async Task PruebaArchivoExactoAsync()
{
    Limpiar(origen);
    var nombre = "factura-001001000000123.xml";
    await File.WriteAllTextAsync(Path.Combine(origen, nombre), Xml("factura", "01", "123"));
    var resultado = await Locator().BuscarAsync(Solicitud(nombre, "01", 123), CancellationToken.None);
    Afirmar(resultado.ArchivoEncontrado && !resultado.EsAmbiguo && resultado.RutaArchivo is not null,
        "Debe localizar y validar el nombre exacto.");
}

async Task PruebaAmbiguedadAsync()
{
    Limpiar(origen);
    await File.WriteAllTextAsync(Path.Combine(origen, "a-001001000000123.xml"), Xml("factura", "01", "123"));
    await File.WriteAllTextAsync(Path.Combine(origen, "b-001001000000123.xml"), Xml("factura", "01", "123"));
    var resultado = await Locator().BuscarAsync(Solicitud(null, "01", 123), CancellationToken.None);
    Afirmar(resultado.EsAmbiguo && resultado.RutaArchivo is null,
        "Dos XML válidos no deben seleccionarse automáticamente.");
}

async Task PruebaTipoIncorrectoAsync()
{
    Limpiar(origen);
    await File.WriteAllTextAsync(Path.Combine(origen, "001001000000123.xml"),
        Xml("notaCredito", "04", "123"));
    var resultado = await Locator().BuscarAsync(Solicitud(null, "01", 123), CancellationToken.None);
    Afirmar(resultado.ArchivoEncontrado && resultado.RutaArchivo is null,
        "Un XML de otro tipo no debe aceptarse por tener la misma serie y secuencial.");
}

async Task PruebaCopiaSinSobrescribirAsync()
{
    Limpiar(origen);
    Limpiar(destino);
    var nombre = "factura.xml";
    var ruta = Path.Combine(origen, nombre);
    await File.WriteAllTextAsync(ruta, Xml("factura", "01", "123"));
    var servicio = new ReenvioDocumentoService(Options.Create(new ReenvioOptions
    {
        Enabled = true,
        RutaParaEnviar = origen,
        ModoOperacion = ReenvioOptions.ModoCopiarACarpeta,
        RutaDestino = destino
    }));
    var primera = await servicio.ReenviarAsync(ruta, nombre, CancellationToken.None);
    var segunda = await servicio.ReenviarAsync(ruta, nombre, CancellationToken.None);
    Afirmar(primera.Reenviado && !segunda.Reenviado && File.Exists(Path.Combine(origen, nombre)),
        "Debe copiar una vez, no sobrescribir y conservar el original.");
}

async Task PruebaContratoTomarAsync()
{
    var handler = new CapturaHttpHandler("""
        {"data":{"idSolicitud":9,"codigoPunto":"PTO 001","idEmisor":1,
        "tipoComprobante":"01","serie":"001001","secuencial":123,
        "claveAcceso":null,"nombreArchivo":null,"estado":"PROCESANDO",
        "cantidadIntentos":1,"tokenEjecucion":"11111111-1111-1111-1111-111111111111",
        "fechaSolicitud":"2026-09-23T10:00:00","detalleResultado":null}}
        """);
    var client = new ReenvioApiClient(new HttpClient(handler)
    {
        BaseAddress = new Uri("http://localhost/balanceApiUrl/")
    });
    var tarea = await client.TomarAsync("PTO 001", "api-key-prueba", CancellationToken.None);
    Afirmar(tarea?.IdSolicitud == 9 &&
            handler.UltimaRuta?.Contains("PTO%20001", StringComparison.Ordinal) == true &&
            handler.UltimaAutorizacion == "ApiKey api-key-prueba",
        "Tomar debe respetar el envelope, la ruta escapada y la API Key.");
}

async Task PruebaContratoResultadoAsync()
{
    var handler = new CapturaHttpHandler("{}");
    var client = new ReenvioApiClient(new HttpClient(handler)
    {
        BaseAddress = new Uri("http://localhost/balanceApiUrl/")
    });
    var token = Guid.Parse("22222222-2222-2222-2222-222222222222");
    await client.ReportarResultadoAsync(9,
        new ResultadoReenvioRequest(token, true, false, "factura.xml", "validado"),
        string.Empty, CancellationToken.None);
    Afirmar(handler.UltimaRuta?.EndsWith("monitor/reenvios/9/resultado") == true &&
            handler.UltimoContenido?.Contains("\"archivoEncontrado\":true") == true &&
            handler.UltimoContenido?.Contains(token.ToString()) == true,
        "Resultado debe enviarse a la ruta y con el JSON acordados.");
}

ArchivoXmlLocator Locator() => new(
    Options.Create(new ReenvioOptions
    {
        Enabled = true,
        RutaParaEnviar = origen,
        CacheSegundos = 60,
        ModoOperacion = ReenvioOptions.ModoSoloValidar
    }),
    NullLogger<ArchivoXmlLocator>.Instance);

static SolicitudReenvioResponse Solicitud(string? nombre, string tipo, long secuencial) => new(
    1, "PTO-001", 1, tipo, "001001", secuencial, null, nombre,
    "PROCESANDO", 1, Guid.NewGuid(), DateTime.UtcNow, null);

static string Xml(string raizDocumento, string codDoc, string secuencial) => $"""
    <?xml version="1.0" encoding="utf-8"?>
    <{raizDocumento}>
      <infoTributaria>
        <codDoc>{codDoc}</codDoc>
        <estab>001</estab>
        <ptoEmi>001</ptoEmi>
        <secuencial>{secuencial.PadLeft(9, '0')}</secuencial>
        <claveAcceso>1234567890123456789012345678901234567890123456789</claveAcceso>
      </infoTributaria>
    </{raizDocumento}>
    """;

static void Limpiar(string ruta)
{
    foreach (var archivo in Directory.EnumerateFiles(ruta))
        File.Delete(archivo);
}

static void Afirmar(bool condicion, string mensaje)
{
    if (!condicion)
        throw new InvalidOperationException("PRUEBA FALLIDA: " + mensaje);
}

sealed class CapturaHttpHandler(string respuesta) : HttpMessageHandler
{
    public string? UltimaRuta { get; private set; }
    public string? UltimaAutorizacion { get; private set; }
    public string? UltimoContenido { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        UltimaRuta = request.RequestUri?.AbsoluteUri;
        UltimaAutorizacion = request.Headers.Authorization?.ToString();
        UltimoContenido = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(respuesta, Encoding.UTF8, "application/json")
        };
    }
}
