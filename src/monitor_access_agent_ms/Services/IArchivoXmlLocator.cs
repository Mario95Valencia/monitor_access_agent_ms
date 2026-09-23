using monitor_access_agent_ms.Models;

namespace monitor_access_agent_ms.Services;

public interface IArchivoXmlLocator
{
    Task<ResultadoBusquedaXml> BuscarAsync(
        SolicitudReenvioResponse solicitud,
        CancellationToken cancellationToken);

    void InvalidarIndice();
}
