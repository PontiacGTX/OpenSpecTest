<#
.SYNOPSIS
    Script de orquestación de prueba E2E para DAM Anomaly Engine (OpenSpecTest).
#>

$ErrorActionPreference = "Stop"
$null = chcp 65001
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$SolutionRoot = $PSScriptRoot
$ApiPath      = Join-Path $SolutionRoot "OpenSpecTest"

# 1. Búsqueda dinámica de docker-compose.yml / .yaml
$possibleComposePaths = @(
    "$ApiPath\docker-compose.yml",
    "$ApiPath\docker-compose.yaml",
    "$SolutionRoot\docker-compose.yml",
    "$SolutionRoot\docker-compose.yaml"
)

$DockerComposeFile = $possibleComposePaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $DockerComposeFile) {
    throw "No se encontró 'docker-compose.yml' ni en '$ApiPath' ni en '$SolutionRoot'."
}

# 2. Búsqueda dinámica de init-audit.sql
$possibleSqlPaths = @(
    "$ApiPath\init-audit.sql",
    "$SolutionRoot\init-audit.sql"
)

$InitAuditSqlFile = $possibleSqlPaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $InitAuditSqlFile) {
    throw "No se encontró 'init-audit.sql' ni en '$ApiPath' ni en '$SolutionRoot'."
}

$ContainerName = "anomaly_sqlserver"
$ApiContainerName = "openspec_api"
$SqlPassword =  "SecurePassword123!"
$ApiUrl        = "http://localhost:5000"

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " [1/5] Levantando Infraestructura (Docker Compose) " -ForegroundColor Cyan
Write-Host " Compose File: $DockerComposeFile" -ForegroundColor Gray
Write-Host "====================================================" -ForegroundColor Cyan

docker compose -f $DockerComposeFile up -d --build
Write-Host "`n====================================================" -ForegroundColor Yellow
Write-Host " [2/5] Esperando a que SQL Server esté disponible... " -ForegroundColor Yellow
Write-Host "====================================================" -ForegroundColor Yellow

$maxRetries = 20
$retryCount = 0
$isReady = $false

# Desactivar temporalmente el paro automático por stderr en comandos nativos
$oldPreference = $ErrorActionPreference
$ErrorActionPreference = "SilentlyContinue"

while (-not $isReady -and $retryCount -lt $maxRetries) {
    $retryCount++
    Start-Sleep -Seconds 3
    Write-Host "Verificando SQL Server (Intento $retryCount/$maxRetries)..." -ForegroundColor Gray
    
    # Redirección de stderr a nivel de proceso (2>NUL)
    $null = cmd /c "docker exec $ContainerName /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SqlPassword -C -Q `"SELECT 1`" 2>NUL"
    
    if ($LASTEXITCODE -eq 0) {
        $isReady = $true
        Write-Host "-> SQL Server está en línea." -ForegroundColor Green
    }
}

$ErrorActionPreference = $oldPreference

if (-not $isReady) {
    throw "SQL Server no respondió a tiempo. Revisa: docker logs $ContainerName"
}

Write-Host "`n====================================================" -ForegroundColor Cyan
Write-Host " [3/5] Aplicando Configuración de Auditoría (SQL)  " -ForegroundColor Cyan
Write-Host " SQL File: $InitAuditSqlFile" -ForegroundColor Gray
Write-Host "====================================================" -ForegroundColor Cyan

docker cp $InitAuditSqlFile "${ContainerName}:/tmp/init-audit.sql"
docker exec $ContainerName /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SqlPassword -C -b -i /tmp/init-audit.sql

if ($LASTEXITCODE -ne 0) {
    throw "Error al ejecutar 'init-audit.sql' en el contenedor SQL."
}

Write-Host "-> Auditoría configurada exitosamente." -ForegroundColor Green

Write-Host "`n====================================================" -ForegroundColor Magenta
Write-Host " [4/5] Esperando OpenSpec.API en Docker (.NET 8)     " -ForegroundColor Magenta
Write-Host "====================================================" -ForegroundColor Magenta

Write-Host "Esperando inicio de la API en $ApiUrl..." -ForegroundColor Gray
$apiReady = $false
$apiRetries = 0

while (-not $apiReady -and $apiRetries -lt 15) {
    $apiRetries++
    Start-Sleep -Seconds 3
    try {
        $response = Invoke-WebRequest -Uri "$ApiUrl/swagger/index.html" -Method Get -TimeoutSec 2 -UseBasicParsing -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            $apiReady = $true
            Write-Host "-> OpenSpec.API lista (Swagger accesible)." -ForegroundColor Green
        }
    } catch { }
}

if (-not $apiReady) {
    throw "OpenSpec.API no respondió a tiempo. Revisa: docker logs openspec_api"
}

Write-Host "`n====================================================" -ForegroundColor Green
Write-Host " [5/5] Ejecutando Escenarios de Tráfico Anómalo      " -ForegroundColor Green
Write-Host "====================================================" -ForegroundColor Green

try {
    $triggerEndpoint = "$ApiUrl/api/test/traffic/run?scenario=FullSimulation&iterations=5"
    Invoke-RestMethod -Uri $triggerEndpoint -Method Post -ErrorAction Stop | Out-Null

    Write-Host "Escenarios iniciados. Esperando procesamiento de auditoría..." -ForegroundColor Gray
    Start-Sleep -Seconds 15

    Write-Host "`n-------------------- Salida del motor --------------------" -ForegroundColor White
    docker logs --tail 300 $ApiContainerName 2>&1 |
        Select-String -Pattern "Motor de monitoreo|procesando eventos|INFO \| baseline|ALERT|CRIT|Resumen batch|Usuarios analizados|Análisis Ollama|Harness de Generación" |
        ForEach-Object { Write-Host $_.Line }
    Write-Host "-----------------------------------------------------------" -ForegroundColor White
}
catch {
    throw "No se pudieron iniciar los escenarios mediante la API: $($_.Exception.Message)"
}

Write-Host "`nProceso completado." -ForegroundColor Cyan