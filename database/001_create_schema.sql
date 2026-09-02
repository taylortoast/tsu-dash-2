USE [TSU-Dashboard];
GO

IF OBJECT_ID(N'dbo.Posts', N'U') IS NOT NULL DROP TABLE dbo.Posts;
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL DROP TABLE dbo.Users;
IF OBJECT_ID(N'dbo.Sections', N'U') IS NOT NULL DROP TABLE dbo.Sections;
GO

CREATE TABLE dbo.Sections (
    SectionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Sections PRIMARY KEY,
    SectionCode NVARCHAR(10) NOT NULL CONSTRAINT UQ_Sections_SectionCode UNIQUE,
    SectionName NVARCHAR(100) NOT NULL,
    SortOrder INT NOT NULL CONSTRAINT DF_Sections_SortOrder DEFAULT 0,
    IsPublicVisible BIT NOT NULL CONSTRAINT DF_Sections_IsPublicVisible DEFAULT 1
);
GO

CREATE TABLE dbo.Users (
    UserId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    WindowsUserName NVARCHAR(150) NOT NULL CONSTRAINT UQ_Users_WindowsUserName UNIQUE,
    DisplayName NVARCHAR(150) NULL,
    AssignedSectionId INT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 0,
    IsAdmin BIT NOT NULL CONSTRAINT DF_Users_IsAdmin DEFAULT 0,
    CanAccessAssignmentsBoard BIT NOT NULL CONSTRAINT DF_Users_CanAccessAssignmentsBoard DEFAULT 0,
    IsTsuiAdmin BIT NOT NULL CONSTRAINT DF_Users_IsTsuiAdmin DEFAULT 0,
    FirstSeenUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Users_FirstSeenUtc DEFAULT SYSUTCDATETIME(),
    LastLoginUtc DATETIME2(0) NULL,
    CreatedUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Users_CreatedUtc DEFAULT SYSUTCDATETIME(),
    UpdatedUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Users_UpdatedUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Users_Sections FOREIGN KEY (AssignedSectionId) REFERENCES dbo.Sections(SectionId)
);
GO

CREATE TABLE dbo.Posts (
    PostId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Posts PRIMARY KEY,
    SectionId INT NOT NULL,
    Title NVARCHAR(150) NOT NULL,
    PointOfContact NVARCHAR(150) NOT NULL,
    Description NVARCHAR(MAX) NOT NULL,
    LatestUpdate NVARCHAR(MAX) NOT NULL,
    EstimatedCompletionDate DATE NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Posts_IsActive DEFAULT 1,
    CreatedByUserId INT NOT NULL,
    UpdatedByUserId INT NULL,
    CreatedUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Posts_CreatedUtc DEFAULT SYSUTCDATETIME(),
    UpdatedUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Posts_UpdatedUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Posts_Sections FOREIGN KEY (SectionId) REFERENCES dbo.Sections(SectionId),
    CONSTRAINT FK_Posts_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT FK_Posts_UpdatedBy FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.Users(UserId)
);
GO
