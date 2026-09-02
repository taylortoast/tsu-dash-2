USE [TSU-Dashboard];
GO

/*
Adds no tables. Brings dbo.Users in line with the application's authorization
rules by guaranteeing both access-flag columns exist AND have DEFAULT
constraints.

Why the defaults matter:
CurrentUser.Ensure auto-provisions a first-time CAC user with a two-column
insert:

    INSERT dbo.Users (WindowsUserName, LastLoginUtc) VALUES (...);

Any BIT NOT NULL column without a DEFAULT makes that insert fail with
SQL error 515 ("Cannot insert the value NULL"), which surfaces as a 500 on
api/auth/route.ashx and blocks every new user from ever being created.
IsTsuiAdmin was added to the live table without a default, so this script is
required before the routing rules can be tested.

Safe to run repeatedly.
*/

-- CanAccessAssignmentsBoard ---------------------------------------------------
IF COL_LENGTH(N'dbo.Users', N'CanAccessAssignmentsBoard') IS NULL
BEGIN
    ALTER TABLE dbo.Users
        ADD CanAccessAssignmentsBoard BIT NOT NULL
        CONSTRAINT DF_Users_CanAccessAssignmentsBoard DEFAULT (0);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Users')
      AND COL_NAME(parent_object_id, parent_column_id) = N'CanAccessAssignmentsBoard'
)
BEGIN
    ALTER TABLE dbo.Users
        ADD CONSTRAINT DF_Users_CanAccessAssignmentsBoard
        DEFAULT (0) FOR CanAccessAssignmentsBoard;
END
GO

-- IsTsuiAdmin -----------------------------------------------------------------
IF COL_LENGTH(N'dbo.Users', N'IsTsuiAdmin') IS NULL
BEGIN
    ALTER TABLE dbo.Users
        ADD IsTsuiAdmin BIT NOT NULL
        CONSTRAINT DF_Users_IsTsuiAdmin DEFAULT (0);
END
GO

-- The live table already has this column but with no default. This is the fix.
IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Users')
      AND COL_NAME(parent_object_id, parent_column_id) = N'IsTsuiAdmin'
)
BEGIN
    ALTER TABLE dbo.Users
        ADD CONSTRAINT DF_Users_IsTsuiAdmin
        DEFAULT (0) FOR IsTsuiAdmin;
END
GO

-- Verification ----------------------------------------------------------------
-- Both rows must come back with a non-NULL DefaultConstraint.
SELECT
    c.name AS ColumnName,
    dc.name AS DefaultConstraint,
    dc.definition AS DefaultValue
FROM sys.columns c
LEFT JOIN sys.default_constraints dc
    ON dc.parent_object_id = c.object_id
   AND dc.parent_column_id = c.column_id
WHERE c.object_id = OBJECT_ID(N'dbo.Users')
  AND c.name IN (N'CanAccessAssignmentsBoard', N'IsTsuiAdmin');
GO
