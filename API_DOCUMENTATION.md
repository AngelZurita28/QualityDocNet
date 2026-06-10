# Documentación de la API de Documentos (`DocumentController`)

Esta documentación describe la estructura, el funcionamiento y el uso de los endpoints administrados por [DocumentController](file:///c:/xampp/htdocs/miseria/qualitydoc/controllers/DocumentController.php) dentro del módulo de gestión y control de calidad de documentos.

---

## 📌 Resumen General

El controlador [DocumentController](file:///c:/xampp/htdocs/miseria/qualitydoc/controllers/DocumentController.php) actúa como el intermediario entre el modelo de datos de documentos ([Document](file:///c:/xampp/htdocs/miseria/qualitydoc/models/Document.php)) y las vistas correspondientes. Proporciona funcionalidades clave para:
1. Listar los documentos más recientes.
2. Visualizar un documento específico, registrando su acceso en la tabla de auditoría.
3. Consultar el historial de versiones del documento consultado.
4. Generar acuses de lectura y comprensión de los documentos por parte de los usuarios.
5. Servir el archivo físico/digital proxyando la descarga desde el servidor de login remoto.
6. Sincronizar/subir documentos desde la API de C# externa.

---

## 🛠️ Enrutamiento y Entrada Principal

El proyecto está configurado para ejecutarse en Docker bajo el modo de red de host (`network_mode: host`). Esto significa que los contenedores se enlazan directamente a los puertos de tu máquina local sin necesidad de redirigir puertos a través de la red del puente (bridge).

* **URL Base de la Aplicación (Local):** `http://localhost:8080/index.php` (o bien utilizando la IP de tu máquina `http://<IP_MAQUINA>:8080/index.php`)
* **Parámetro de Acción (`action`):** Define qué método de [DocumentController](file:///c:/xampp/htdocs/miseria/qualitydoc/controllers/DocumentController.php) se ejecuta.

| Acción (`action`) | Método del Controlador | Descripción |
| :--- | :--- | :--- |
| `index` *(por defecto)* | `index()` | Muestra el listado de documentos vigentes. |
| `view` | `view()` | Muestra la vista detallada de un documento, su historial y audita la acción. |
| `acknowledge` | `acknowledge()` | Registra la confirmación de lectura y comprensión del documento. |
| `serve_file` | `serveFile()` | Recupera un archivo de la API remota (`API_LOGIN_URI` + `file_path`) y lo transmite al navegador del cliente. |

---

## 📡 Referencia de Endpoints

### 1. Listar Documentos Vigentes (`index`)

Muestra todos los documentos del sistema cuya versión es la más reciente (`is_latest = TRUE`).

* **Método HTTP:** `GET`
* **Parámetro de Acción:** `action=index` (o sin parámetro)
* **Parámetros del Request:** Ninguno
* **Comportamiento Interno:**
  1. Invoca el método `getAllLatest()` en la clase [Document](file:///c:/xampp/htdocs/miseria/qualitydoc/models/Document.php).
  2. Ejecuta la consulta SQL:
     ```sql
     SELECT * FROM documents WHERE is_latest = TRUE ORDER BY title ASC;
     ```
  3. Carga la vista [list.php](file:///c:/xampp/htdocs/miseria/qualitydoc/views/list.php) pasando el arreglo `$documents`.

#### Ejemplo de URL:
```http
GET http://localhost:8080/index.php?action=index
```

---

### 2. Visualizar Documento e Historial (`view`)

Muestra el visor del documento solicitado, recupera su historial completo de versiones y registra de manera automática un evento en la bitácora de auditoría.

* **Método HTTP:** `GET`
* **Parámetro de Acción:** `action=view`
* **Parámetros del Request:**
  
  | Parámetro | Tipo | Ubicación | Obligatorio | Descripción |
  | :--- | :--- | :--- | :--- | :--- |
  | `id` | `UUID` | Query String | **Sí** | Identificador único del documento. |

* **Comportamiento Interno:**
  1. Valida la existencia del parámetro `id`. Si no se provee, redirige a `index.php`.
  2. Obtiene el registro del documento mediante `getById($id)` en el modelo [Document](file:///c:/xampp/htdocs/miseria/qualitydoc/models/Document.php).
  3. Si el documento existe:
     - **Registro de Auditoría:** Invoca `logView($id)`, insertando en la tabla `document_views_audit` los datos de acceso (por defecto con `user_id = 1`, `company_id = 1` y `area = 'Sistemas'`).
     - **Historial de Versiones:** Invoca `getHistory($document_code)` utilizando el código único del documento (`document_code`) para recuperar las versiones anteriores y actuales ordenadas de forma descendente.
     - **Carga de Vista:** Requiere la vista [view.php](file:///c:/xampp/htdocs/miseria/qualitydoc/views/view.php).
  4. Si el documento no existe, imprime en pantalla `"Documento no encontrado."`.

#### Ejemplo de URL:
```http
GET http://localhost:8080/index.php?action=view&id=d3b07384-d113-4ec2-a5d6-848e658e4521
```

---

### 3. Acuse de Lectura (`acknowledge`)

Registra el acuse explícito de lectura del documento por parte del usuario.

* **Método HTTP:** `POST`
* **Parámetro de Acción:** `action=acknowledge` (enviado en la URL)
* **Parámetros del Request (Cuerpo POST):**

  | Parámetro | Tipo | Ubicación | Obligatorio | Descripción |
  | :--- | :--- | :--- | :--- | :--- |
  | `document_id` | `UUID` | Body (x-www-form-urlencoded) | **Sí** | Identificador del documento leído. |

* **Comportamiento Interno:**
  1. Verifica que exista `$_POST['document_id']`.
  2. Invoca el método `markAsRead($document_id)` del modelo [Document](file:///c:/xampp/htdocs/miseria/qualitydoc/models/Document.php).
  3. Ejecuta la inserción en la base de datos PostgreSQL:
     ```sql
     INSERT INTO document_read_acknowledgments (document_id, user_id, company_id, area) 
     VALUES (:document_id, 1, 1, 'Sistemas') 
     ON CONFLICT (document_id, user_id) DO NOTHING;
     ```
     > [!NOTE]
     > La instrucción `ON CONFLICT DO NOTHING` previene errores de duplicado en caso de que el usuario envíe la confirmación de lectura en múltiples ocasiones para el mismo documento.
  4. **Redirección:** Redirige al usuario de vuelta a la interfaz del visor:
     ```php
     header("Location: index.php?action=view&id=" . $id);
     ```

#### Ejemplo de Petición HTTP:
```http
POST http://localhost:8080/index.php?action=acknowledge
Content-Type: application/x-www-form-urlencoded

document_id=d3b07384-d113-4ec2-a5d6-848e658e4521
```

---

### 4. Servir Archivo (`serve_file`)

Descarga y transmite el archivo digital de un documento desde la API de Login remota al cliente web.

* **Método HTTP:** `GET`
* **Parámetro de Acción:** `action=serve_file` (enviado en la URL)
* **Parámetros del Request:**

  | Parámetro | Tipo | Ubicación | Obligatorio | Descripción |
  | :--- | :--- | :--- | :--- | :--- |
  | `id` | `UUID` | Query String | **Sí** | Identificador único del documento. |

* **Comportamiento Interno:**
  1. Recupera el registro del documento por su `id`.
  2. Construye la URL remota del archivo combinando el valor de la variable de entorno `API_LOGIN_URI` (por defecto `http://host.docker.internal:5000`) con el path relativo `file_path` almacenado en la base de datos (por ejemplo, `/uploads/document.pdf`).
  3. Realiza una petición HTTP interna para descargar el archivo del servidor remoto de forma segura.
  4. Transmite los datos al cliente web estableciendo las cabeceras `Content-Type` y `Content-Length` correspondientes obtenidas del servidor remoto.

#### Ejemplo de URL:
```http
GET http://localhost:8080/index.php?action=serve_file&id=d3b07384-d113-4ec2-a5d6-848e658e4521
```

---

### 5. Sincronizar/Subir Documento (`upload`)

Registra o actualiza la información completa de un documento y su versión en la base de datos PostgreSQL.

* **Método HTTP:** `POST`
* **Parámetro de Acción:** `action=upload` (enviado en la URL)
* **Parámetros del Request (Cuerpo POST - JSON):**

  | Parámetro | Tipo | Ubicación | Obligatorio | Descripción |
  | :--- | :--- | :--- | :--- | :--- |
  | `Id` | `UUID` | Body (JSON) | **Sí** | Identificador único del documento en SQL Server. |
  | `DocumentCode` | `String` | Body (JSON) | No | Código único del documento. |
  | `Title` | `String` | Body (JSON) | **Sí** | Título del documento. |
  | `Description` | `String` | Body (JSON) | No | Descripción del documento. |
  | `FilePath` | `String` | Body (JSON) | No | Ruta relativa del archivo digital. |
  | `VersionNumber` | `Int` | Body (JSON) | **Sí** | Número de versión. |
  | `IsLatest` | `Boolean` | Body (JSON) | **Sí** | Si es la versión más reciente del documento. *Nota: El servidor siempre forzará este valor a `true` al procesar la subida.* |
  | `StatusName` | `String` | Body (JSON) | No | Nombre del estado actual del documento (ej. 'Aprobado'). |
  | `CompanyId` | `Int` | Body (JSON) | No | Identificador de la compañía. |
  | `CompanyName` | `String` | Body (JSON) | No | Nombre de la compañía dueña del documento. |
  | `AuthorId` | `Int` | Body (JSON) | No | Identificador del autor creador. |
  | `CreatedAt` | `String` | Body (JSON) | No | Fecha de creación del documento original. |

* **Comportamiento Interno:**
  1. Valida que el JSON recibido contenga los campos requeridos mínimos.
  2. Si el registro con la clave `Id` ya existe en la tabla `documents`, actualiza los campos correspondientes. De lo contrario, inserta un registro nuevo.
  3. **Forzado de Última Versión y Obsolescencia:** El servidor establece la columna `is_latest` del documento entrante en `TRUE` de manera incondicional. Acto seguido, busca el resto de documentos en la base de datos con el mismo `document_code` (e IDs distintos) y los actualiza estableciendo `is_latest = FALSE` y `status_name = 'Obsoleto'`.
  4. Retorna un objeto JSON con la confirmación de éxito.

#### Ejemplo de Petición HTTP:
```http
POST http://localhost:8080/index.php?action=upload
Content-Type: application/json

{
  "Id": "d3b07384-d113-4ec2-a5d6-848e658e4521",
  "DocumentCode": "DOC-001",
  "Title": "Manual de Operaciones y Procesos (V2)",
  "Description": "Segunda versión revisada con nuevos procedimientos.",
  "FilePath": "/uploads/manual_procesos_v2.pdf",
  "VersionNumber": 2,
  "IsLatest": true,
  "StatusName": "Aprobado",
  "CompanyId": 1,
  "CompanyName": "Compañía de Pruebas S.A.",
  "AuthorId": 101,
  "CreatedAt": "2026-06-03T10:00:00Z"
}
```

---

## 🗄️ Esquema de Base de Datos Relacionado

Para comprender la interacción de las API y el controlador, se adjunta el modelo de datos utilizado en PostgreSQL (definido en [db.sql](file:///c:/xampp/htdocs/miseria/qualitydoc/db.sql)):

```sql
-- 1. Tabla de Documentos
CREATE TABLE documents (
    id UUID PRIMARY KEY,
    document_code VARCHAR(50),
    title VARCHAR(200) NOT NULL,
    description TEXT,
    file_path VARCHAR(500),
    version_number INT DEFAULT 1,
    is_latest BOOLEAN DEFAULT TRUE,
    status_name VARCHAR(50),
    company_id INT,
    company_name VARCHAR(100),
    author_id INT,
    sqlserver_created_at TIMESTAMP,
    synced_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 2. Tabla de Auditoría (Registro de visualizaciones automáticas)
CREATE TABLE document_views_audit (
    id BIGSERIAL PRIMARY KEY,
    document_id UUID NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
    user_id INT NOT NULL,                  
    company_id INT,                        
    area VARCHAR(100),                     
    viewed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 3. Tabla de Acuses de Lectura (Confirmación explícita)
CREATE TABLE document_read_acknowledgments (
    id BIGSERIAL PRIMARY KEY,
    document_id UUID NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
    user_id INT NOT NULL,
    company_id INT,
    area VARCHAR(100),
    acknowledged_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_document_user_ack UNIQUE (document_id, user_id)
);
```

---

## 💻 Ejemplos Prácticos de Integración

### Formulario de Acuse de Lectura en HTML
Para integrar la funcionalidad de "Marcar como leído" en la vista, se suele renderizar un formulario POST que apunta directamente a la acción `acknowledge`:

```html
<form action="index.php?action=acknowledge" method="POST">
    <!-- ID del documento actual -->
    <input type="hidden" name="document_id" value="<?= $document['id']; ?>">
    
    <!-- Botón de Confirmación -->
    <button type="submit" class="btn btn-success">
        <i class="fas fa-check-circle"></i> He leído y comprendido este documento
    </button>
</form>
```

### Script de Prueba de la API usando cURL

#### Consultar listado principal:
```bash
curl -X GET "http://localhost:8080/index.php?action=index"
```

#### Enviar un acuse de lectura vía terminal:
```bash
curl -X POST "http://localhost:8080/index.php?action=acknowledge" \
     -H "Content-Type: application/x-www-form-urlencoded" \
     -d "document_id=d3b07384-d113-4ec2-a5d6-848e658e4521"
```
