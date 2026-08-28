# monitor_access_agent_ms

Worker Service .NET 8 que consulta en modo lectura la última nota emitida por una
o varias cajas de Sic3000 y envía un heartbeat por punto al microservicio
`monitor_facturacion_ms`.

## Configuración local

1. Copiar `src/monitor_access_agent_ms/.env.example` como
   `src/monitor_access_agent_ms/.env`.
2. Completar la URL de la API y cada elemento de `FuentesAccess`. Una fuente
   representa un MDB y contiene su ruta, contraseña y uno o varios puntos.
3. Configurar la API Key dentro de cada punto cuando se habilite autenticación.
4. Para agregar puntos al mismo MDB, incrementar el índice de `Puntos`; para otro
   MDB, incrementar el índice de `FuentesAccess`.
5. El proceso se compila para x86 porque el proveedor ACE disponible es de 32 bits.

Ejemplo de una fuente con dos puntos (caso Mi Economía):

```env
Monitor__FuentesAccess__0__AccessPath=D:\Facturacion\Sic3000.mdb
Monitor__FuentesAccess__0__AccessPassword=CAMBIAR
Monitor__FuentesAccess__0__Puntos__0__CodigoPunto=PTO-001
Monitor__FuentesAccess__0__Puntos__0__Serie=002001
Monitor__FuentesAccess__0__Puntos__0__Caja=001
Monitor__FuentesAccess__0__Puntos__0__ApiKey=
Monitor__FuentesAccess__0__Puntos__1__CodigoPunto=PTO-002
Monitor__FuentesAccess__0__Puntos__1__Serie=002002
Monitor__FuentesAccess__0__Puntos__1__Caja=002
Monitor__FuentesAccess__0__Puntos__1__ApiKey=
```

En cada ciclo se abre cada MDB una sola vez y se consultan todos sus puntos con la
misma conexión. Un error de lectura o envío en una fuente o punto queda registrado,
pero no impide procesar los demás.

## Prueba de un solo ciclo

```powershell
dotnet run --project .\src\monitor_access_agent_ms\monitor_access_agent_ms.csproj -- --once
```

El modo `--once` consulta Access, envía un heartbeat y finaliza. Sin ese argumento,
el agente continúa ejecutándose cada minuto por defecto, según `Monitor__IntervaloMinutos`.

La cuenta del servicio necesita leer el MDB y poder crear el archivo de bloqueo de
Access en su carpeta. El agente no escribe ni modifica información dentro del MDB.
