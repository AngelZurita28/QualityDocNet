#!/bin/bash

# =================================================================
# Script de Configuracion Inicial - QualityDoc (Linux)
# =================================================================

CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN}  Iniciando configuracion del Proyecto  ${NC}"
echo -e "${CYAN}========================================${NC}"

# 1. Verificar Docker y permisos de sudo
DOCKER_CMD="docker"
if ! docker info > /dev/null 2>&1; then
    if sudo docker info > /dev/null 2>&1; then
        echo -e "${YELLOW}Aviso: Docker requiere privilegios de superusuario. Se usara 'sudo' para los comandos de Docker.${NC}"
        DOCKER_CMD="sudo docker"
    else
        echo -e "${RED}ERROR: Docker no esta ejecutandose o no esta instalado.${NC}"
        echo -e "${RED}Asegurate de iniciar el servicio de Docker (ej. systemctl start docker).${NC}"
        exit 1
    fi
fi

if $DOCKER_CMD compose version > /dev/null 2>&1; then
    DOCKER_COMPOSE_CMD="$DOCKER_CMD compose"
elif command -v docker-compose > /dev/null 2>&1; then
    if [ "$DOCKER_CMD" = "sudo docker" ]; then
        DOCKER_COMPOSE_CMD="sudo docker-compose"
    else
        DOCKER_COMPOSE_CMD="docker-compose"
    fi
else
    echo -e "${RED}ERROR: No se encontro 'docker compose' ni 'docker-compose'. Por favor instala Docker Compose.${NC}"
    exit 1
fi

# 2. Pedir credenciales con Validacion Estricta
echo -e "\n${GREEN}Configuracion de la Base de Datos:${NC}"
read -p "Ingresa el USUARIO que deseas usar (Enter para usar 'sa'): " dbUser
if [ -z "$dbUser" ]; then
    dbUser="sa"
fi

isValidPassword=false
while [ "$isValidPassword" = false ]; do
    echo -e "\n${YELLOW}La contrasena debe tener: Minimo 8 caracteres, mayusculas, minusculas y numeros/simbolos.${NC}"
    read -p "Ingresa la CONTRASENA: " dbPasswordPlain
    
    # Expresion regular (Perl Regex) para validar complejidad
    if echo "$dbPasswordPlain" | grep -Pq '^(?=.*[a-z])(?=.*[A-Z])(?=.*\d|.*[^\w\s]).{8,}$'; then
        isValidPassword=true
        echo -e "${GREEN}Formato de contrasena valido.${NC}"
    else
        echo -e "${RED}ERROR: La contrasena no cumple con los requisitos. Intenta de nuevo.${NC}"
    fi
done

saPassword="$dbPasswordPlain"

# 3. Guardar en .env
echo -e "\nGenerando archivo .env..."
cat <<EOF > .env
DB_USER=$dbUser
DB_PASSWORD=$dbPasswordPlain
SA_PASSWORD=$saPassword
EOF

if [ ! -d "./uploads_compartidos" ]; then
    mkdir -p "./uploads_compartidos"
    # Asegurar permisos abiertos temporalmente para evitar problemas de escritura desde el contenedor
    chmod 777 "./uploads_compartidos"
fi

# 4. Limpiar e Iniciar Docker
echo -e "\n${CYAN}Limpiando contenedores anteriores (y volumenes) para evitar datos corruptos...${NC}"
$DOCKER_COMPOSE_CMD down -v
echo -e "${CYAN}Levantando contenedores nuevos...${NC}"
$DOCKER_COMPOSE_CMD up -d --build

# 5. Bucle de Conexion y Configuracion
maxAttempts=20
attempt=1
isConnected=false

echo -e "\nEsperando 15 segundos iniciales a que el motor arranque..."
sleep 15

while [ $attempt -le $maxAttempts ] && [ "$isConnected" = false ]; do
    echo -e "\n[Intento $attempt/$maxAttempts] Verificando conexion con la base de datos..."
    
    checkQuery="SELECT 1;"
    checkResult=$($DOCKER_CMD exec -e SQLCMDUSER=sa -e "SQLCMDPASSWORD=$saPassword" -i mssql_db /opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -C -Q "$checkQuery" 2>&1)
    exitCode=$?
    
    if [ $exitCode -eq 0 ]; then
        echo -e "${GREEN}¡Conexion exitosa a SQL Server!${NC}"
        isConnected=true
        
        # Crear usuario personalizado si es necesario
        if [ "$dbUser" != "sa" ]; then
            echo "Creando usuario '$dbUser' en la base de datos..."
            sqlQuery="IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = N'$dbUser') BEGIN CREATE LOGIN [$dbUser] WITH PASSWORD = '$dbPasswordPlain'; END; ALTER SERVER ROLE sysadmin ADD MEMBER [$dbUser];"
            userResult=$($DOCKER_CMD exec -e SQLCMDUSER=sa -e "SQLCMDPASSWORD=$saPassword" -i mssql_db /opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -C -Q "$sqlQuery" 2>&1)
            if [ $? -ne 0 ]; then
                echo -e "${YELLOW}Advertencia al crear usuario: $userResult${NC}"
            else
                echo -e "${GREEN}Usuario '$dbUser' verificado/creado con exito.${NC}"
            fi
        fi

        # Importar el script de base de datos
        scriptPath="./QualityDoc/Database/script.sql"
        if [ -f "$scriptPath" ]; then
            echo "Importando script inicial de la base de datos..."
            $DOCKER_CMD cp "$scriptPath" mssql_db:/tmp/script.sql
            importResult=$($DOCKER_CMD exec -e SQLCMDUSER=sa -e "SQLCMDPASSWORD=$saPassword" -i mssql_db /opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -C -i /tmp/script.sql 2>&1)
            if [ $? -eq 0 ]; then
                echo -e "${GREEN}¡Base de datos importada!${NC}"
            else
                echo -e "${RED}Hubo un error importando el script: $importResult${NC}"
            fi
        fi
    else
        echo -e "${YELLOW}El motor aun no esta listo o rechazo la conexion. Esperando 10 segundos...${NC}"
        echo -e "\033[1;30m(Mensaje interno: $checkResult)\033[0m" # Dark Gray
        sleep 10
        attempt=$((attempt + 1))
    fi
done

if [ "$isConnected" = false ]; then
    echo -e "\n${RED}ERROR FATAL: No se pudo conectar a la base de datos despues de $maxAttempts intentos.${NC}"
    echo -e "${RED}Es posible que la contrasena tenga algun caracter no compatible o Docker este fallando.${NC}"
else
    echo -e "\n${GREEN}========================================${NC}"
    echo -e "${GREEN}  ¡Entorno configurado con exito!       ${NC}"
    echo -e "${CYAN}  La app esta disponible en: http://localhost:5000${NC}"
    echo -e "${GREEN}========================================${NC}"
fi
