# Fix-IIS-SSL-Final.ps1
# This script uses appcmd and netsh to ensure all APITeamsV3 hostnames have SNI enabled and the correct certificate.

$certs = @{
    "idat.edu.pe"             = "CE3E6922C1D2AF0EED6D2128F817A117B4B8259D"
    "zegel.edu.pe"            = "081CD2FFCD1EBE909CC60F65233C4C53CF1C3787"
    "corrientealterna.edu.pe" = "7991039590CFD953FEA4E61FC062C3575AA093C3"
    "its.edu.pe"              = "FDBA73271A7A1607BA9783E5F9A51022EB56B558"
    "centrodelaimagen.pe"     = "EE1E07F50C0D5020460157B38EDED438B13B7153"
}

$sites = @{
    "API"   = @("api.teams.zegel.edu.pe", "api.teams.idat.edu.pe", "api.teams.corrientealterna.edu.pe", "api.teams.its.edu.pe", "api.teams.centrodelaimagen.pe")
    "FRONT" = @("teams.zegel.edu.pe", "teams.idat.edu.pe", "teams.corrientealterna.edu.pe", "teams.its.edu.pe", "teams.centrodelaimagen.pe")
}

$appcmd = "C:\Windows\System32\inetsrv\appcmd"
$appid = "{4dc3e181-e14b-4a21-b022-59fc669b0914}" # Taken from previous netsh show output

foreach ($siteName in $sites.Keys) {
    foreach ($hostname in $sites[$siteName]) {
        
        # Determine parent domain to select certificate
        $domain = ""
        if ($hostname -like "*idat.edu.pe*") { $domain = "idat.edu.pe" }
        elseif ($hostname -like "*zegel.edu.pe*") { $domain = "zegel.edu.pe" }
        elseif ($hostname -like "*corrientealterna.edu.pe*") { $domain = "corrientealterna.edu.pe" }
        elseif ($hostname -like "*its.edu.pe*") { $domain = "its.edu.pe" }
        elseif ($hostname -like "*centrodelaimagen.pe*") { $domain = "centrodelaimagen.pe" }

        if ($domain -eq "" -or -not $certs.ContainsKey($domain)) {
            Write-Host "No certificate mapping found for $hostname" -ForegroundColor Yellow
            continue
        }

        $thumbprint = $certs[$domain]
        Write-Host "Processing $hostname (Site: $siteName, Cert: $thumbprint)..." -ForegroundColor Cyan

        # 1. Enable SNI in IIS Config (appcmd)
        $bindingInfo = "*:443:$hostname"
        & $appcmd set site /site.name:$siteName /"bindings.[protocol='https',bindingInformation='$bindingInfo'].sslFlags:1" 
        
        # 2. Add SSL certificate to hostname (netsh)
        # We try to remove existing one first to ensure it's updated with the correct hash
        & netsh http delete sslcert hostnameport=$hostname:443 2>$null
        & netsh http add sslcert hostnameport=$hostname:443 certhash=$thumbprint appid=$appid certstorename=My
        
        Write-Host "Done with $hostname" -ForegroundColor Green
    }
}

Write-Host "IIS SSL Fix process finished." -ForegroundColor Green
