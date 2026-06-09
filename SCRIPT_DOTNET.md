# Explicación del Script `setup.ps1`

Este documento detalla paso a paso lo que hace el script de inicialización (`setup.ps1`) para preparar el entorno de desarrollo local.

## Resumen del Flujo de Ejecución

1.  **Verificación de Docker:**
    *   Ejecuta el comando `docker info` de forma silenciosa.
    *   Si falla, asume que Docker Desktop no está instalado o no está en ejecución. Muestra un error en rojo y detiene el script.

2.  **Solicitud de Credenciales (Interactivo):**
    *   Pide al usuario que ingrese un **nombre de usuario** para la base de datos (por defecto usa `sa` si se deja en blanco).
    *   Pide una **contraseña** de forma segura (oculta en la pantalla). Esta contraseña se utilizará tanto para el usuario creado como para el administrador raíz (`sa`) de SQL Server.

3.  **Generación del archivo `.env`:**
    *   Crea automáticamente un archivo `.env` en la raíz del proyecto.
    *   Guarda las credenciales ingresadas (`DB_USER`, `DB_PASSWORD`, `SA_PASSWORD`) en este archivo.
    *   *Nota:* Docker Compose leerá este archivo automáticamente para inyectar estas variables en los contenedores.

4.  **Creación de Carpetas de Volúmenes:**
    *   Verifica si existe la carpeta `./uploads_compartidos`.
    *   Si no existe, la crea. Esto evita errores de permisos o montajes fallidos cuando Docker intenta mapear el volumen para los PDFs.

5.  **Despliegue de Contenedores:**
    *   Ejecuta `docker-compose up -d --build`.
    *   Esto construye la imagen personalizada del backend (`Dockerfile.dev` que instala Node.js y .NET) y descarga la imagen de SQL Server. Luego, arranca ambos contenedores en segundo plano.

6.  **Espera de Inicialización de la BD:**
    *   El script se pausa durante 25 segundos. Esto da tiempo suficiente para que el contenedor de SQL Server termine su proceso interno de arranque antes de intentar interactuar con él.

7.  **Creación de Usuario en SQL Server (Opcional):**
    *   Si el usuario eligió un nombre distinto a `sa` en el paso 2, el script se conecta al contenedor de la base de datos usando `sqlcmd`.
    *   Ejecuta una consulta SQL para crear ese nuevo inicio de sesión, asignarle la contraseña proporcionada y darle permisos de administrador (`sysadmin`).

8.  **Inyección del Script de Base de Datos:**
    *   Busca el archivo `./QualityDoc/Database/script_utf8.sql`.
    *   Si lo encuentra, lo copia dentro del contenedor de la base de datos (`docker cp`).
    *   Ejecuta el archivo `.sql` copiado utilizando `sqlcmd` para crear las tablas, relaciones y datos iniciales requeridos por la aplicación.

9.  **Finalización:**
    *   Muestra un mensaje de éxito indicando que la aplicación está lista y escuchando en `http://localhost:5000`.
