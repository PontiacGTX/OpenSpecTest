<#
.SYNOPSIS
    Script de orquestación de prueba E2E para DAM Anomaly Engine (OpenSpecTest).
#>

$ErrorActionPreference = "Stop"

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
$SqlPassword =  "SecurePassword123!"
$ApiUrl        = "http://localhost:5000"

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " [1/5] Levantando Infraestructura (Docker Compose) " -ForegroundColor Cyan
Write-Host " Compose File: $DockerComposeFile" -ForegroundColor Gray
Write-Host "====================================================" -ForegroundColor Cyan

docker compose -f $DockerComposeFile up -d
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
docker exec $ContainerName /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SqlPassword -C -i /tmp/init-audit.sql

if ($LASTEXITCODE -ne 0) {
    throw "Error al ejecutar 'init-audit.sql' en el contenedor SQL."
}

Write-Host "-> Auditoría configurada exitosamente." -ForegroundColor Green

Write-Host "`n====================================================" -ForegroundColor Magenta
Write-Host " [4/5] Compilando e Iniciando OpenSpec.API (.NET 8)  " -ForegroundColor Magenta
Write-Host "====================================================" -ForegroundColor Magenta

$apiProcess = Start-Process dotnet -ArgumentList "run --project `"$ApiPath/OpenSpec.API.csproj`"" -PassThru -NoNewWindow

Write-Host "Esperando inicio de la API en $ApiUrl..." -ForegroundColor Gray
$apiReady = $false
$apiRetries = 0

while (-not $apiReady -and $apiRetries -lt 15) {
    $apiRetries++
    Start-Sleep -Seconds 3
    try {
        $response = Invoke-WebRequest -Uri "$ApiUrl/swagger/index.html" -Method Get -TimeoutSec 2 -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            $apiReady = $true
            Write-Host "-> OpenSpec.API lista (Swagger accesible)." -ForegroundColor Green
        }
    } catch { }
}

Write-Host "`n====================================================" -ForegroundColor Green
Write-Host " [5/5] Ejecutando Escenarios de Tráfico Anómalo      " -ForegroundColor Green
Write-Host "====================================================" -ForegroundColor Green

try {
    $triggerEndpoint = "$ApiUrl/api/v1/trafficgenerator/execute-all"
    $response = Invoke-RestMethod -Uri $triggerEndpoint -Method Post -ErrorAction SilentlyContinue
    $response | ConvertTo-Json -Depth 3 | Write-Host -ForegroundColor White
}
catch {
    Write-Host "Ejecutando generador directamente por script si está disponible..." -ForegroundColor Yellow
    $GeneratorScript = Join-Path $SolutionRoot "scripts\Invoke-DamTrafficGenerator.ps1"
    if (Test-Path $GeneratorScript) {
        & $GeneratorScript -Scenario All -Mode Direct -ConnectionString "Server=localhost,1433;Database=AnomalyTestDb;User Id=sa;Password=$SqlPassword;TrustServerCertificate=True;"
    } else {
        Write-Warning "Finalizado. Ejecuta las pruebas desde el Swagger UI."
    }
}

Write-Host "`nProceso completado." -ForegroundColor Cyan