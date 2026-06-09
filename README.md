# QualityDoc

## Instalación en Windows (Entorno de Desarrollo)

Este proyecto está configurado para ejecutarse en contenedores Docker, lo que garantiza que todos los desarrolladores tengan el mismo entorno sin necesidad de instalar bases de datos o SDKs localmente.

### Prerrequisitos
Antes de comenzar, asegúrate de tener instalados:
1.  **[Git](https://git-scm.com/downloads)** (para clonar el repositorio).
2.  **[Docker Desktop](https://www.docker.com/products/docker-desktop/)**.
    *   *Nota:* Asegúrate de que Docker Desktop esté abierto y ejecutándose en segundo plano antes de continuar.

### Pasos de Instalación

1.  **Clonar el repositorio:**
    Abre una terminal y clona el proyecto en tu máquina:
    ```bash
    git clone <URL_DEL_REPOSITORIO>
    cd QualityDocNet
    ```

2.  **Ejecutar el script de configuración:**
    Hemos preparado un script automático que levantará la base de datos, compilará el código y configurará todo por ti.
    
    Abre **PowerShell** en la carpeta raíz del proyecto (`QualityDocNet`) y ejecuta:
    ```powershell
    .\setup.ps1
    ```

    *Si PowerShell te muestra un error en rojo diciendo que "la ejecución de scripts está deshabilitada en este sistema", ejecuta este comando primero para darle permiso temporal:*
    ```powershell
    Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
    ```
    *Y luego vuelve a intentar ejecutar `.\setup.ps1`.*

3.  **Configurar Credenciales:**
    El script te pedirá que elijas un usuario y una contraseña para la base de datos local (puedes dejar el usuario por defecto `sa` presionando Enter). **Importante:** SQL Server requiere contraseñas seguras (mínimo 8 caracteres, mayúsculas, minúsculas y números, por ejemplo: `MiPasswordSeguro123!`).

4.  **Esperar y Probar:**
    El script descargará las imágenes de Docker, instalará las dependencias de Node.js (para TailwindCSS), compilará el backend en `.NET` y ejecutará los scripts SQL iniciales de la base de datos.
    
    Una vez que el script termine y diga "¡Entorno configurado con éxito!", abre tu navegador y visita:
    👉 **http://localhost:5000**
