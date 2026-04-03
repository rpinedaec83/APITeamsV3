Add-Type -Path "c:\Sources\APITeamsV3\APITeamsV3.API\bin\Debug\net8.0\Microsoft.Data.Sqlite.dll"
$connectionString = "Data Source=c:\Sources\APITeamsV3\APITeamsV3.API\Smart_IDAT.db"
$connection = New-Object Microsoft.Data.Sqlite.SqliteConnection($connectionString)
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandText = "SELECT Fecha, Tipo, EntidadAfectada, Referencia, Mensaje FROM TeamsLogOperativo ORDER BY Fecha DESC LIMIT 20"
    $reader = $command.ExecuteReader()
        
    while ($reader.Read()) {
        $fecha = $reader.GetString(0)
        $tipo = $reader.GetString(1)
        $entidad = $reader.GetString(2)
        $referencia =""
        if(!$reader.IsDBNull(3)) { $referencia = $reader.GetString(3) }
        $mensaje = $reader.GetString(4)
        Write-Host "[$fecha] $tipo | $entidad | $referencia | $mensaje"
    }
} finally {
    $connection.Close()
}
