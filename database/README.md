# AFNet Database Scripts

Run scripts in numeric order against the AFNet development SQL Server `MAHG-WS-3402v`.

The dashboard database name for AFNet is `[TSU-Dashboard]`. The external RUMP source database remains `RUMP`.

`004_bootstrap_admin_template.sql` is required to grant the first TSU Chief account `IsActive = 1`, section `TSU`, and `IsAdmin = 1`.

`005_optional_seed_dev_data.sql` is optional sample data for local testing.

`006_reset_users_and_posts.sql` is a destructive development reset for the `Users` and `Posts` tables. It drops `Posts` first because `Posts` references `Users`.

`007_seed_tsu_posts.sql` inserts or updates realistic TSU Flight posts for the public-board-v2 ticker.

`008_seed_tsui_posts.sql` inserts or updates realistic TSUI posts for public-board column testing.

`009_add_tsur_and_public_section_display.sql` updates an existing database with the TSUR section and the `Sections.IsPublicVisible` public-dashboard column toggle.

`010_add_user_access_flags.sql` is **required** on any existing database. It adds no tables; it guarantees `Users.CanAccessAssignmentsBoard` and `Users.IsTsuiAdmin` exist **and carry `DEFAULT 0` constraints**. `IsTsuiAdmin` was added to the live table as `BIT NOT NULL` with no default, which makes the two-column auto-provisioning insert in `CurrentUser.Ensure` fail with SQL error 515 for every first-time user. Run this before testing access routing.

Production reference only:

- SQL Server: `MAHG-DB-3202v`
- IIS web server: `MAHG-WS-3302v`
