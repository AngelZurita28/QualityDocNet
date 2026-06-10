USE [master]
GO

-- 1. Crear la base de datos de forma estándar (Docker decidirá las rutas en Linux)
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'QualityDocDB')
BEGIN
    CREATE DATABASE [QualityDocDB];
END
GO

-- 2. Configurar el modo y compatibilidad básica
ALTER DATABASE [QualityDocDB] SET COMPATIBILITY_LEVEL = 160 -- Nivel compatible con contenedores modernos
GO
ALTER DATABASE [QualityDocDB] SET RECOVERY SIMPLE 
GO
ALTER DATABASE [QualityDocDB] SET MULTI_USER 
GO

-- 3. Entrar a la base de datos para crear la estructura
USE [QualityDocDB]
GO
/****** Objeto: Table [dbo].[ApprovalHistory] Fecha de script: 09/06/2026 10:37:34 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ApprovalHistory](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[DocumentId] [uniqueidentifier] NOT NULL,
	[UserId] [int] NOT NULL,
	[Action] [varchar](50) NOT NULL,
	[Comment] [text] NULL,
	[ActionDate] [datetime] NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Objeto: Table [dbo].[Companies] Fecha de script: 09/06/2026 10:37:35 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Companies](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [varchar](100) NOT NULL,
	[IsActive] [bit] NULL,
	[CreatedAt] [datetime] NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Objeto: Table [dbo].[Departments] Fecha de script: 09/06/2026 10:37:35 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Departments](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Objeto: Table [dbo].[Documents] Fecha de script: 09/06/2026 10:37:35 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Documents](
	[Id] [uniqueidentifier] NOT NULL,
	[ParentId] [uniqueidentifier] NULL,
	[VersionNumber] [int] NULL,
	[IsLatest] [bit] NULL,
	[Title] [varchar](200) NOT NULL,
	[Description] [text] NULL,
	[FilePath] [varchar](500) NULL,
	[AuthorId] [int] NOT NULL,
	[StatusId] [int] NOT NULL,
	[CompanyId] [int] NOT NULL,
	[CreatedAt] [datetime] NULL,
	[SyncPostgre] [bit] NULL,
	[SyncFirebase] [bit] NULL,
	[LastErrorLog] [text] NULL,
	[DocumentCode] [varchar](50) NULL,
	[DepartmentId] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Objeto: Table [dbo].[DocumentStatus] Fecha de script: 09/06/2026 10:37:35 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DocumentStatus](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [varchar](50) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Objeto: Table [dbo].[Roles] Fecha de script: 09/06/2026 10:37:35 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Roles](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [varchar](50) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Objeto: Table [dbo].[Users] Fecha de script: 09/06/2026 10:37:35 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Users](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[FullName] [varchar](100) NOT NULL,
	[Email] [varchar](100) NOT NULL,
	[PasswordHash] [varchar](256) NOT NULL,
	[RoleId] [int] NOT NULL,
	[CompanyId] [int] NULL,
	[IsActive] [bit] NULL,
	[CreatedAt] [datetime] NULL,
	[DepartmentId] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Email] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
ALTER TABLE [dbo].[ApprovalHistory] ADD  DEFAULT (getdate()) FOR [ActionDate]
GO
ALTER TABLE [dbo].[Companies] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Companies] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Documents] ADD  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[Documents] ADD  DEFAULT ((1)) FOR [VersionNumber]
GO
ALTER TABLE [dbo].[Documents] ADD  DEFAULT ((1)) FOR [IsLatest]
GO
ALTER TABLE [dbo].[Documents] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Documents] ADD  DEFAULT ((0)) FOR [SyncPostgre]
GO
ALTER TABLE [dbo].[Documents] ADD  DEFAULT ((0)) FOR [SyncFirebase]
GO
ALTER TABLE [dbo].[Users] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Users] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[ApprovalHistory]  WITH CHECK ADD FOREIGN KEY([DocumentId])
REFERENCES [dbo].[Documents] ([Id])
GO
ALTER TABLE [dbo].[ApprovalHistory]  WITH CHECK ADD FOREIGN KEY([UserId])
REFERENCES [dbo].[Users] ([Id])
GO
ALTER TABLE [dbo].[Documents]  WITH CHECK ADD FOREIGN KEY([AuthorId])
REFERENCES [dbo].[Users] ([Id])
GO
ALTER TABLE [dbo].[Documents]  WITH CHECK ADD FOREIGN KEY([CompanyId])
REFERENCES [dbo].[Companies] ([Id])
GO
ALTER TABLE [dbo].[Documents]  WITH CHECK ADD FOREIGN KEY([ParentId])
REFERENCES [dbo].[Documents] ([Id])
GO
ALTER TABLE [dbo].[Documents]  WITH CHECK ADD FOREIGN KEY([StatusId])
REFERENCES [dbo].[DocumentStatus] ([Id])
GO
ALTER TABLE [dbo].[Documents]  WITH CHECK ADD  CONSTRAINT [FK_Documents_Departments] FOREIGN KEY([DepartmentId])
REFERENCES [dbo].[Departments] ([Id])
GO
ALTER TABLE [dbo].[Documents] CHECK CONSTRAINT [FK_Documents_Departments]
GO
ALTER TABLE [dbo].[Users]  WITH CHECK ADD FOREIGN KEY([CompanyId])
REFERENCES [dbo].[Companies] ([Id])
GO
ALTER TABLE [dbo].[Users]  WITH CHECK ADD FOREIGN KEY([RoleId])
REFERENCES [dbo].[Roles] ([Id])
GO
ALTER TABLE [dbo].[Users]  WITH CHECK ADD  CONSTRAINT [FK_Users_Departments] FOREIGN KEY([DepartmentId])
REFERENCES [dbo].[Departments] ([Id])
GO
ALTER TABLE [dbo].[Users] CHECK CONSTRAINT [FK_Users_Departments]
GO

-- 4. Insertar datos por defecto (Solo Roles)
SET IDENTITY_INSERT [dbo].[Roles] ON;
INSERT INTO [dbo].[Roles] ([Id], [Name]) VALUES (1, 'Super Admin');
INSERT INTO [dbo].[Roles] ([Id], [Name]) VALUES (2, 'Admin');
INSERT INTO [dbo].[Roles] ([Id], [Name]) VALUES (3, 'Reviewer');
INSERT INTO [dbo].[Roles] ([Id], [Name]) VALUES (4, 'Redacter');
INSERT INTO [dbo].[Roles] ([Id], [Name]) VALUES (5, 'Operador');
SET IDENTITY_INSERT [dbo].[Roles] OFF;
GO

USE [master]
GO
ALTER DATABASE [QualityDocDB] SET  READ_WRITE 
GO
