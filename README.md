# QualityDoc

## Entorno de Desarrollo (Instalación)

Este proyecto está configurado para ejecutarse en contenedores Docker, lo que garantiza que todos los desarrolladores tengan el mismo entorno sin necesidad de instalar bases de datos o SDKs localmente. Sigue las instrucciones para tu sistema operativo.

---

### Instalación en Windows

#### Prerrequisitos (Windows)
Antes de comenzar, asegúrate de tener instalados:
1.  **[Git](https://git-scm.com/downloads)** (para clonar el repositorio).
2.  **[Docker Desktop](https://www.docker.com/products/docker-desktop/)**.
    *   *Nota:* Asegúrate de que Docker Desktop esté abierto y ejecutándose en segundo plano.

#### Pasos de Instalación (Windows)
1.  **Clonar el repositorio:**
    Abre una terminal y clona el proyecto:
    ```bash
    git clone https://github.com/AngelZurita28/QualityDocNet.git
    cd QualityDocNet
    ```

2.  **Ejecutar el script de configuración:**
    Abre **PowerShell** en la carpeta raíz del proyecto y ejecuta:
    ```powershell
    .\setup.ps1
    ```
    *Si PowerShell muestra un error de políticas de ejecución, ejecuta esto primero: `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass`*

---

### Instalación en Linux

El script de Bash (`setup.sh`) ha sido diseñado para ser universal y compatible con la mayoría de las distribuciones Linux, incluyendo (pero no limitándose a):
*   **Basadas en Debian:** Ubuntu, Linux Mint, Pop!_OS.
*   **Basadas en RHEL:** Fedora, CentOS, Rocky Linux.
*   **Arch Linux:** Manjaro, EndeavourOS, CachyOS.

#### Prerrequisitos (Linux)
1.  **Git** (ej. `sudo apt install git` o `sudo dnf install git`).
2.  **Docker y Docker Compose plugin** (No Docker Desktop, sino el motor nativo).
    *   *Consideración:* El servicio de Docker debe estar activo (`sudo systemctl start docker`).
    *   El script detectará automáticamente si necesitas usar `sudo` para ejecutar los comandos de Docker, aunque se recomienda añadir tu usuario al grupo `docker` (`sudo usermod -aG docker $USER`).

#### Pasos de Instalación (Linux)
1.  **Clonar el repositorio:**
    ```bash
    git clone <URL_DEL_REPOSITORIO>
    cd QualityDocNet
    ```

2.  **Dar permisos de ejecución y correr el script:**
    En la terminal, dale permisos al script de bash y ejecútalo:
    ```bash
    chmod +x setup.sh
    ./setup.sh
    ```

---

### Configuración Común y Ejecución

*   **Credenciales Seguras:** Ya sea en Windows o Linux, el script te pedirá un usuario y una contraseña para la base de datos local. La contraseña **debe** cumplir con los requisitos de SQL Server (mínimo 8 caracteres, al menos una mayúscula, una minúscula y un número o símbolo, ej: `MiPassword123!`).
*   **Acceso:** Una vez finalizado el proceso y que el terminal muestre "¡Entorno configurado con éxito!", abre tu navegador y visita:
    👉 **http://localhost:5000**

---

### Uso Diario (Cómo encender y apagar)

Una vez que hayas ejecutado el script de instalación (`setup`) por primera vez, **no necesitas volver a ejecutarlo** (a menos que quieras borrar tu base de datos y empezar de cero).

Para tu trabajo del día a día, abre una terminal en la carpeta `QualityDocNet` y utiliza los comandos estándar de Docker:

*   **Para Encender la App:**
    ```bash
    docker compose up -d
    ```
    *(Esto levantará la base de datos y la aplicación en segundo plano. La app recompilará automáticamente tus cambios en el código).*

*   **Para Apagar la App:**
    ```bash
    docker compose down
    ```
    *(Esto detendrá los contenedores, pero tus datos en la base de datos se mantendrán seguros).*
