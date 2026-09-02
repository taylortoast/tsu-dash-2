USE [TSU-Dashboard];
GO

/*
Seeds realistic TSU Flight posts for the public-board-v2 ticker.
Safe to rerun: existing posts are matched by SectionId + Title.
*/

DECLARE @SectionId INT =
    (SELECT SectionId FROM dbo.Sections WHERE SectionCode = N'TSU');

DECLARE @UserId INT =
    (SELECT TOP (1) UserId
     FROM dbo.Users
     WHERE IsActive = 1
       AND IsAdmin = 1
     ORDER BY LastLoginUtc DESC, UserId);

IF @SectionId IS NULL
BEGIN
    THROW 50001, 'TSU section was not found. Run 002_seed_sections.sql first.', 1;
END;

IF @UserId IS NULL
BEGIN
    THROW 50002, 'No active admin user was found. Run 004_bootstrap_admin_template.sql first.', 1;
END;

MERGE dbo.Posts AS target
USING (VALUES
    (
        N'Weekly Flight Sync',
        N'TSU Flight Chief',
        N'TSU will hold the weekly flight sync to review section priorities, current training support issues, and upcoming suspense items across TSUI, TSUL, TSUS, and TSUR.',
        N'Agenda topics are being collected from section leads. Final inputs are due before the next scheduled sync.',
        DATEADD(DAY, 7, CAST(GETDATE() AS DATE)),
        1
    ),
    (
        N'Commander Update Prep',
        N'TSU Front Office',
        N'TSU is preparing the consolidated flight update for command review, including active project status, overdue refresh items, and upcoming completion dates.',
        N'Section inputs are under review. TSUI and TSUS updates have been received; TSUL and TSUR inputs are pending final validation.',
        DATEADD(DAY, 3, CAST(GETDATE() AS DATE)),
        1
    ),
    (
        N'Training Support Coverage',
        N'TSU Scheduler',
        N'TSU is coordinating instructor and support coverage for scheduled training events, classroom support requests, and near-term operational requirements.',
        N'Coverage gaps have been identified for two upcoming events. Section leads are reviewing available personnel options.',
        DATEADD(DAY, 10, CAST(GETDATE() AS DATE)),
        1
    ),
    (
        N'Quarterly Readiness Review',
        N'TSU Flight Chief',
        N'TSU is collecting readiness data and section-level project status for the quarterly review package.',
        N'Initial data call was sent to all sections. Consolidated readiness summary will be drafted after section responses are received.',
        DATEADD(DAY, 21, CAST(GETDATE() AS DATE)),
        1
    ),
    (
        N'Office Display Validation',
        N'TSUI / TSU',
        N'TSU is validating the public office display workflow to confirm current project information is visible, readable, and refreshed by the responsible sections.',
        N'Public board layout is being tested on the large display. Feedback is being used to tune spacing, ticker behavior, and card readability.',
        DATEADD(DAY, 5, CAST(GETDATE() AS DATE)),
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
WHERE s.SectionCode = N'TSU'
ORDER BY p.UpdatedUtc DESC, p.Title;
GO
