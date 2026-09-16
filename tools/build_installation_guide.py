from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.section import WD_SECTION
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.enum.style import WD_STYLE_TYPE
from pathlib import Path

OUT = Path(r"D:\ProyectGap\MonitorAccess\monitor_access_agent_ms\docs\Guia_Instalacion_Operacion_GAP_Monitor_Access_Agent.docx")
OUT.parent.mkdir(parents=True, exist_ok=True)

BLUE = "2E74B5"
DARK = "17365D"
LIGHT = "E8EEF5"
PALE = "F4F6F9"
GREEN = "E2F0D9"
GOLD = "FFF2CC"
RED = "FCE4D6"
GRAY = "666666"

doc = Document()
sec = doc.sections[0]
sec.page_width = Inches(8.5)
sec.page_height = Inches(11)
sec.top_margin = Inches(0.82)
sec.bottom_margin = Inches(0.78)
sec.left_margin = Inches(0.86)
sec.right_margin = Inches(0.86)
sec.header_distance = Inches(0.38)
sec.footer_distance = Inches(0.38)

styles = doc.styles
normal = styles["Normal"]
normal.font.name = "Calibri"
normal._element.rPr.rFonts.set(qn("w:ascii"), "Calibri")
normal._element.rPr.rFonts.set(qn("w:hAnsi"), "Calibri")
normal.font.size = Pt(10.5)
normal.paragraph_format.space_after = Pt(6)
normal.paragraph_format.line_spacing = 1.18

for name, size, before, after, color in [
    ("Heading 1", 16, 16, 8, BLUE),
    ("Heading 2", 13, 12, 6, BLUE),
    ("Heading 3", 11.5, 9, 4, DARK),
]:
    st = styles[name]
    st.font.name = "Calibri"
    st._element.rPr.rFonts.set(qn("w:ascii"), "Calibri")
    st._element.rPr.rFonts.set(qn("w:hAnsi"), "Calibri")
    st.font.size = Pt(size)
    st.font.bold = True
    st.font.color.rgb = RGBColor.from_string(color)
    st.paragraph_format.space_before = Pt(before)
    st.paragraph_format.space_after = Pt(after)
    st.paragraph_format.keep_with_next = True

for name in ["List Bullet", "List Number"]:
    st = styles[name]
    st.font.name = "Calibri"
    st.font.size = Pt(10.5)
    st.paragraph_format.left_indent = Inches(0.38)
    st.paragraph_format.first_line_indent = Inches(-0.19)
    st.paragraph_format.space_after = Pt(4)
    st.paragraph_format.line_spacing = 1.18

code_style = styles.add_style("Code Block", WD_STYLE_TYPE.PARAGRAPH)
code_style.font.name = "Consolas"
code_style._element.rPr.rFonts.set(qn("w:ascii"), "Consolas")
code_style._element.rPr.rFonts.set(qn("w:hAnsi"), "Consolas")
code_style.font.size = Pt(8.3)
code_style.paragraph_format.left_indent = Inches(0.16)
code_style.paragraph_format.right_indent = Inches(0.10)
code_style.paragraph_format.space_before = Pt(3)
code_style.paragraph_format.space_after = Pt(7)
code_style.paragraph_format.line_spacing = 1.0

def shade(paragraph, fill=PALE):
    ppr = paragraph._p.get_or_add_pPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:fill"), fill)
    ppr.append(shd)
    borders = OxmlElement("w:pBdr")
    left = OxmlElement("w:left")
    left.set(qn("w:val"), "single")
    left.set(qn("w:sz"), "14")
    left.set(qn("w:color"), BLUE)
    left.set(qn("w:space"), "5")
    borders.append(left)
    ppr.append(borders)

def code(text):
    p = doc.add_paragraph(style="Code Block")
    p.add_run(text)
    shade(p, "F3F5F7")
    return p

def callout(label, text, fill=LIGHT):
    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(5)
    p.paragraph_format.space_after = Pt(8)
    r = p.add_run(label + "  ")
    r.bold = True
    r.font.color.rgb = RGBColor.from_string(DARK)
    p.add_run(text)
    shade(p, fill)
    return p

def add_bullet(text):
    doc.add_paragraph(text, style="List Bullet")

def add_step(text):
    doc.add_paragraph(text, style="List Number")

def set_cell_fill(cell, fill):
    tcpr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:fill"), fill)
    tcpr.append(shd)

def set_cell_width(cell, twips):
    tcpr = cell._tc.get_or_add_tcPr()
    tcw = tcpr.find(qn("w:tcW"))
    if tcw is None:
        tcw = OxmlElement("w:tcW")
        tcpr.append(tcw)
    tcw.set(qn("w:w"), str(twips))
    tcw.set(qn("w:type"), "dxa")

def style_table(table, widths):
    table.alignment = WD_TABLE_ALIGNMENT.LEFT
    table.autofit = False
    tblpr = table._tbl.tblPr
    tblw = tblpr.find(qn("w:tblW"))
    if tblw is None:
        tblw = OxmlElement("w:tblW")
        tblpr.append(tblw)
    tblw.set(qn("w:w"), str(sum(widths)))
    tblw.set(qn("w:type"), "dxa")
    tblind = OxmlElement("w:tblInd")
    tblind.set(qn("w:w"), "120")
    tblind.set(qn("w:type"), "dxa")
    tblpr.append(tblind)
    grid = table._tbl.tblGrid
    for child in list(grid):
        grid.remove(child)
    for width in widths:
        col = OxmlElement("w:gridCol")
        col.set(qn("w:w"), str(width))
        grid.append(col)
    for row in table.rows:
        for idx, cell in enumerate(row.cells):
            set_cell_width(cell, widths[idx])
            cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
            for p in cell.paragraphs:
                p.paragraph_format.space_after = Pt(2)
                for run in p.runs:
                    run.font.size = Pt(9.3)

def add_page_number(paragraph):
    paragraph.add_run("Página ")
    fld = OxmlElement("w:fldSimple")
    fld.set(qn("w:instr"), "PAGE")
    paragraph._p.append(fld)

# Header and footer
hp = sec.header.paragraphs[0]
hp.text = "GAP SYSTEMS  |  OPERACIÓN DE MONITOREO"
hp.alignment = WD_ALIGN_PARAGRAPH.RIGHT
hp.runs[0].font.size = Pt(8)
hp.runs[0].font.bold = True
hp.runs[0].font.color.rgb = RGBColor.from_string(GRAY)
fp = sec.footer.paragraphs[0]
fp.alignment = WD_ALIGN_PARAGRAPH.RIGHT
fp.style = styles["Normal"]
for r in fp.runs:
    r.font.size = Pt(8)
add_page_number(fp)

# Cover
p = doc.add_paragraph()
p.paragraph_format.space_before = Pt(120)
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
r = p.add_run("GAP SYSTEMS")
r.bold = True; r.font.size = Pt(12); r.font.color.rgb = RGBColor.from_string(BLUE)
p = doc.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.space_after = Pt(8)
r = p.add_run("Guía de instalación y operación")
r.bold = True; r.font.size = Pt(27); r.font.color.rgb = RGBColor.from_string(DARK)
p = doc.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.space_after = Pt(28)
r = p.add_run("GAP Monitor Access Agent")
r.font.size = Pt(17); r.font.color.rgb = RGBColor.from_string(BLUE)
callout("Objetivo", "Instalar el agente en cajas Sic3000, configurarlo para leer las bases Access y mantenerlo ejecutándose automáticamente como servicio de Windows.", LIGHT)
p = doc.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.space_before = Pt(90)
r = p.add_run("Manual operativo • Versión 1.0 • Septiembre 2026")
r.font.size = Pt(10); r.font.color.rgb = RGBColor.from_string(GRAY)
doc.add_page_break()

doc.add_heading("1. Cómo funciona", level=1)
doc.add_paragraph("El agente trabaja en segundo plano. En cada ciclo consulta los puntos configurados, obtiene el último documento emitido y envía un heartbeat al backend de monitoreo.")
add_bullet("Consulta primero NotaDiaria en la base local o principal.")
add_bullet("Consulta también Nota en la base general o compartida.")
add_bullet("Si existen datos en ambas, envía el documento con el mayor consecutivo.")
add_bullet("La base con tabla Nota es crítica: si falla, envía AccessDisponible=false y el detalle para generar una alerta.")
add_bullet("Un error de una fuente o punto no detiene los demás.")
callout("Importante", "El agente no modifica registros del MDB. Access puede necesitar crear un archivo temporal de bloqueo en la carpeta de la base.", GOLD)

doc.add_heading("2. Requisitos", level=1)
add_bullet("Windows de 64 o 32 bits con permisos de administrador para instalar el servicio.")
add_bullet("Proveedor Microsoft ACE OLEDB o Jet compatible con 32 bits.")
add_bullet("Acceso TCP al backend configurado, por ejemplo 186.4.132.139:5014.")
add_bullet("Rutas correctas a los MDB y permisos de lectura/modificación en sus carpetas.")
add_bullet("Cuenta dedicada MonitorAgentSvc con contraseña segura.")
add_bullet("Si Nota está en red, usar ruta UNC (\\SERVIDOR\\Recurso), nunca una unidad mapeada como F:.")

doc.add_heading("3. Publicar y copiar el agente", level=1)
doc.add_paragraph("Desde la computadora de desarrollo, abrir PowerShell dentro del repositorio y publicar una versión autónoma x86:")
code("cd D:\\ProyectGap\\MonitorAccess\\monitor_access_agent_ms\n\ndotnet publish .\\src\\monitor_access_agent_ms\\monitor_access_agent_ms.csproj `\n  -c Release `\n  -r win-x86 `\n  --self-contained true `\n  -o D:\\Publicaciones\\MonitorAccessAgent")
doc.add_paragraph("Copiar toda la carpeta publicada en la caja:")
code("C:\\Sic3000\\Agente\\MonitorAccessAgent")
callout("No copiar solo el EXE", "El agente necesita las DLL, archivos JSON y el .env que acompañan al ejecutable.", RED)

doc.add_heading("4. Configurar el archivo .env", level=1)
doc.add_paragraph("El archivo .env debe estar junto a monitor_access_agent_ms.exe. Nunca incluir contraseñas reales en manuales, correos o capturas.")
code("Monitor__IntervaloMinutos=1\nMonitor__ApiUrl=http://SERVIDOR_API:PUERTO/balanceApiUrl/monitor/heartbeat\nMonitor__VersionAgente=1.0.0\nMonitor__HttpTimeoutSeconds=30\n\n# Primera base: NotaDiaria\nMonitor__FuentesAccess__0__AccessPath=C:\\Sic3000\\GESA\\Sic3000.mdb\nMonitor__FuentesAccess__0__AccessPassword=\nMonitor__FuentesAccess__0__Tabla=NotaDiaria\n\n# Segunda base crítica: Nota\nMonitor__FuentesAccess__0__FallbackAccessPath=\\\\SERVIDOR\\Series3000\\Sic3000\\GESA\\Sic3000.mdb\nMonitor__FuentesAccess__0__FallbackAccessPassword=CAMBIAR\nMonitor__FuentesAccess__0__FallbackTabla=Nota\n\n# Punto\nMonitor__FuentesAccess__0__Puntos__0__CodigoPunto=ATU-CAJA 002-001\nMonitor__FuentesAccess__0__Puntos__0__IdEmisor=CAMBIAR\nMonitor__FuentesAccess__0__Puntos__0__Serie=002001\nMonitor__FuentesAccess__0__Puntos__0__Caja=001\nMonitor__FuentesAccess__0__Puntos__0__ApiKey=")

doc.add_heading("Significado de los identificadores", level=2)
table = doc.add_table(rows=1, cols=3)
table.style = "Table Grid"
headers = ["Parámetro", "Ejemplo", "Uso"]
for i, text in enumerate(headers):
    table.rows[0].cells[i].text = text
    set_cell_fill(table.rows[0].cells[i], LIGHT)
    for run in table.rows[0].cells[i].paragraphs[0].runs: run.bold = True
rows = [
    ("CodigoPunto", "ATU-CAJA 002-001", "Nombre visible del punto."),
    ("IdEmisor", "6", "ID de DOCUMENTO_ESTADO; empata con Serie en el backend."),
    ("Serie", "002001", "Serie tributaria de seis dígitos."),
    ("Caja", "001", "Prefijo de numfac dentro del MDB."),
    ("ApiKey", "[secreto]", "Clave del punto cuando el backend la exige."),
]
for vals in rows:
    cells = table.add_row().cells
    for i, value in enumerate(vals): cells[i].text = value
style_table(table, [1900, 2100, 5360])

doc.add_heading("Varios puntos o estaciones", level=2)
doc.add_paragraph("Para otro punto en el mismo par de bases, agregar Puntos__1. Para otra estación o par de MDB, agregar FuentesAccess__1. No reutilizar IdEmisor + Serie entre puntos distintos.")

doc.add_heading("5. Validar antes de instalar", level=1)
add_step("Comprobar el puerto del backend.")
code("Test-NetConnection SERVIDOR_API -Port PUERTO")
add_step("Comprobar las rutas de las bases.")
code("Test-Path -LiteralPath 'C:\\Sic3000\\GESA\\Sic3000.mdb'\nTest-Path -LiteralPath '\\\\SERVIDOR\\Series3000\\Sic3000\\GESA\\Sic3000.mdb'")
add_step("Ejecutar un solo ciclo desde la carpeta del agente.")
code("cd C:\\Sic3000\\Agente\\MonitorAccessAgent\n.\\monitor_access_agent_ms.exe --once")
doc.add_paragraph("Resultado esperado:")
code("Access disponible. Documento 0010065928, secuencial 65928.\nHeartbeat enviado correctamente.")
callout("No continuar", "Si aparece 'falló la base crítica Nota', corregir primero la ruta UNC, contraseña de Access o permisos de red.", RED)

doc.add_heading("6. Cuenta dedicada para acceso de red", level=1)
doc.add_paragraph("La cuenta recomendada es MonitorAgentSvc. Debe tener una contraseña segura y, en redes sin dominio, existir con el mismo nombre y contraseña en la caja y en SERVIDOR.")
doc.add_heading("Crear o actualizar en la caja", level=2)
code("$password = Read-Host 'Contraseña para MonitorAgentSvc' -AsSecureString\n\nif (Get-LocalUser MonitorAgentSvc -ErrorAction SilentlyContinue) {\n  Set-LocalUser -Name MonitorAgentSvc -Password $password `\n    -PasswordNeverExpires $true -UserMayChangePassword $false\n} else {\n  New-LocalUser -Name MonitorAgentSvc -Password $password `\n    -Description 'Cuenta del servicio GAP Monitor Access' `\n    -PasswordNeverExpires -UserMayNotChangePassword\n}")
doc.add_heading("Permisos locales", level=2)
code("icacls 'C:\\Sic3000\\Agente\\MonitorAccessAgent' /grant 'MonitorAgentSvc:(OI)(CI)RX'\nicacls 'C:\\Sic3000\\GESA' /grant 'MonitorAgentSvc:(OI)(CI)M'")
doc.add_heading("Permisos en SERVIDOR", level=2)
add_bullet("Crear MonitorAgentSvc con la misma contraseña o usar una cuenta de dominio administrada.")
add_bullet("Conceder permiso Cambiar en el recurso compartido y Modificar en la carpeta física.")
add_bullet("No guardar ni compartir la contraseña en texto, BAT, .env, capturas o chats.")

doc.add_heading("7. Instalar el servicio", level=1)
doc.add_paragraph("Método recomendado: copiar el BAT actualizado y ejecutarlo como administrador:")
code("Instalar_GAPMonitorAccessAgent_CuentaDedicada.bat")
doc.add_paragraph("El instalador valida el EXE y .env, crea o actualiza el servicio, solicita la contraseña sin mostrarla, asigna MonitorAgentSvc, configura recuperación e inicia el servicio.")
doc.add_heading("Instalación manual alternativa", level=2)
code("New-Service `\n  -Name 'GAPMonitorAccessAgent' `\n  -DisplayName 'GAP Monitor Access Agent' `\n  -BinaryPathName '\"C:\\Sic3000\\Agente\\MonitorAccessAgent\\monitor_access_agent_ms.exe\"' `\n  -StartupType Automatic")
doc.add_paragraph("Después, en services.msc, abrir Propiedades > Iniciar sesión, seleccionar Esta cuenta y configurar .\\MonitorAgentSvc. No incluir --once en el servicio.")
code("sc.exe failure GAPMonitorAccessAgent reset= 86400 actions= restart/60000/restart/60000/restart/60000\nsc.exe failureflag GAPMonitorAccessAgent 1")

doc.add_heading("8. Verificación del servicio", level=1)
code("Get-Service GAPMonitorAccessAgent\n\nGet-CimInstance Win32_Service -Filter \"Name='GAPMonitorAccessAgent'\" |\n  Select-Object State, StartMode, StartName, PathName")
doc.add_paragraph("Valores esperados:")
code("State     : Running\nStartMode : Auto\nStartName : .\\MonitorAgentSvc")
add_bullet("Esperar uno o dos minutos y confirmar que Última respuesta cambia en el frontend.")
add_bullet("Confirmar Conectividad activa y que el secuencial emitido coincide con Access.")
add_bullet("No ejecutar manualmente el EXE mientras el servicio esté activo: produciría heartbeats duplicados.")
doc.add_heading("Prueba de arranque", level=2)
doc.add_paragraph("Cuando sea seguro reiniciar la caja, reiniciar Windows. El servicio debe quedar Running y el heartbeat debe reaparecer sin abrir PowerShell.")

doc.add_heading("9. Comandos de operación", level=1)
table = doc.add_table(rows=1, cols=2)
table.style = "Table Grid"
for i, text in enumerate(["Acción", "Comando"]):
    table.rows[0].cells[i].text = text
    set_cell_fill(table.rows[0].cells[i], LIGHT)
    for run in table.rows[0].cells[i].paragraphs[0].runs: run.bold = True
ops = [
    ("Ver estado", "Get-Service GAPMonitorAccessAgent"),
    ("Iniciar", "Start-Service GAPMonitorAccessAgent"),
    ("Detener", "Stop-Service GAPMonitorAccessAgent"),
    ("Reiniciar", "Restart-Service GAPMonitorAccessAgent"),
    ("Ver configuración", "sc.exe qc GAPMonitorAccessAgent"),
    ("Ver recuperación", "sc.exe qfailure GAPMonitorAccessAgent"),
    ("Eliminar", "Stop-Service GAPMonitorAccessAgent; sc.exe delete GAPMonitorAccessAgent"),
]
for action, command in ops:
    cells = table.add_row().cells
    cells[0].text = action
    cells[1].text = command
    cells[1].paragraphs[0].runs[0].font.name = "Consolas"
    cells[1].paragraphs[0].runs[0].font.size = Pt(8.5)
style_table(table, [2100, 7260])

doc.add_heading("Actualizar configuración o binarios", level=2)
add_bullet("Detener el servicio.")
add_bullet("Respaldar el .env.")
add_bullet("Reemplazar todos los archivos publicados o editar el .env.")
add_bullet("Iniciar el servicio y verificar el frontend.")
code("Stop-Service GAPMonitorAccessAgent\n# actualizar archivos o .env\nStart-Service GAPMonitorAccessAgent")

doc.add_heading("10. Diagnóstico rápido", level=1)
diagnostics = [
    ("Proveedor ACE no registrado", "Usar publicación win-x86. El agente intenta ACE 16, ACE 12 y Jet 4.0. Instalar Access Runtime x86 si ninguno existe."),
    ("No se encontró la base", "Comprobar Test-Path y corregir AccessPath. Para red usar \\\\SERVIDOR\\Recurso, no F:."),
    ("Manual funciona, servicio falla", "Comprobar StartName. El servicio debe usar MonitorAgentSvc con permisos locales y de red."),
    ("Error 1069 al iniciar", "Volver a escribir la contraseña en services.msc > Iniciar sesión y revisar el derecho de inicio como servicio."),
    ("Error 1067 al iniciar", "Revisar .env y el Visor de eventos de Windows; suele ser configuración inválida."),
    ("HTTP rechazado", "Comprobar ApiUrl y Test-NetConnection. Verificar firewall, NAT y que el backend esté activo."),
    ("Frontend conserva Error Access", "Detener procesos duplicados, reiniciar el servicio y comprobar que Última respuesta se actualice."),
]
for title, detail in diagnostics:
    p = doc.add_paragraph()
    p.paragraph_format.space_after = Pt(5)
    r = p.add_run(title + ": ")
    r.bold = True; r.font.color.rgb = RGBColor.from_string(DARK)
    p.add_run(detail)

doc.add_heading("Consultar eventos recientes", level=2)
code("Get-WinEvent -FilterHashtable @{\n  LogName='Application'\n  StartTime=(Get-Date).AddMinutes(-15)\n} | Where-Object { $_.LevelDisplayName -eq 'Error' } |\n  Select-Object -First 10 TimeCreated,ProviderName,Message | Format-List")

doc.add_heading("11. Lista final de puesta en producción", level=1)
for item in [
    "Toda la publicación está en C:\\Sic3000\\Agente\\MonitorAccessAgent.",
    "El .env contiene ApiUrl, IdEmisor, Serie, Caja y rutas correctas.",
    "NotaDiaria apunta a la primera base y Nota a la base crítica.",
    "Las rutas de red usan formato UNC con dos barras iniciales.",
    "MonitorAgentSvc existe, tiene contraseña segura y permisos requeridos.",
    "La ejecución --once muestra Access disponible y heartbeat correcto.",
    "El servicio está Running, Automatic y usa MonitorAgentSvc.",
    "El frontend actualiza la última respuesta y muestra los secuenciales.",
    "Después de reiniciar Windows, el heartbeat vuelve sin intervención manual.",
]:
    add_bullet("☐ " + item)

callout("Criterio de cierre", "La estación está lista cuando el servicio inicia con Windows, ambas bases pueden consultarse, el frontend recibe heartbeats periódicos y una falla de Nota genera Error de Access.", GREEN)

# Metadata and save
doc.core_properties.title = "Guía de instalación y operación - GAP Monitor Access Agent"
doc.core_properties.subject = "Instalación, configuración, servicio Windows y diagnóstico"
doc.core_properties.author = "GAP Systems"
doc.core_properties.keywords = "GAP, Sic3000, Access, Windows Service, Monitor"
doc.save(OUT)
print(OUT)
