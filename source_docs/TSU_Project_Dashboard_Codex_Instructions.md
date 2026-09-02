# TSU Project Dashboard — Codex Project Instructions

## Project Purpose

Build a TSU Project Dashboard web application for managing and displaying TSU flight project, event, and announcement information.

The system has two primary sides:

1. **Authenticated operational dashboards** used by assigned section users to manage posts.
2. **Public display dashboard** shown on a large screen as a clean office announcement board.

The application will run in an IIS/AFNET-style environment using independent HTML, CSS, and JavaScript pages. Database storage will be MSSQL. JavaScript will communicate with backend `.NET` `.ashx` handler endpoints using `fetch()`.

---

## Technology Stack

### Frontend

- Independent `.html`, `.css`, and `.js` pages
- No frontend framework
- JavaScript `fetch()` calls for backend interaction
- Dark screen-friendly theme
- Large-display readable layouts

### Backend/API

- ASP.NET `.ashx` handler endpoints
- JSON request/response format
- IIS/CAC authentication
- MSSQL database

### Database

- Microsoft SQL Server
- Primary application database stores all TSU Project Dashboard data.
- Local application tables:
  - `Sections`
  - `Users`
  - `Posts`

#### External Prefill Source: RUMP

- `RUMP` is a separate MSSQL database.
- RUMP is an optional external prefill source for **TSUL** and **TSUS** post creation/editing.
- RUMP should not be treated as the live display source for the public dashboard.
- Imported RUMP values should initially populate form fields, then the reviewed/edited values should be saved into the local `Posts` table.
- The public board, CRUD dashboards, and TSU command page should read from the local TSU Project Dashboard database, not directly from RUMP.
- Likely RUMP prefill fields:
  - Post title
  - Description
  - Estimated completion date
- Exact RUMP tables and fields are **to be determined**.

---

## Prototype Files Created During Planning

These prototype files were created for visual and interaction design reference.

### 1. `main-dashboard_prototype.html`

**Purpose:** Public large-screen TSU Project Dashboard.

**Design Summary:**

- Header with title and live clock
- Four columns:
  - TSU
  - TSUI
  - TSUL
  - TSUS
- Footer with TSUI credit and refresh guidance
- Cards display active public posts
- Overflowing columns scroll vertically only when needed
- Dark screen-friendly office-style layout

**Card Fields:**

- Title
- Last updated pill
- Point of Contact
- Description
- Latest Update
- Estimated completion full-width bar

**Status Indicators:**

- Last Updated:
  - Fresh: green
  - Near stale: amber
  - Expired refresh: red
- Estimated Completion:
  - Normal: neutral slate bar/border
  - Due soon: pink border and pink completion bar

---

### 2. `crud_dashboard_three_column_prototype.html`

**Purpose:** Shared operational CRUD dashboard used by TSU, TSUI, TSUL, and TSUS users.

**Design Summary:**

Three-column layout:

1. **Left column:** compact post list and filters
2. **Center column:** create/edit post form
3. **Right column:** live public-card preview

**Left List Filters:**

- All
- Active
- Inactive
- Needs Refresh
- Due Soon
- Expired Completion

**Editor Fields:**

- Section display, read-only
- Title
- Point of Contact
- Description
- Latest Update
- Estimated Completion Date
- Status: Active / Inactive

**Actions:**

- Save Post
- Create New Post
- Clear Form
- Deactivate Post

**Important Rule:**

The section is visible but not editable by normal users. The assigned section comes from the authenticated user's app profile.

---

### 3. `tsu_section_command_compact_prototype.html`

**Purpose:** TSU Chief command page for reviewing and controlling posts from TSUI, TSUL, and TSUS.

**Design Summary:**

Three-column layout:

- TSUI
- TSUL
- TSUS

Each column lists all posts from that section, including active and inactive posts.

**Compact Card Fields:**

- Title
- Active/Inactive badge
- Last Updated date
- Estimated Completion date
- Warning labels
- Action button

**Actions:**

- Active posts: `Deactivate Post`
- Inactive posts with valid completion date: `Reactivate Post`
- Inactive posts with expired completion date: show `Update Completion Date Required`

**Important Rules:**

- TSU Chief can deactivate section posts.
- TSU Chief can reactivate inactive posts only when the estimated completion date is still valid.
- If estimated completion date is expired, reactivation should require the post owner or authorized editor to update the completion date first.

---

### 4. `user_account_admin_prototype.html`

**Purpose:** TSU Chief user account administration page.

**Design Summary:**

Two-column layout:

1. **Left column:** known CAC-authenticated users
2. **Right column:** selected user assignment and status controls

**Admin Capabilities:**

- View known users
- Activate/deactivate accounts
- Assign user to section:
  - TSU
  - TSUI
  - TSUL
  - TSUS
- Grant/revoke TSU admin access

**Important Rule:**

CAC authentication confirms identity. This page controls application authorization.

---

### 5. `login_landing_access_pending_prototype.html`

**Purpose:** Landing/access-pending page for new, inactive, or unassigned users.

**Design Summary:**

Displayed when a user authenticates successfully but does not yet have active application access.

**Flow:**

1. User visits main `index.html`
2. IIS/CAC authentication occurs
3. Application checks `Users` table
4. If user is unknown:
   - create pending user profile record
   - show access-pending page
5. If user is inactive or unassigned:
   - show access-pending page
6. If user is active and assigned:
   - redirect to assigned dashboard

**Important Wording:**

Store authenticated user identity/profile information, not CAC credentials or secrets.

---

## Core Business Rules

### User Identity and Access

- IIS/CAC handles authentication.
- The application stores known user profile records in the `Users` table.
- The unique user key should be the Windows username, such as `AREA52\michael.taylor`.
- First successful CAC-authenticated visit creates a pending user profile if one does not already exist.
- Newly known users are inactive by default.
- The TSU Chief must activate the account and assign a section.
- Unknown, inactive, or unassigned users see the access-pending page.
- Active users with an assigned section are redirected to the correct CRUD dashboard.

### Section Assignment

Allowed sections:

- TSU
- TSUI
- TSUL
- TSUS

Section assignment controls routing and ownership of posts.

### Admin Access

Use an explicit `IsAdmin` field.

Do not assume all TSU-assigned users are admins.

Admin page access requires:

```text
User.IsActive = true
AND User.IsAdmin = true
```

### Routing Rules

| User State            | Result                                              |
| --------------------- | --------------------------------------------------- |
| Unknown user          | Create pending user profile and show access-pending |
| Inactive user         | Show access-pending / denied                        |
| Active but no section | Show access-pending                                 |
| Active + TSUI         | Route to TSUI CRUD dashboard                        |
| Active + TSUL         | Route to TSUL CRUD dashboard                        |
| Active + TSUS         | Route to TSUS CRUD dashboard                        |
| Active + TSU          | Route to TSU CRUD dashboard                         |
| Active + IsAdmin      | Allow TSU admin pages                               |

### Post Lifecycle

Posts have a simple lifecycle:

```text
Active ↔ Inactive
```

There is no draft status in v1.

### Post Deletion

- Posts should not be hard-deleted.
- Posts should be deactivated instead.
- Users may be deleted by the TSU Chief if they are unwanted/pending records.

### Public Board Visibility

A post appears on the public board only when:

```sql
IsActive = 1
AND EstimatedCompletionDate >= CAST(GETDATE() AS DATE)
```

Estimated completion expiration does **not** automatically set `IsActive = 0`.

Instead:

- `IsActive` is user-controlled.
- `EstimatedCompletionDate` controls public-board eligibility.

### Reactivation Rule

If a post is inactive:

- If `EstimatedCompletionDate` is today or in the future:
  - allow reactivation
- If `EstimatedCompletionDate` is in the past:
  - block direct reactivation
  - require updated estimated completion date first

### Refresh Rule

Each section is responsible for refreshing active posts weekly.

Last updated status:

| State         | Rule         | Color |
| ------------- | ------------ | ----- |
| Fresh         | 0–5 days old | Green |
| Near stale    | 6 days old   | Amber |
| Needs refresh | 7+ days old  | Red   |

### Estimated Completion Status

| State           | Rule                 | Visual                                   |
| --------------- | -------------------- | ---------------------------------------- |
| Normal          | More than 1 day away | Neutral card border/bar                  |
| Due soon        | Tomorrow or today    | Pink card border and pink completion bar |
| Past completion | Date is past         | Do not display publicly                  |

### RUMP Import Rule

For TSUL and TSUS users, the CRUD dashboard may eventually include a RUMP import/prefill option. This import should populate editable form fields only. Once saved, the post becomes a local `Posts` table record and follows normal application rules for activation, refresh status, completion date visibility, and public-board display.

---

## SQL Database Design

### `Sections`

```sql
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
```

### `Users`

```sql
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
```

### `Posts`

```sql
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
```

### Indexes

```sql
CREATE INDEX IX_Posts_PublicBoard
ON dbo.Posts (SectionId, IsActive, EstimatedCompletionDate);

CREATE INDEX IX_Users_WindowsUserName
ON dbo.Users (WindowsUserName);
```

### Public Board Query

```sql
SELECT *
FROM dbo.Posts
WHERE IsActive = 1
  AND EstimatedCompletionDate >= CAST(GETDATE() AS DATE);
```

---

## Recommended Endpoint Structure

Suggested `.ashx` endpoint organization:

```text
/api/
  auth/
    whoami.ashx
    route.ashx

  posts/
    list.ashx
    get.ashx
    create.ashx
    update.ashx
    set-status.ashx

  public-board/
    active-posts.ashx

  users/
    list.ashx
    get.ashx
    update-assignment.ashx
    set-status.ashx
    delete.ashx
```

### Endpoint Purpose Notes

#### `auth/whoami.ashx`

Returns authenticated IIS/CAC identity and app user status.

Should create pending user record if unknown.

#### `auth/route.ashx`

Returns where the active user should be redirected.

#### `posts/list.ashx`

Returns posts for the authenticated user’s section.

Admin mode may allow filtering by section.

#### `posts/create.ashx`

Creates a new post for the authenticated user’s assigned section.

Normal users should not be allowed to create posts for other sections.

#### `posts/update.ashx`

Updates editable fields for an existing post.

Normal users should only update posts from their assigned section.

#### `posts/set-status.ashx`

Activates or deactivates a post.

Reactivation must check estimated completion date.

#### `public-board/active-posts.ashx`

Returns active public-board posts grouped by section.

#### `users/list.ashx`

Admin-only. Returns known users.

#### `users/update-assignment.ashx`

Admin-only. Updates assigned section and admin permission.

#### `users/set-status.ashx`

Admin-only. Activates/deactivates user account.

#### `users/delete.ashx`

Admin-only. Deletes unwanted user records. Intended mainly for pending/unwanted users.

---

## Page Implementation Plan

### Main Index / Access Routing

Possible files:

```text
index.html
css/index.css
js/index.js
```

Responsibilities:

- Call `auth/whoami.ashx` or `auth/route.ashx`
- If authorized, redirect to correct dashboard
- If pending/inactive/unassigned, show access-pending content

### Public Board

Possible files:

```text
public-board.html
css/public-board.css
js/public-board.js
```

Responsibilities:

- Load public active posts
- Group by section
- Render four columns
- Apply status colors
- Start scrolling only when column content overflows
- Show live clock

### Section CRUD Dashboard

Possible files:

```text
section-dashboard.html
css/section-dashboard.css
js/section-dashboard.js
```

Responsibilities:

- Load posts for assigned section
- Render compact post list
- Filter posts
- Create/edit/deactivate/reactivate posts
- Show live preview card
- Enforce section ownership

### TSU Section Command

Possible files:

```text
section-command.html
css/section-command.css
js/section-command.js
```

Responsibilities:

- Admin-only
- Load all TSUI/TSUL/TSUS posts
- Show compact cards grouped by section
- Allow deactivate/reactivate where valid
- Block reactivation when completion date is expired

### User Account Admin

Possible files:

```text
user-admin.html
css/user-admin.css
js/user-admin.js
```

Responsibilities:

- Admin-only
- List known users
- Filter by section/status
- Assign section
- Activate/deactivate users
- Grant/revoke admin access
- Delete unwanted user records

---

## Suggested Implementation Order

1. Create database tables and seed `Sections`
2. Build shared backend helpers:
   - database connection
   - JSON response helper
   - authenticated user lookup
   - authorization checks
3. Build `auth/whoami.ashx`
4. Build access routing page
5. Build user admin page
6. Build CRUD dashboard
7. Build public board
8. Build TSU section command page
9. Add validation, error handling, and final UI polish

---

## Notes for Development

- Keep frontend files separated in production:
  - `.html`
  - `.css`
  - `.js`
- The prototypes use embedded CSS/JS only for review convenience.
- Do not store CAC credentials or secrets.
- Store only authenticated identity/profile data.
- Prefer soft lifecycle controls over hard deletion for posts.
- Keep admin checks explicit.
- Keep v1 simple: no audit/history tables unless leadership later requires accountability reporting.
- Extra database: RUMP is an optional external prefill source for TSUL and TSUS post creation/editing. Imported RUMP values populate form fields initially, but saved posts are stored in the TSU Project Dashboard Posts table and managed locally afterward. The RUMP data will more than likely fill in post title, description, and estimated completion date. The Tables and Fields to use from the RUMP database are to be determined.
