-- =========================================
-- CREAR BASE DE DATOS
-- =========================================
CREATE DATABASE QualityDocDB;
GO

USE QualityDocDB;
GO

-- =========================================
-- TABLA: ROLES
-- =========================================
CREATE TABLE Roles (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre VARCHAR(50) NOT NULL UNIQUE
);
GO

-- =========================================
-- TABLA: USUARIOS
-- =========================================
CREATE TABLE Usuarios (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre VARCHAR(100) NOT NULL,
    Correo VARCHAR(100) NOT NULL UNIQUE,
    PasswordHash VARCHAR(256) NOT NULL,
    RolId INT NOT NULL,
    Activo BIT DEFAULT 1,
    FechaCreacion DATETIME DEFAULT GETDATE(),

    FOREIGN KEY (RolId) REFERENCES Roles(Id)
);
GO

-- =========================================
-- TABLA: ESTADOS DE DOCUMENTO
-- =========================================
CREATE TABLE EstadosDocumento (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre VARCHAR(50) NOT NULL UNIQUE
);
GO

-- =========================================
-- TABLA: DOCUMENTOS
-- =========================================
CREATE TABLE Documentos (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Titulo VARCHAR(200) NOT NULL,
    Descripcion TEXT,
    RutaArchivo VARCHAR(500),
    AutorId INT NOT NULL,
    EstadoId INT NOT NULL,
    FechaCreacion DATETIME DEFAULT GETDATE(),

    FOREIGN KEY (AutorId) REFERENCES Usuarios(Id),
    FOREIGN KEY (EstadoId) REFERENCES EstadosDocumento(Id)
);
GO

-- =========================================
-- TABLA: HISTORIAL DE APROBACIONES
-- =========================================
CREATE TABLE HistorialAprobaciones (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    DocumentoId UNIQUEIDENTIFIER NOT NULL,
    UsuarioId INT NOT NULL,
    Accion VARCHAR(50) NOT NULL,
    Comentario TEXT,
    Fecha DATETIME DEFAULT GETDATE(),

    FOREIGN KEY (DocumentoId) REFERENCES Documentos(Id),
    FOREIGN KEY (UsuarioId) REFERENCES Usuarios(Id)
);
GO

-- =========================================
-- INSERTAR DATOS INICIALES
-- =========================================

-- Roles
INSERT INTO Roles (Nombre) VALUES
('Admin'),
('Aprobador'),
('Operario');
GO

-- Estados
INSERT INTO EstadosDocumento (Nombre) VALUES
('Borrador'),
('Revision'),
('Aprobado'),
('Obsoleto');
GO

-- Usuario de prueba (password: 123456 en SHA-256 ejemplo)
INSERT INTO Usuarios (Nombre, Correo, PasswordHash, RolId)
VALUES (
    'Administrador',
    'admin@qualitydoc.com',
    '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86a7f3fbb0d7f3a4e0f8f1c2',
    1
);
GO

UPDATE Usuarios
SET PasswordHash = '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92'
WHERE Correo = 'admin@qualitydoc.com';