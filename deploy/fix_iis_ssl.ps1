# Fix-IISSSL.ps1
# This script enables SNI and assigns the correct certificates to APITeamsV3 sites.

Import-Module WebAdministration

$certs = @{
    "idat.edu.pe"             = "CE3E6922C1D2AF0EED6D2128F817A117B4B8259D"
    "zegel.edu.pe"            = "081CD2FFCD1EBE909CC60F65233C4C53CF1C3787"
    "corrientealterna.edu.pe" = "7991039590CFD953FEA4E61FC062C3575AA093C3"
    "its.edu.pe"              = "FDBA73271A7A1607BA9783E5F9A51022EB56B558"
    "centrodelaimagen.pe"     = "EE1E07F50C0D5020460157B38EDED438B13B7153"
}

$sites = @("API", "FRONT")

foreach ($siteName in $sites) {
    try {
        $site = Get-Website -Name $siteName
    } catch {
        Write-Host "Error accessing site $siteName"
        continue
    }

    if ($null -eq $site) {
        Write-Host "Site $siteName not found."
        continue
    }

    foreach ($binding in $site.Bindings.Collection) {
        if ($binding.protocol -eq "https") {
            $hostname = $binding.bindingInformation.Split(':')[-1]
            if ([string]::IsNullOrWhiteSpace($hostname)) { continue }

            # Extract parent domain to find correct certificate
            $domain = ""
            if ($hostname -like "*idat.edu.pe*") { $domain = "idat.edu.pe" }
            elseif ($hostname -like "*zegel.edu.pe*") { $domain = "zegel.edu.pe" }
            elseif ($hostname -like "*corrientealterna.edu.pe*") { $domain = "corrientealterna.edu.pe" }
            elseif ($hostname -like "*its.edu.pe*") { $domain = "its.edu.pe" }
            elseif ($hostname -like "*centrodelaimagen.pe*") { $domain = "centrodelaimagen.pe" }

            if ($domain -ne "" -and $certs.ContainsKey($domain)) {
                $thumbprint = $certs[$domain]
                Write-Host "Updating $hostname in $siteName with $thumbprint (SNI Enabled)"
                
                try {
                    $bindingInfo = $binding.bindingInformation
                    
                    # Set SNI flag (1 = SNI)
                    Set-WebBinding -Name $siteName -BindingInformation $bindingInfo -PropertyName "sslFlags" -Value 1 -ErrorAction Stop
                    
                    # Bind certificate
                    $certInStore = Get-Item "Cert:\LocalMachine\My\$thumbprint"
                    $bindingObj = Get-WebBinding -Name $siteName -BindingInformation $bindingInfo
                    $bindingObj.AddSslCertificate($certInStore.GetCertHashString(), "My")
                    
                    Write-Host "Successfully updated $hostname"
                } catch {
                    $errText = $_.Exception.Message
                    Write-Host "Error updating $hostname : $errText"
                }
            }
        }
    }
}
Write-Host "Process finished."
