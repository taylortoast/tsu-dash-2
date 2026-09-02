# Next Steps — Manual Setup Required

Code changes for the access-routing fix are complete. The items below are on systems I have no access to (SQL Server, IIS) and **must be done by hand** before the routing rules can work.

Ordered by dependency. Step 1 is a hard blocker.

---

## 1. SQL Server — run `database/010_add_user_access_flags.sql` (BLOCKER)

Run against `[TSU-Dashboard]`. **This adds no tables.** It only guarantees two existing columns have `DEFAULT` constraints.

### Why this is a blocker

`Users.IsTsuiAdmin` was added to the live table as `BIT NOT NULL` **with no `DEFAULT`**. The application auto-provisions a first-time CAC user with a two-column insert (`CurrentUser.Ensure`, `App_Code/DashboardCore.cs`):

```sql
INSERT dbo.Users (WindowsUserName, LastLoginUtc) VALUES (@WindowsUserName, SYSUTCDATETIME());
```

Against the current schema that fails with **SQL error 515** — "Cannot insert the value NULL into column 'IsTsuiAdmin'". Every brand-new user gets a 500 from `api/auth/route.ashx` and can never be created. Because `web.config` has `customErrors mode="Off"`, they see the raw SQL text on the routing page.

The script is idempotent. Confirm it prints two rows with a non-NULL `DefaultConstraint`:

```sql
SELECT c.name AS ColumnName, dc.name AS DefaultConstraint, dc.definition AS DefaultValue
FROM sys.columns c
LEFT JOIN sys.default_constraints dc
    ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE c.object_id = OBJECT_ID(N'dbo.Users')
  AND c.name IN (N'CanAccessAssignmentsBoard', N'IsTsuiAdmin');
```

If either `DefaultConstraint` comes back NULL, stop — nothing downstream will work.

### If you are building the dev database from scratch

`001_create_schema.sql` and `006_reset_users_and_posts.sql` have been updated to include both columns with defaults, so a fresh build in numeric order (`000` → `010`) already has them. Note this matters more than it looks: before this change, a database built from `001` was **missing `CanAccessAssignmentsBoard` entirely**, which made `reader["CanAccessAssignmentsBoard"]` throw `IndexOutOfRangeException` on *every* login.

Do **not** run `docs/sql/sql/*.sql`. Those are reverse-engineered dumps of the live table, kept for reference only. `database/` is the source of truth.

## 2. SQL Server — bootstrap the first admin

Until one user has `IsAdmin = 1`, nobody can reach `user-admin.html` to grant anyone else anything.

Edit `database/004_bootstrap_admin_template.sql` and replace the placeholder on line 9:

```sql
DECLARE @WindowsUserName NVARCHAR(150) = N'KEESW-TSU001\Admin';  -- <-- your DOMAIN\username
```

Use the **exact** string IIS reports. Easiest way to get it: browse to `api/auth/whoami.ashx` once and read `windowsUserName` from the JSON. It is matched against a `UNIQUE` constraint, so a mismatch silently creates a second inactive row instead of activating you.

That account needs `IsActive = 1`, `IsAdmin = 1`, and a section — the script sets all three. **A section is required even for admins**: `GetAccessStatus` returns `"unassigned"` without one, which lands even an admin on `access-pending.html`.

## 3. SQL Server — grant the IIS app pool identity database access

The connection strings use `Integrated Security=True` and `web.config` sets `<identity impersonate="false" />`, so SQL sees the **app pool identity**, not the browsing user.

Create a login for whatever the pool runs as (`IIS APPPOOL\<PoolName>`, or a domain service account) and grant it on `[TSU-Dashboard]`:

- `db_datareader`, `db_datawriter` — enough for all dashboard operations

If you also want the RUMP prefill dropdown to work, the same identity needs `SELECT` on `[RUMP].[dbo].[Projects]`. It is not required for access routing; without it the RUMP panel shows an error and everything else is unaffected. `RumpPrefill` conveniently reports the identity it is running as in its error text.

## 4. web.config — point the connection strings at your dev SQL Server

`web.config` currently ships with:

| Name | Current value | Action |
|---|---|---|
| `TSUDashboardDb` | `Data Source=localhost;Initial Catalog=TSU-Dashboard` | Set to your dev instance |
| `RumpDb` | `Data Source=MAHG-DB-3202v;Initial Catalog=RUMP` | Production host — repoint or accept RUMP failing |
| `TSUDataConnString` | `Server=localhost; Database=TSDB` | Unused by any code path; ignore |

## 5. IIS — authentication

The app has no login page; it trusts IIS entirely. `CurrentUser.GetWindowsUserName` reads `context.User.Identity.Name`, then falls back to the `LOGON_USER` and `REMOTE_USER` server variables, and throws a 403 with an explanatory message if all three are empty.

On the application:

- **Windows Authentication: Enabled**
- **Anonymous Authentication: Disabled**
- Application pool: **.NET CLR v4.0**, **Integrated** pipeline (`App_Code/*.cs` is compiled at runtime by ASP.NET; it will not build under a No Managed Code pool)
- Install the **Windows Authentication** role feature if the option is missing from the Authentication pane

Anonymous exceptions for the public board are already declared in `web.config` via `<location>` blocks, so the public dashboard keeps working with Anonymous off at the app level.

## 6. IIS — a note on the deployment path

`js/auth.js` previously prefixed every URL with `../`, which resolved *above* the application root. That only worked by accident when the app was served from a site root; in a virtual directory (e.g. `/TSU-Dashboard/`) the whoami request 404'd and the page guard rejected instead of redirecting, leaving the dashboard half-initialized with no visible error.

That is fixed — `auth.js` now derives its prefix from the page's own location — so **either layout works**. If you do use a virtual directory, confirm in DevTools → Network that `api/auth/whoami.ashx` returns 200 and not 404.

## 7. Before this leaves your dev box

Two settings are appropriate for development and should not follow you to production:

- `<customErrors mode="Off" />` and `<httpErrors errorMode="Detailed" />` — combined with the catch-all `Json.Error(context, 500, ex.Message)` in `JsonHandler`, these send **raw SQL exception text to the browser**. Useful for step 1; a disclosure problem later.
- `<httpCookies requireSSL="true" />` is already set, so if you test over plain HTTP be aware cookies will not be issued. Windows auth does not depend on them, so this should not block you.

---

## Verifying the routing rules

Once steps 1–5 are done, check the API **before** touching the UI. Browse directly to:

```
api/auth/route.ashx
```

The JSON must contain `isTsuiAdmin`, `allowedPages`, and `routeTarget`. If those three are absent, the App_Code changes did not compile — check the Event Log and fix that before testing anything else.

Then walk one test account through the matrix. After step 1 you can flip every flag from `user-admin.html`; no hand-written SQL is needed. Change flags, then reload `index.html`.

| IsActive | IsAdmin | Section | CanAccessAssignmentsBoard | IsTsuiAdmin | Expected landing page |
|---|---|---|---|---|---|
| 0 | – | – | – | – | `access-pending.html` — "Pending Activation" |
| 1 | 0 | *(none)* | – | – | `access-pending.html` — "Pending Section Assignment" |
| 1 | 1 | any | – | – | **`section-command.html`** |
| 1 | 0 | TSUI | 1 | 1 | `section-dashboard.html`, TSUI Project Assignments link **visible** |
| 1 | 0 | TSUI | 1 | 0 | `assignments-board/index.html`, Back to Dashboard **hidden** |
| 1 | 0 | TSUL/TSUS/TSUR | 0 | – | `section-dashboard.html`, assignments link **hidden** |

That last row is the case that was completely broken: those users previously fell through both branches of the router and were stranded on `access-pending.html` forever, while the page label cheerfully read "Active".

### Test the guards, not just the router

The router only controls where you *land*. Type these into the address bar directly:

- As the board-only TSUI user (`CanAccessAssignmentsBoard=1, IsTsuiAdmin=0`), open `section-dashboard.html` → must bounce to the board
- As any non-admin, open `user-admin.html` → must bounce to your own landing page
- As a user with no board access, open `assignments-board/index.html` → must bounce away

In each case the address bar must settle on **one** URL. Any flicker between two pages means a redirect loop — report it, because the design makes that structurally impossible (you are only ever redirected to a target drawn from your own `allowedPages`).

---

## What changed in code, for reference

The routing rules were duplicated in two places that had already drifted apart — `AuthRoute` in `App_Code/Handlers.cs` and a hand-patched copy in `js/access-pending.js`. They now live in exactly one function, `CurrentUser.GetAllowedPages` (`App_Code/DashboardCore.cs`), which returns the ordered list of pages a user may open. Everything else consumes it:

| File | Change |
|---|---|
| `App_Code/DashboardCore.cs` | Added `AppUser.IsTsuiAdmin`, the `GetAllowedPages` / `GetRouteTarget` rule set, and `isTsuiAdmin` / `allowedPages` / `routeTarget` on the user JSON |
| `App_Code/Handlers.cs` | `AuthRoute` now calls `GetRouteTarget` instead of its own `if/else` |
| `App_Code/UserRepository.cs` | Reads and writes both flags; the admin list reuses `CurrentUser.ToJson` so its route preview cannot drift from the real router |
| `js/auth.js` | Fixed the `../` path bug; replaced `requireActive`/`requireAdmin` with `requirePage(pageKey)` |
| `js/access-pending.js` | Duplicated rules deleted; obeys `routeTarget`. Status label no longer reports "Active" to a user it refuses to route |
| `js/section-dashboard.js` | Assignments link gated on board access rather than the string `sectionCode === "TSUI"` |
| `js/section-command.js`, `js/user-admin.js` | Use `requirePage` |
| `user-admin.html`, `js/user-admin.js` | Added "Assignments Board Access" and "TSUI Admin" toggles, list badges, and a truthful route preview |
| `assignments-board/index.html`, `app.js` | Page is now guarded; the back-link guard actually runs (it previously ran at parse time against a `null` user and an element id that did not exist) |
| `database/010_add_user_access_flags.sql` | New — see step 1 |
| `database/001_create_schema.sql`, `006_reset_users_and_posts.sql` | Both flag columns added to the fresh-build schema |

## Known issues I did not touch

Real, but outside the access-routing work — say the word on any of them:

1. **"Pull from RUMP" is permanently invisible.** `section-dashboard.html` declares the button `hidden`; `section-dashboard.js` attaches a click listener but nothing ever unhides it, so `pullFromRump` is unreachable. The RUMP *panel* is toggled separately, so the two controls use inconsistent mechanisms. `searchRump()` is likewise defined and never called.
2. **`api/posts/list.ashx` is guarded by `RequireActive`, not `RequireAdmin`** — yet it backs the admin-only Section Command page. Any active user can read every section's posts straight from the API. This is a server-side hole that the page guards above do not close.
3. **`js/public-board.js` is dead** — there is no `public-board.html`, only `public-board-v2.html`.
4. **`api/users/update-assignment.ashx` duplicates `update.ashx`** — both declare `Class="UsersUpdate"`.
