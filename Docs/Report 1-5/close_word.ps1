$ErrorActionPreference = "SilentlyContinue"
try {
    $word = [System.Runtime.InteropServices.Marshal]::GetActiveObject("Word.Application")
    if ($word) {
        Write-Host "Found Word Application via COM"
        foreach ($d in $word.Documents) {
            Write-Host "Open doc: " $d.FullName
        }
        $word.Quit([ref]$false)
        Write-Host "Closed Word gracefully."
    }
} catch {
    Write-Host "Error or no COM instance: " $_
}

