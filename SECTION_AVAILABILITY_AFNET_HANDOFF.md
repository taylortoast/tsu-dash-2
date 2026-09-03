# Section Availability Controls - AFNET Pass-Off

## Developer status

Implemented in the development checkout. No database commands, compilation, browser testing, or AFNET testing were performed on the development computer.

AFNET deployment and validation must be completed manually.

## Files to move to AFNET

Copy these frontend files into the existing TSU Dashboard application root:

- `access-pending.html`
- `ref.html`
- `js/access-pending.js`
- `js/public-board-v2.js`
- `js/public-board.js`
- `js/section-command.js`
- `js/user-admin.js`

Database/deployment documentation:

- `database/011_add_section_enabled.sql` - run this on an existing AFNET database.
- `database/001_create_schema.sql` - reference for new database creation only; do not run it against an existing database.
- `SECTION_AVAILABILITY_AFNET_HANDOFF.md` - this pass-off document.

Copy these backend files into the existing application:

- `App_Code/DashboardCore.cs`
- `App_Code/Handlers.cs`
- `App_Code/PostRepository.cs`
- `App_Code/ProjectBoardRepository.cs`
- `App_Code/UserRepository.cs`
- `api/sections/set-enabled.ashx`

No CSS or `web.config` change is required for this feature.

## Database change

For an existing AFNET database, run this script before copying or activating application files:

```text
database/011_add_section_enabled.sql
```

Do not run `database/001_create_schema.sql` against an existing database. That script drops and recreates core tables.

Before execution, confirm the target database and server with the AFNET database administrator. The repository documentation identifies `MAHG-WS-3402v` as the AFNET development SQL Server and `MAHG-DB-3202v` as the production reference. Do not use the development computer's local SQL connection by assumption.

The migration is safe to rerun. It adds:

```text
dbo.Sections.IsEnabled BIT NOT NULL DEFAULT (1)
```

It does not delete or modify posts, users, assignments, or history. `IsPublicVisible` remains a separate public-board setting.

## Database verification

After running the migration, verify the four controllable sections:

```sql
USE [TSU-Dashboard];
GO

SELECT
    SectionId,
    SectionCode,
    SectionName,
    SortOrder,
    IsPublicVisible,
    IsEnabled
FROM dbo.Sections
WHERE SectionCode IN (N'TSUI', N'TSUL', N'TSUS', N'TSUR')
ORDER BY SortOrder;
GO
```

All four rows should initially have `IsEnabled = 1`. TSU Flight is not controlled by the Section Command switch and must remain available.

## IIS/application notes

1. Back up the current AFNET application files and database before deployment.
2. Run the migration and verification query above on the confirmed AFNET target.
3. Copy the listed backend and frontend files into the existing application root.
4. Confirm `api/sections/set-enabled.ashx` is under the same IIS application as the other API handlers.
5. Allow the application to compile `App_Code` normally, or recycle the application pool if required by the existing deployment procedure.
6. No new connection string, app setting, IIS authorization exception, or web.config entry is required.

## Manual AFNET validation checklist

Use an administrator account after deployment:

- Open Section Command and confirm TSUI, TSUL, TSUS, and TSUR each show `Section On` and a separate public-display control.
- Turn one section off.
- Refresh the public board and confirm that section and its public posts are absent.
- Refresh a normal dashboard or assignments board and confirm the disabled section is absent.
- Sign in as a non-admin user assigned to the disabled section and confirm the user is routed to access pending with `Assigned Section Unavailable`.
- Confirm the disabled user cannot use the section API directly.
- Confirm Section Command still shows the disabled section and its existing posts.
- Turn the section back on and confirm the assigned user can return normally.
- Toggle `Public On/Off` separately and confirm it remains independent of `Section On/Off`.
- Confirm TSU Flight and its public ticker remain available.
- Confirm posts, users, assignments, and history remain unchanged throughout.
- Repeat the availability test for TSUL, TSUS, and TSUR.

## Rollback

If the application files need to be rolled back:

1. Restore the backed-up application files.
2. Leave `dbo.Sections.IsEnabled` in place; older application code can ignore the additional column.
3. If necessary, restore availability with:

```sql
USE [TSU-Dashboard];
GO

UPDATE dbo.Sections
SET IsEnabled = 1
WHERE SectionCode IN (N'TSUI', N'TSUL', N'TSUS', N'TSUR');
GO
```

Do not drop the column and do not run a destructive reset script.

## Responsibility boundary

Developer-provided work:

- Application code and handler changes listed above.
- The idempotent database migration.
- This deployment and validation handoff.

AFNET operator/database administrator work:

- Confirming the actual AFNET SQL target.
- Backing up the deployment.
- Running the migration.
- Moving application files into the AFNET IIS application.
- Performing the manual validation checklist.
- Reporting any IIS, SQL, authentication, or browser errors found during testing.
