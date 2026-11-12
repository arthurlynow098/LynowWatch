<#
  Instala o LynowWatch como serviço do Windows.
  Execute em um PowerShell como administrador, na pasta extraída do pacote:
    .\instalar-servico.ps1
#>
#Requires -RunAsAdministrator
$ErrorActionPreference = "Stop"

$pasta = Join-Path $env:ProgramFiles "LynowWatch"
$dados = Join-Path $env:ProgramData "LynowWatch"
$config = Join-Path $dados "lynowwatch.json"

New-Item -ItemType Directory -Force -Path $pasta, $dados | Out-Null
Copy-Item -Force (Join-Path $PSScriptRoot "LynowWatch.exe") $pasta

if (-not (Test-Path $config)) {
    Copy-Item (Join-Path $PSScriptRoot "lynowwatch.json") $config
    Write-Host "Configuração de exemplo copiada para $config"
    Write-Host "Ajuste as pastas, rode 'LynowWatch simular' e execute este script de novo."
    notepad $config
    exit 0
}

& (Join-Path $pasta "LynowWatch.exe") verificar --config $config
if ($LASTEXITCODE -ne 0) { throw "Corrija a configuração antes de instalar o serviço." }

if (Get-Service LynowWatch -ErrorAction SilentlyContinue) {
    Stop-Service LynowWatch
    sc.exe delete LynowWatch | Out-Null
    Start-Sleep -Seconds 2
}

New-Service -Name LynowWatch `
    -DisplayName "LynowWatch" `
    -Description "Organiza, copia e compacta arquivos das pastas configuradas em $config" `
    -BinaryPathName "`"$pasta\LynowWatch.exe`" --config `"$config`"" `
    -StartupType Automatic | Out-Null

# Reinicia sozinho depois de uma falha.
sc.exe failure LynowWatch reset= 86400 actions= restart/60000/restart/60000/""/0 | Out-Null
Start-Service LynowWatch
Write-Host "LynowWatch instalado e em execução."
