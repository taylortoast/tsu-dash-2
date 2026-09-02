USE [TSU-Dashboard];
GO

/*
Replace AREA52\tsu.chief and display name with the initial TSU Chief account.
Run this after the user has visited the app once, or run as-is to create the row.
*/

DECLARE @WindowsUserName NVARCHAR(150) = N'taylor_home_pc\micha';
DECLARE @DisplayName NVARCHAR(150) = N'TSU Flight Chief';
DECLARE @SectionId INT = (SELECT SectionId FROM dbo.Sections WHERE SectionCode = N'TSU');

MERGE dbo.Users AS target
USING (SELECT @WindowsUserName AS WindowsUserName) AS source
ON target.WindowsUserName = source.WindowsUserName
WHEN MATCHED THEN
    UPDATE SET
        DisplayName = @DisplayName,
        AssignedSectionId = @SectionId,
        IsActive = 1,
        IsAdmin = 1,
        UpdatedUtc = SYSUTCDATETIME()
WHEN NOT MATCHED BY TARGET THEN
    INSERT (WindowsUserName, DisplayName, AssignedSectionId, IsActive, IsAdmin, LastLoginUtc)
    VALUES (@WindowsUserName, @DisplayName, @SectionId, 1, 1, SYSUTCDATETIME());
GO
