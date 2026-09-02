USE [TSU-Dashboard];
GO

DECLARE @AdminUserId INT;
DECLARE @AdminSectionId INT = (SELECT SectionId FROM dbo.Sections WHERE SectionCode = N'TSU');

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE WindowsUserName = N'DEV\tsu.admin')
BEGIN
    INSERT dbo.Users (WindowsUserName, DisplayName, AssignedSectionId, IsActive, IsAdmin, LastLoginUtc)
    VALUES (N'DEV\tsu.admin', N'Development TSU Admin', @AdminSectionId, 1, 1, SYSUTCDATETIME());
END

SELECT @AdminUserId = UserId FROM dbo.Users WHERE WindowsUserName = N'DEV\tsu.admin';

IF NOT EXISTS (SELECT 1 FROM dbo.Posts)
BEGIN
    INSERT dbo.Posts
        (SectionId, Title, PointOfContact, Description, LatestUpdate, EstimatedCompletionDate, IsActive, CreatedByUserId, UpdatedByUserId)
    SELECT SectionId, N'Flight Training Calendar Review', N'Flight Chief / TSU',
        N'Reviewing upcoming training support requirements and recurring coordination events.',
        N'Initial coordination list sent to section leads.',
        DATEADD(DAY, 12, CAST(GETDATE() AS DATE)), 1, @AdminUserId, @AdminUserId
    FROM dbo.Sections WHERE SectionCode = N'TSU'
    UNION ALL
    SELECT SectionId, N'Moodle Dashboard Cleanup', N'TSUI Developer',
        N'Refining course dashboard layout and visibility for instructional products.',
        N'Prototype layout completed for review.',
        DATEADD(DAY, 10, CAST(GETDATE() AS DATE)), 1, @AdminUserId, @AdminUserId
    FROM dbo.Sections WHERE SectionCode = N'TSUI'
    UNION ALL
    SELECT SectionId, N'Lab Equipment Inventory', N'TSUL Logistics',
        N'Tracking equipment status and classroom support inventory.',
        N'Inventory review completed for priority rooms.',
        DATEADD(DAY, 20, CAST(GETDATE() AS DATE)), 1, @AdminUserId, @AdminUserId
    FROM dbo.Sections WHERE SectionCode = N'TSUL'
    UNION ALL
    SELECT SectionId, N'System Access Validation', N'TSUS Support',
        N'Validating user access for classroom support systems.',
        N'Final user list under review.',
        DATEADD(DAY, 1, CAST(GETDATE() AS DATE)), 1, @AdminUserId, @AdminUserId
    FROM dbo.Sections WHERE SectionCode = N'TSUS'
    UNION ALL
    SELECT SectionId, N'Requirements Intake Review', N'TSUR Requirements',
        N'Reviewing new training support requests for scope, priority, and section coordination.',
        N'Initial request queue has been organized for owner review.',
        DATEADD(DAY, 15, CAST(GETDATE() AS DATE)), 1, @AdminUserId, @AdminUserId
    FROM dbo.Sections WHERE SectionCode = N'TSUR';
END
GO
