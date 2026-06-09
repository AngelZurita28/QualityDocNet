# =================================================================
# Script de Configuracion Inicial - QualityDoc
# =================================================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Iniciando configuracion del Proyecto" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. Verificar si Docker esta corriendo
try {
    $null = docker info 2>&1
    if ($LASTEXITCODE -ne 0) { throw }
} catch {
    Write-Host "ERROR: Docker no esta ejecutandose o no esta instalado." -ForegroundColor Red
    Write-Host "Por favor, abre Docker Desktop y vuelve a ejecutar este script." -ForegroundColor Yellow
    Read-Host "Presiona Enter para salir..."
    exit
}

# 2. Pedir credenciales
Write-Host "`nConfiguracion de la Base de Datos:" -ForegroundColor Green
$dbUser = Read-Host "Ingresa el USUARIO que deseas usar (ej. sa)"
if ([string]::IsNullOrWhiteSpace($dbUser)) { $dbUser = "sa" }

Write-Host "`nNOTA: SQL Server requiere una contrasena fuerte (minimo 8 caracteres, mayusculas, minusculas y numeros)." -ForegroundColor Yellow
$dbPassword = Read-Host -AsSecureString "Ingresa la CONTRASENA"
$dbPasswordPlain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($dbPassword))

# Usamos la misma contraseña para el administrador root (sa) del contenedor
$saPassword = $dbPasswordPlain 

# 3. Guardar credenciales en un archivo .env (Docker Compose lo lee automaticamente)
Write-Host "`nGenerando archivo de entorno (.env)..."
Set-Content -Path ".env" -Value "DB_USER=$dbUser" -Encoding ascii
Add-Content -Path ".env" -Value "DB_PASSWORD=$dbPasswordPlain" -Encoding ascii
Add-Content -Path ".env" -Value "SA_PASSWORD=$saPassword" -Encoding ascii

# 4. Crear carpeta de volúmenes si no existe
if (-not (Test-Path -Path ".\uploads_compartidos")) {
    New-Item -ItemType Directory -Force -Path ".\uploads_compartidos" | Out-Null
    Write-Host "Directorio 'uploads_compartidos' creado."
}

# 5. Iniciar Docker Compose
Write-Host "`nLevantando contenedores... (Esto puede tomar unos minutos la primera vez)" -ForegroundColor Cyan
docker-compose up -d --build

# 6. Esperar a que el motor de base de datos arranque bien
Write-Host "`nEsperando 35 segundos a que SQL Server inicie correctamente..."
Start-Sleep -Seconds 35

# 7. Crear el usuario personalizado si no eligió 'sa'
if ($dbUser -ne "sa") {
    Write-Host "Creando usuario '$dbUser' en la base de datos..."
    $sqlQuery = "IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = N'$dbUser') BEGIN CREATE LOGIN [$dbUser] WITH PASSWORD = '$dbPasswordPlain'; END; ALTER SERVER ROLE sysadmin ADD MEMBER [$dbUser];"
    $execResult = docker exec -i mssql_db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$saPassword" -C -Q "$sqlQuery"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Advertencia: Hubo un problema al crear el usuario. Revisa los logs: $execResult" -ForegroundColor Yellow
    } else {
        Write-Host "Usuario '$dbUser' verificado/creado con exito." -ForegroundColor Green
    }
}

# 8. Importar el script de base de datos
$scriptPath = ".\QualityDoc\Database\script_utf8.sql"
if (Test-Path -Path $scriptPath) {
    Write-Host "Importando script inicial de la base de datos..."
    # Copiamos el archivo al contenedor para evitar problemas de codificación de Windows a Linux
    docker cp $scriptPath mssql_db:/tmp/script.sql
    # Ejecutamos el archivo desde adentro del contenedor
    docker exec -i mssql_db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$saPassword" -C -i /tmp/script.sql
    Write-Host "¡Base de datos importada!" -ForegroundColor Green
} else {
    Write-Host "Aviso: No se encontro el archivo script_utf8.sql en $scriptPath" -ForegroundColor Yellow
}

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  ¡Entorno configurado con exito!" -ForegroundColor Green
Write-Host "  La app esta disponible en: http://localhost:5000" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Green
Write-Host "Presiona cualquier tecla para cerrar esta ventana..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
