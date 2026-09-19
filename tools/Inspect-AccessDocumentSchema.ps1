param(
    [Parameter(Mandatory = $true)]
    [string]$Path,
    [string]$Password = "",
    [switch]$IncludeSamples,
    [switch]$ListTables
)

$ErrorActionPreference = "Stop"
$providers = @(
    "Microsoft.ACE.OLEDB.16.0",
    "Microsoft.ACE.OLEDB.12.0",
    "Microsoft.Jet.OLEDB.4.0"
)

$connection = $null
foreach ($provider in $providers) {
    $connectionString = "Provider=$provider;Data Source=$Path;"
    if (-not [string]::IsNullOrWhiteSpace($Password)) {
        $connectionString += "Jet OLEDB:Database Password=$Password;"
    }

    try {
        $candidate = New-Object System.Data.OleDb.OleDbConnection($connectionString)
        $candidate.Open()
        $connection = $candidate
        Write-Output "PROVIDER=$provider"
        break
    }
    catch {
        if ($null -ne $candidate) { $candidate.Dispose() }
        Write-Output "PROVIDER_ERROR=$provider :: $($_.Exception.Message)"
    }
}

if ($null -eq $connection) { throw "No fue posible abrir la base." }

try {
    if ($ListTables) {
        try {
            $command = $connection.CreateCommand()
            $command.CommandText = "SELECT Name, Type, Database, ForeignName FROM MSysObjects WHERE Type IN (1, 4, 6) AND Left(Name, 4) <> 'MSys' ORDER BY Name"
            $adapter = New-Object System.Data.OleDb.OleDbDataAdapter($command)
            $data = New-Object System.Data.DataTable
            [void]$adapter.Fill($data)
            $data | Format-Table -AutoSize | Out-String -Width 300 | Write-Output
            $adapter.Dispose()
            $command.Dispose()
        }
        catch {
            Write-Warning "TABLE_LIST_ERROR :: $($_.Exception.Message)"
        }
    }

    $columns = foreach ($tableName in @("NotaCredito", "GuiaRemision", "CgRetencion")) {
        try {
            $command = $connection.CreateCommand()
            $command.CommandText = "SELECT * FROM [$tableName] WHERE 1 = 0"
            $reader = $command.ExecuteReader([System.Data.CommandBehavior]::SchemaOnly)
            foreach ($row in $reader.GetSchemaTable().Rows) {
                [pscustomobject]@{
                    TABLE_NAME = $tableName
                    COLUMN_NAME = $row["ColumnName"]
                    DATA_TYPE = $row["DataType"].Name
                    CHARACTER_MAXIMUM_LENGTH = $row["ColumnSize"]
                    NUMERIC_PRECISION = $row["NumericPrecision"]
                    NUMERIC_SCALE = $row["NumericScale"]
                    IS_NULLABLE = $row["AllowDBNull"]
                    ORDINAL_POSITION = $row["ColumnOrdinal"] + 1
                }
            }
            $reader.Dispose()
            $command.Dispose()
        }
        catch {
            Write-Warning "TABLE_ERROR=$tableName :: $($_.Exception.Message)"
        }
    }

    $columns = $columns |
        Sort-Object TABLE_NAME, ORDINAL_POSITION |
        Select-Object TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH,
            NUMERIC_PRECISION, NUMERIC_SCALE, IS_NULLABLE, ORDINAL_POSITION

    $columns | Format-Table -AutoSize | Out-String -Width 240 | Write-Output

    if ($IncludeSamples) {
        foreach ($query in @(
            "SELECT TOP 10 numnota, id, caja, fecha, cancelado FROM [NotaCredito] ORDER BY fecha DESC",
            "SELECT TOP 10 numGuia, numFac, codEstablecimientoDestino, fechaEmisionDocSustento, fechaIniTransporte, fechaFinTransporte FROM [GuiaRemision] ORDER BY Id DESC"
        )) {
            try {
                $command = $connection.CreateCommand()
                $command.CommandText = $query
                $adapter = New-Object System.Data.OleDb.OleDbDataAdapter($command)
                $data = New-Object System.Data.DataTable
                [void]$adapter.Fill($data)
                $data | Format-Table -AutoSize | Out-String -Width 240 | Write-Output
                $adapter.Dispose()
                $command.Dispose()
            }
            catch {
                Write-Warning "SAMPLE_ERROR :: $($_.Exception.Message)"
            }
        }
    }
}
finally {
    $connection.Dispose()
}
