# monitor_access_agent_ms

Worker Service .NET 8 que consulta en modo lectura la última nota emitida por una
o varias cajas de Sic3000 y envía un heartbeat por punto al microservicio
`monitor_facturacion_ms`.

## Arquitectura del agente

El ejecutable se organiza en workers independientes:

- `MonitorWorker`: habilitado actualmente. Consulta Access y envía heartbeats.
- `ReenvioWorker`: reservado para una fase posterior. Consultará órdenes del
  backend y las ejecutará mediante una cola aislada, sin bloquear el monitoreo.

El backend no abre conexiones hacia las cajas. Tanto el monitoreo como el futuro
reenvío iniciarán sus conexiones desde el agente.

## Configuración local

1. Copiar `src/monitor_access_agent_ms/.env.example` como
   `src/monitor_access_agent_ms/.env`.
2. Completar la URL de la API y cada elemento de `FuentesAccess`. Una fuente
   representa una estación: contiene la base principal (`NotaDiaria`), la base
   general (`Nota`) y uno o varios puntos.
3. Configurar la API Key dentro de cada punto cuando se habilite autenticación.
4. Para agregar puntos al mismo MDB, incrementar el índice de `Puntos`; para otro
   MDB, incrementar el índice de `FuentesAccess`.
5. El proceso se compila para x86 porque el proveedor ACE disponible es de 32 bits.

Cada fuente declara los tipos documentales que podrá consultar. En esta primera
fase solamente está habilitado el lector de factura (`01`). Los códigos admitidos
por configuración son `01`, `03`, `04`, `05`, `06` y `07`; configurar uno de los
otros códigos deja registrada la capacidad futura, pero el agente advertirá que
su lector todavía no está activo.

Ejemplo de una fuente con dos puntos (caso Mi Economía):

```env
Monitor__FuentesAccess__0__AccessPath=D:\Facturacion\SecureWrap\Sic3000.mdb
Monitor__FuentesAccess__0__AccessPassword=
Monitor__FuentesAccess__0__Tabla=NotaDiaria
Monitor__FuentesAccess__0__TiposDocumento__0=01
Monitor__FuentesAccess__0__FallbackAccessPath=D:\Facturacion\Sic3000.mdb
Monitor__FuentesAccess__0__FallbackAccessPassword=CAMBIAR
Monitor__FuentesAccess__0__FallbackTabla=Nota
Monitor__FuentesAccess__0__Puntos__0__CodigoPunto=PTO-001
Monitor__FuentesAccess__0__Puntos__0__IdEmisor=1
Monitor__FuentesAccess__0__Puntos__0__Serie=002001
Monitor__FuentesAccess__0__Puntos__0__Caja=001
Monitor__FuentesAccess__0__Puntos__0__ApiKey=
Monitor__FuentesAccess__0__Puntos__1__CodigoPunto=PTO-002
Monitor__FuentesAccess__0__Puntos__1__IdEmisor=2
Monitor__FuentesAccess__0__Puntos__1__Serie=002002
Monitor__FuentesAccess__0__Puntos__1__Caja=002
Monitor__FuentesAccess__0__Puntos__1__ApiKey=
```

En cada ciclo se consulta primero `NotaDiaria` y luego `Nota`. Si el punto existe en
ambas tablas, se envía el documento con el mayor consecutivo; si solo existe en una,
se usa ese resultado. Cada MDB se abre una vez por ciclo. Un error en una base no
descarta un resultado válido de la otra ni impide procesar los demás puntos.

La base configurada con la tabla `Nota` es crítica. Si su archivo, conexión, tabla o
consulta falla, el heartbeat se envía con `AccessDisponible=false` y el mensaje de
error para generar la alerta, incluso si `NotaDiaria` proporcionó un consecutivo.

## Prueba de un solo ciclo

```powershell
dotnet run --project .\src\monitor_access_agent_ms\monitor_access_agent_ms.csproj -- --once
```

El modo `--once` consulta Access, envía un heartbeat y finaliza. Sin ese argumento,
el agente continúa ejecutándose cada minuto por defecto, según `Monitor__IntervaloMinutos`.

La cuenta del servicio necesita leer el MDB y poder crear el archivo de bloqueo de
Access en su carpeta. El agente no escribe ni modifica información dentro del MDB.
