# =================================================================
# Script de Configuracion Inicial - QualityDoc
# =================================================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Iniciando configuracion del Proyecto" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. Verificar Docker
try {
    $null = docker info 2>&1
    if ($LASTEXITCODE -ne 0) { throw }
} catch {
    Write-Host "ERROR: Docker no esta ejecutandose." -ForegroundColor Red
    Read-Host "Presiona Enter para salir..."
    exit 1
}

# 2. Pedir credenciales con Validacion Estricta
$dbUser = Read-Host "Ingresa el USUARIO que deseas usar (Enter para usar 'sa')"
if ([string]::IsNullOrWhiteSpace($dbUser)) { $dbUser = "sa" }

$isValidPassword = $false
while (-not $isValidPassword) {
    Write-Host "`nLa contrasena debe tener: Minimo 8 caracteres, mayusculas, minusculas y numeros/simbolos." -ForegroundColor Yellow
    $dbPassword = Read-Host -AsSecureString "Ingresa la CONTRASENA"
    $dbPasswordPlain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($dbPassword))
    
    # Expresion regular para validar la complejidad requerida por SQL Server
    if ($dbPasswordPlain -match '^(?=.*[a-z])(?=.*[A-Z])(?=.*\d|.*[^\w\s]).{8,}$') {
        $isValidPassword = $true
        Write-Host "Formato de contrasena valido." -ForegroundColor Green
    } else {
        Write-Host "ERROR: La contrasena no cumple con los requisitos. Intenta de nuevo." -ForegroundColor Red
    }
}

$saPassword = $dbPasswordPlain 

# 3. Guardar en .env
Write-Host "`nGenerando archivo .env..."
Set-Content -Path ".env" -Value "DB_USER=$dbUser" -Encoding ascii
Add-Content -Path ".env" -Value "DB_PASSWORD=$dbPasswordPlain" -Encoding ascii
Add-Content -Path ".env" -Value "SA_PASSWORD=$saPassword" -Encoding ascii

if (-not (Test-Path -Path ".\uploads_compartidos")) { 
    New-Item -ItemType Directory -Force -Path ".\uploads_compartidos" | Out-Null 
}

# 4. Limpiar e Iniciar Docker
Write-Host "`nLimpiando contenedores anteriores (y volumenes) para evitar datos corruptos..." -ForegroundColor Cyan
docker-compose down -v
Write-Host "Levantando contenedores nuevos..." -ForegroundColor Cyan
docker-compose up -d --build

# 5. Bucle de Conexion y Configuracion
$maxAttempts = 20
$attempt = 1
$isConnected = $false

Write-Host "`nEsperando 15 segundos iniciales a que el motor arranque..."
Start-Sleep -Seconds 15

while ($attempt -le $maxAttempts -and -not $isConnected) {
    Write-Host "`n[Intento $attempt/$maxAttempts] Verificando conexion con la base de datos..."
    
    # Intentamos una consulta simple. Pasamos las credenciales por variables de entorno de Docker 
    # para evitar que PowerShell rompa los caracteres especiales en la linea de comandos.
    $checkQuery = "SELECT 1;"
    $checkResult = docker exec -e SQLCMDUSER=sa -e "SQLCMDPASSWORD=$saPassword" -i mssql_db /opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -C -Q "$checkQuery" 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "¡Conexion exitosa a SQL Server!" -ForegroundColor Green
        $isConnected = $true
        
        # Crear usuario personalizado si es necesario
        if ($dbUser -ne "sa") {
            Write-Host "Creando usuario '$dbUser' en la base de datos..."
            $sqlQuery = "IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = N'$dbUser') BEGIN CREATE LOGIN [$dbUser] WITH PASSWORD = '$dbPasswordPlain'; END; ALTER SERVER ROLE sysadmin ADD MEMBER [$dbUser];"
            $userResult = docker exec -e SQLCMDUSER=sa -e "SQLCMDPASSWORD=$saPassword" -i mssql_db /opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -C -Q "$sqlQuery" 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-Host "Advertencia al crear usuario: $userResult" -ForegroundColor Yellow
            } else {
                Write-Host "Usuario '$dbUser' verificado/creado con exito." -ForegroundColor Green
            }
        }

        # Importar el script de base de datos
        $scriptPath = "./QualityDoc/Database/script.sql"
        if (Test-Path -Path $scriptPath) {
            Write-Host "Importando script inicial de la base de datos..."
            docker cp $scriptPath mssql_db:/tmp/script.sql
            $importResult = docker exec -e SQLCMDUSER=sa -e "SQLCMDPASSWORD=$saPassword" -i mssql_db /opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -C -i /tmp/script.sql 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Host "¡Base de datos importada!" -ForegroundColor Green
            } else {
                Write-Host "Hubo un error importando el script: $importResult" -ForegroundColor Red
            }
        }
    } else {
        Write-Host "El motor aun no esta listo o rechazo la conexion. Esperando 10 segundos..." -ForegroundColor Yellow
        Write-Host "(Mensaje interno: $checkResult)" -ForegroundColor DarkGray
        Start-Sleep -Seconds 10
        $attempt++
    }
}

if (-not $isConnected) {
    Write-Host "`nERROR FATAL: No se pudo conectar a la base de datos despues de $maxAttempts intentos." -ForegroundColor Red
    Write-Host "Es posible que la contrasena tenga algun caracter no compatible o Docker este fallando." -ForegroundColor Red
} else {
    Write-Host "`n========================================" -ForegroundColor Green
    Write-Host "  ¡Entorno configurado con exito!" -ForegroundColor Green
    Write-Host "  La app esta disponible en: http://localhost:5000" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Green
}

Read-Host "Presiona Enter para salir..."
