USE [TSU-Dashboard];
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Posts_PublicBoard' AND object_id = OBJECT_ID(N'dbo.Posts'))
BEGIN
    CREATE INDEX IX_Posts_PublicBoard
    ON dbo.Posts (SectionId, IsActive, EstimatedCompletionDate)
    INCLUDE (UpdatedUtc);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Posts_Section_Status' AND object_id = OBJECT_ID(N'dbo.Posts'))
BEGIN
    CREATE INDEX IX_Posts_Section_Status
    ON dbo.Posts (SectionId, IsActive, UpdatedUtc DESC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_WindowsUserName' AND object_id = OBJECT_ID(N'dbo.Users'))
BEGIN
    CREATE INDEX IX_Users_WindowsUserName
    ON dbo.Users (WindowsUserName);
END
GO
