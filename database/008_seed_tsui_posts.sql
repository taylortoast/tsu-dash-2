USE [TSU-Dashboard];
GO

/*
Seeds realistic TSUI posts for public board testing.
Safe to rerun: existing posts are matched by SectionId + Title.
*/

DECLARE @SectionId INT =
    (SELECT SectionId FROM dbo.Sections WHERE SectionCode = N'TSUI');

DECLARE @UserId INT =
    (SELECT TOP (1) UserId
     FROM dbo.Users
     WHERE IsActive = 1
     ORDER BY
        CASE WHEN AssignedSectionId = @SectionId THEN 0 ELSE 1 END,
        LastLoginUtc DESC,
        UserId);

IF @SectionId IS NULL
BEGIN
    THROW 50001, 'TSUI section was not found. Run 002_seed_sections.sql first.', 1;
END;

IF @UserId IS NULL
BEGIN
    THROW 50002, 'No active user was found. Activate at least one user before seeding posts.', 1;
END;

MERGE dbo.Posts AS target
USING (VALUES
    (
        N'Moodle Course Dashboard Cleanup',
        N'TSUI Development Team',
        N'TSUI is refining Moodle dashboard organization so instructors and students can find current courseware, references, and support links faster.',
        N'Initial layout cleanup is complete. Remaining work is focused on final link validation and section feedback.',
        DATEADD(DAY, 9, CAST(GETDATE() AS DATE)),
        1
    ),
    (
        N'SCORM Package Validation',
        N'TSUI LMS Team',
        N'TSUI is validating SCORM completion reporting and score behavior for upcoming courseware releases.',
        N'Test packages loaded successfully. Completion and score fields are being compared against expected LMS reporting behavior.',
        DATEADD(DAY, 2, CAST(GETDATE() AS DATE)),
        1
    ),
    (
        N'Instructor Resource Page Refresh',
        N'TSUI Content Support',
        N'TSUI is updating instructor resource pages with current guides, classroom links, and support references.',
        N'Outdated references have been removed. Current guides are being staged for review before publication.',
        DATEADD(DAY, 14, CAST(GETDATE() AS DATE)),
        1
    ),
    (
        N'Public Display Layout Support',
        N'TSUI Web Team',
        N'TSUI is supporting public dashboard layout adjustments for large-screen office display use.',
        N'Background image rotation and ticker behavior are in testing. Large-screen spacing feedback is being incorporated.',
        DATEADD(DAY, 5, CAST(GETDATE() AS DATE)),
        1
    ),
    (
        N'Courseware Issue Tracker Review',
        N'TSUI Support Desk',
        N'TSUI is reviewing open courseware issues to identify items that require instructor coordination or developer updates.',
        N'Priority items have been grouped by course. Follow-up actions are being assigned to content owners.',
        DATEADD(DAY, 11, CAST(GETDATE() AS DATE)),
        1
    ),
    (
        N'Digital Training Product Archive',
        N'TSUI Knowledge Management',
        N'TSUI is organizing archived digital training products to improve retrieval and reduce duplicated reference material.',
        N'Archive folders have been inventoried. Duplicate product sets are being flagged for owner review.',
        DATEADD(DAY, 18, CAST(GETDATE() AS DATE)),
        1
    )
) AS source
(
    Title,
    PointOfContact,
    Description,
    LatestUpdate,
    EstimatedCompletionDate,
    IsActive
)
ON target.SectionId = @SectionId
AND target.Title = source.Title
WHEN MATCHED THEN
    UPDATE SET
        PointOfContact = source.PointOfContact,
        Description = source.Description,
        LatestUpdate = source.LatestUpdate,
        EstimatedCompletionDate = source.EstimatedCompletionDate,
        IsActive = source.IsActive,
        UpdatedByUserId = @UserId,
        UpdatedUtc = SYSUTCDATETIME()
WHEN NOT MATCHED BY TARGET THEN
    INSERT
    (
        SectionId,
        Title,
        PointOfContact,
        Description,
        LatestUpdate,
        EstimatedCompletionDate,
        IsActive,
        CreatedByUserId,
        UpdatedByUserId
    )
    VALUES
    (
        @SectionId,
        source.Title,
        source.PointOfContact,
        source.Description,
        source.LatestUpdate,
        source.EstimatedCompletionDate,
        source.IsActive,
        @UserId,
        @UserId
    );
GO

SELECT
    p.PostId,
    s.SectionCode,
    p.Title,
    p.PointOfContact,
    p.EstimatedCompletionDate,
    p.IsActive,
    p.UpdatedUtc
FROM dbo.Posts p
INNER JOIN dbo.Sections s ON s.SectionId = p.SectionId
WHERE s.SectionCode = N'TSUI'
ORDER BY p.UpdatedUtc DESC, p.Title;
GO
