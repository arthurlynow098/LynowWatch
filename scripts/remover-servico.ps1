<#
  Remove o serviço LynowWatch. A configuração e o registro em %ProgramData%\LynowWatch são mantidos.
#>
#Requires -RunAsAdministrator
$ErrorActionPreference = "Stop"

if (Get-Service LynowWatch -ErrorAction SilentlyContinue) {
    Stop-Service LynowWatch -ErrorAction SilentlyContinue
    sc.exe delete LynowWatch | Out-Null
    Write-Host "Serviço removido."
}
Remove-Item -Recurse -Force (Join-Path $env:ProgramFiles "LynowWatch") -ErrorAction SilentlyContinue
Write-Host "Arquivos do programa removidos. Configuração e registro continuam em $env:ProgramData\LynowWatch."
