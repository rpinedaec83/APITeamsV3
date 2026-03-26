Add-Type -Path "E:\APITEAMSV3\publish\api\Microsoft.Data.Sqlite.dll"
$connectionString = "Data Source=E:\APITEAMSV3\publish\api\APITeamsV3_Central.db"
$connection = New-Object Microsoft.Data.Sqlite.SqliteConnection($connectionString)
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandText = "SELECT * FROM CompanyConfigs"
    $reader = $command.ExecuteReader()
    $columns = $reader.GetColumnSchema()
    
    # Header
    $header = $columns.Name -join " | "
    Write-Host $header -ForegroundColor Cyan
    Write-Host ("-" * $header.Length)
    
    while ($reader.Read()) {
        $row = @()
        for ($i = 0; $i -lt $reader.FieldCount; $i++) {
            $row += $reader.GetValue($i).ToString()
        }
        $row -join " | "
    }
} finally {
    $connection.Close()
}
