@echo off
setlocal EnableExtensions

REM ============================================================
REM GAP Monitor Access Agent - Instalador / Actualizador
REM Usa la cuenta local dedicada MonitorAgentSvc.
REM La misma cuenta y contrasena deben existir en el servidor SMB.
REM ============================================================

set "SERVICE_NAME=GAPMonitorAccessAgent"
set "DISPLAY_NAME=GAP Monitor Access Agent"
set "DESCRIPTION=Monitorea los consecutivos emitidos por Sic3000"
set "SERVICE_ACCOUNT=MonitorAgentSvc"
set "INSTALL_DIR=C:\Sic3000\Agente\MonitorAccessAgent"
set "EXE_PATH=%INSTALL_DIR%\monitor_access_agent_ms.exe"
set "ENV_PATH=%INSTALL_DIR%\.env"

REM Solicitar elevacion si el proceso no es administrador.
net session >nul 2>&1
if errorlevel 1 (
    echo Solicitando permisos de administrador...
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

cls
echo ============================================================
echo  Instalacion / Actualizacion de %DISPLAY_NAME%
echo ============================================================
echo.

if not exist "%EXE_PATH%" (
    echo [ERROR] No se encontro el ejecutable:
    echo         %EXE_PATH%
    goto :ERROR_ARCHIVOS
)

if not exist "%ENV_PATH%" (
    echo [ERROR] No se encontro la configuracion:
    echo         %ENV_PATH%
    goto :ERROR_ARCHIVOS
)

echo [OK] Ejecutable y .env encontrados.

REM Detener el servicio antes de actualizarlo.
sc.exe query "%SERVICE_NAME%" >nul 2>&1
if not errorlevel 1 (
    sc.exe query "%SERVICE_NAME%" | find /I "RUNNING" >nul 2>&1
    if not errorlevel 1 (
        echo [INFO] Deteniendo servicio existente...
        sc.exe stop "%SERVICE_NAME%" >nul 2>&1
        timeout /t 3 /nobreak >nul
    )
)

REM Crear o actualizar la definicion del servicio.
sc.exe query "%SERVICE_NAME%" >nul 2>&1
if errorlevel 1 (
    echo [INFO] Creando servicio...
    sc.exe create "%SERVICE_NAME%" binPath= "\"%EXE_PATH%\"" start= auto DisplayName= "%DISPLAY_NAME%"
) else (
    echo [INFO] Actualizando servicio...
    sc.exe config "%SERVICE_NAME%" binPath= "\"%EXE_PATH%\"" start= auto DisplayName= "%DISPLAY_NAME%"
)
if errorlevel 1 goto :ERROR_GENERAL

sc.exe description "%SERVICE_NAME%" "%DESCRIPTION%" >nul
if errorlevel 1 goto :ERROR_GENERAL

REM Solicita la contrasena de forma segura, crea/actualiza la cuenta local
REM y configura el servicio para iniciar con ella. No guarda la contrasena.
echo.
echo La consola solicitara la contrasena de %SERVICE_ACCOUNT%.
echo Use la misma contrasena configurada en SERVIDOR.
echo No apareceran caracteres mientras la escribe.
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $name='%SERVICE_ACCOUNT%'; $secure=Read-Host 'Contrasena para MonitorAgentSvc' -AsSecureString; $ptr=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure); try { $plain=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr); if($plain.Length -lt 10){ throw 'La contrasena debe tener al menos 10 caracteres.' }; if($null -eq (Get-LocalUser -Name $name -ErrorAction SilentlyContinue)){ New-LocalUser -Name $name -Password $secure -Description 'Cuenta del servicio GAP Monitor Access' -PasswordNeverExpires -UserMayNotChangePassword | Out-Null } else { Set-LocalUser -Name $name -Password $secure -PasswordNeverExpires $true -UserMayChangePassword $false }; $svc=Get-CimInstance Win32_Service -Filter ('Name='''+'%SERVICE_NAME%'+''''); $result=Invoke-CimMethod -InputObject $svc -MethodName Change -Arguments @{StartName=('.\'+$name);StartPassword=$plain}; if($result.ReturnValue -ne 0){ throw ('No se pudo asignar la cuenta al servicio. Codigo WMI: '+$result.ReturnValue) } } finally { if($ptr -ne [IntPtr]::Zero){ [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }; $plain=$null }"
if errorlevel 1 goto :ERROR_CUENTA

REM Permisos minimos sobre los archivos del agente.
icacls "%INSTALL_DIR%" /grant "%SERVICE_ACCOUNT%:(OI)(CI)RX" >nul
if errorlevel 1 goto :ERROR_GENERAL

REM Permiso de modificacion en carpetas locales comunes de los MDB.
REM Access necesita crear su archivo temporal de bloqueo.
if exist "C:\Sic3000\GESA" icacls "C:\Sic3000\GESA" /grant "%SERVICE_ACCOUNT%:(OI)(CI)M" >nul
if exist "C:\Sic3000\SecureWrap" icacls "C:\Sic3000\SecureWrap" /grant "%SERVICE_ACCOUNT%:(OI)(CI)M" >nul

REM Recuperacion: tres reinicios separados por 60 segundos.
sc.exe failure "%SERVICE_NAME%" reset= 86400 actions= restart/60000/restart/60000/restart/60000 >nul
if errorlevel 1 goto :ERROR_GENERAL
sc.exe failureflag "%SERVICE_NAME%" 1 >nul
if errorlevel 1 goto :ERROR_GENERAL

echo [INFO] Iniciando servicio...
sc.exe start "%SERVICE_NAME%" >nul 2>&1

for /L %%I in (1,1,15) do (
    sc.exe query "%SERVICE_NAME%" | find /I "RUNNING" >nul 2>&1
    if not errorlevel 1 goto :SERVICIO_OK
    timeout /t 1 /nobreak >nul
)

echo.
echo [ERROR] El servicio no llego al estado RUNNING.
sc.exe query "%SERVICE_NAME%"
goto :ERROR_FINAL

:SERVICIO_OK
echo.
echo ============================================================
echo [OK] Servicio instalado y ejecutandose.
echo ============================================================
echo.
sc.exe query "%SERVICE_NAME%"
echo.
powershell.exe -NoProfile -Command "Get-CimInstance Win32_Service -Filter \"Name='%SERVICE_NAME%'\" | Select-Object Name,State,StartMode,StartName | Format-List"
echo Verifique el frontend despues de uno o dos minutos.
echo.
pause
exit /b 0

:ERROR_ARCHIVOS
echo.
echo Copie primero toda la publicacion del agente y configure el .env.
goto :ERROR_FINAL

:ERROR_CUENTA
echo.
echo [ERROR] No se pudo crear/configurar la cuenta dedicada.
echo Compruebe la contrasena y ejecute nuevamente el instalador.
goto :ERROR_FINAL

:ERROR_GENERAL
echo.
echo [ERROR] No se pudo completar la configuracion del servicio.
echo Codigo de error: %errorlevel%

:ERROR_FINAL
echo.
pause
exit /b 1
