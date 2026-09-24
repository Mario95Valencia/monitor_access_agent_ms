param([Parameter(Mandatory = $true)][string[]]$Path)

$ErrorActionPreference = "Stop"
foreach ($item in $Path) {
    $settings = New-Object System.Xml.XmlReaderSettings
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [System.Xml.XmlReader]::Create($item, $settings)
    try { $document = [System.Xml.Linq.XDocument]::Load($reader) }
    finally { $reader.Dispose() }

    function Value-Of([string]$name) {
        return ($document.Descendants() |
            Where-Object { $_.Name.LocalName -eq $name } |
            Select-Object -First 1).Value
    }

    $clave = Value-Of "claveAcceso"
    if ([string]::IsNullOrWhiteSpace($clave)) { $clave = Value-Of "numeroAutorizacion" }
    $claveResumen = if ($clave.Length -gt 12) {
        $clave.Substring(0, 4) + "..." + $clave.Substring($clave.Length - 6)
    } else { $clave }

    [pscustomobject]@{
        Archivo = [IO.Path]::GetFileName($item)
        Raiz = $document.Root.Name.LocalName
        CodDoc = Value-Of "codDoc"
        Estab = Value-Of "estab"
        PtoEmi = Value-Of "ptoEmi"
        Secuencial = Value-Of "secuencial"
        ClaveResumen = $claveResumen
        TieneComprobanteInterno = [bool]($document.Descendants() |
            Where-Object { $_.Name.LocalName -eq "comprobante" } |
            Select-Object -First 1)
    }
}
