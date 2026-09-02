--Sections table//

SectionId INT IDENTITY PRIMARY KEY
SectionCode NVARCHAR(10) NOT NULL UNIQUE -- TSU, TSUI, TSUL, TSUS
SectionName NVARCHAR(100) NOT NULL

CREATE TABLE dbo.Sections (
    SectionId INT IDENTITY(1,1) PRIMARY KEY,
    SectionCode NVARCHAR(10) NOT NULL UNIQUE,
    SectionName NVARCHAR(100) NOT NULL,
    SortOrder INT NOT NULL DEFAULT 0
);

INSERT INTO dbo.Sections (SectionCode, SectionName, SortOrder)
VALUES
('TSU',  'TSU Flight', 1),
('TSUI', 'TSUI', 2),
('TSUL', 'TSUL', 3),
('TSUS', 'TSUS', 4);


--Users table//

UserId INT IDENTITY PRIMARY KEY
WindowsUserName NVARCHAR(150) NOT NULL UNIQUE
DisplayName NVARCHAR(150) NULL
AssignedSectionId INT NULL
IsActive BIT NOT NULL DEFAULT 0
IsAdmin BIT NOT NULL DEFAULT 0
FirstSeenUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
LastLoginUtc DATETIME2 NULL
CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
UpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()

CREATE TABLE dbo.Users (
    UserId INT IDENTITY(1,1) PRIMARY KEY,

    WindowsUserName NVARCHAR(150) NOT NULL UNIQUE,
    DisplayName NVARCHAR(150) NULL,

    AssignedSectionId INT NULL,
    IsActive BIT NOT NULL DEFAULT 0,
    IsAdmin BIT NOT NULL DEFAULT 0,

    FirstSeenUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    LastLoginUtc DATETIME2 NULL,
    CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Users_Sections
        FOREIGN KEY (AssignedSectionId)
        REFERENCES dbo.Sections(SectionId)
);


--Posts table//

PostId INT IDENTITY PRIMARY KEY
SectionId INT NOT NULL
Title NVARCHAR(150) NOT NULL
PointOfContact NVARCHAR(150) NOT NULL
Description NVARCHAR(MAX) NOT NULL
LatestUpdate NVARCHAR(MAX) NOT NULL
EstimatedCompletionDate DATE NOT NULL
IsActive BIT NOT NULL DEFAULT 1
CreatedByUserId INT NOT NULL
UpdatedByUserId INT NULL
CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
UpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()

CREATE TABLE dbo.Posts (
    PostId INT IDENTITY(1,1) PRIMARY KEY,

    SectionId INT NOT NULL,
    Title NVARCHAR(150) NOT NULL,
    PointOfContact NVARCHAR(150) NOT NULL,
    Description NVARCHAR(MAX) NOT NULL,
    LatestUpdate NVARCHAR(MAX) NOT NULL,
    EstimatedCompletionDate DATE NOT NULL,

    IsActive BIT NOT NULL DEFAULT 1,

    CreatedByUserId INT NOT NULL,
    UpdatedByUserId INT NULL,

    CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Posts_Sections
        FOREIGN KEY (SectionId)
        REFERENCES dbo.Sections(SectionId),

    CONSTRAINT FK_Posts_CreatedBy
        FOREIGN KEY (CreatedByUserId)
        REFERENCES dbo.Users(UserId),

    CONSTRAINT FK_Posts_UpdatedBy
        FOREIGN KEY (UpdatedByUserId)
        REFERENCES dbo.Users(UserId)
);


CREATE INDEX IX_Posts_PublicBoard
ON dbo.Posts (SectionId, IsActive, EstimatedCompletionDate);

CREATE INDEX IX_Users_WindowsUserName
ON dbo.Users (WindowsUserName);


--Public board query rule://

SELECT *
FROM dbo.Posts
WHERE IsActive = 1
  AND EstimatedCompletionDate >= CAST(GETDATE() AS DATE);