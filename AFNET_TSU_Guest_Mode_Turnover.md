# AFNET Turn-Over Document
## TSU Project Dashboard Guest Read-Only Mode

This release adds guest browsing from the index page. Guests can view live
project information without editing posts, assignments, section settings,
workers, or notes.

Guest-accessible internal pages display an orange viewport border, a centered
Guest Mode label, and an Exit Guest Mode button. The exit button clears the
guest cookie and returns the user to index.html.

public-board-v2.html does not receive the guest-mode frame and remains the
public display page.

## Files to Replace

Replace these tracked application files in the deployed application root:

```text
App_Code/DashboardCore.cs
App_Code/Handlers.cs
App_Code/PostRepository.cs
App_Code/ProjectBoardRepository.cs

access-pending.html
index.html
section-dashboard.html
section-command.html

assignments-board/app.js
assignments-board/styles.css

css/access-pending.css
css/base.css

js/access-pending.js
js/index.js
js/section-command.js
js/section-dashboard.js
js/ui.js
```

## IIS Configuration

Update the deployed environment-specific file:

```text
web.config
```

Merge the guest anonymous-access location entries from
AFNET_web.config_guest_access_entries.txt. Preserve AFNET connection strings
and existing IIS settings. Keep the top-level anonymous deny rule. Do not
allow anonymous access to write endpoints or to
api/auth/update-display-name.ashx.

## CSS Changes

### css/access-pending.css

Add the index access-choice layout:

```css
.access-choice-row {
  margin-top: 1.25rem;
  align-items: stretch;
}

.access-choice-row > .button-link,
.access-choice-row > button {
  flex: 1 1 14rem;
  min-width: 0;
  text-align: center;
}

.access-choice-row > .button-link {
  cursor: pointer;
}

.access-choice-row > .button-link:hover,
.access-choice-row > button:hover {
  filter: brightness(1.08);
}

.access-choice-row > .button-link:focus-visible,
.access-choice-row > button:focus-visible {
  outline: 3px solid var(--blue);
  outline-offset: 2px;
}

.access-note {
  margin-top: 1rem;
  margin-bottom: 0;
}

@media (max-width: 520px) {
  .access-choice-row > .button-link,
  .access-choice-row > button {
    flex-basis: 100%;
    width: 100%;
  }
}
```

### css/base.css

Add the disabled-field styling and guest frame used by root-level guest pages:

```css
.guest-mode input:disabled,
.guest-mode textarea:disabled,
.guest-mode select:disabled {
  opacity: 0.9;
  color: var(--muted);
  cursor: not-allowed;
}

body.guest-mode {
  position: relative;
}

body.guest-mode::before {
  content: '';
  position: fixed;
  z-index: 3000;
  inset: 0.5rem;
  border: 3px solid #f59e0b;
  border-radius: 10px;
  pointer-events: none;
}

.guest-mode-label,
.guest-mode-exit {
  position: fixed;
  z-index: 3001;
  top: 0.85rem;
}

.guest-mode-label {
  left: 50%;
  transform: translateX(-50%);
  padding: 0.2rem 0.75rem;
  border: 1px solid #f59e0b;
  border-radius: 999px;
  background: var(--bg);
  color: #f59e0b;
  font-size: 0.8rem;
  font-weight: 800;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  white-space: nowrap;
}

.guest-mode-exit {
  right: 1.25rem;
  padding: 0.45rem 0.7rem;
  border: 1px solid #f59e0b;
  border-radius: 7px;
  background: #f59e0b;
  color: #020617;
  font-size: 0.78rem;
  font-weight: 800;
  text-decoration: none;
  white-space: nowrap;
}

.guest-mode-exit:hover {
  background: #fbbf24;
}

.guest-mode-exit:focus-visible {
  outline: 3px solid var(--blue);
  outline-offset: 2px;
}

@media (max-width: 600px) {
  .guest-mode-label {
    top: 0.85rem;
  }

  .guest-mode-exit {
    top: 2.75rem;
  }
}
```

### assignments-board/styles.css

Add this guest frame block. The board uses #60a5fa for the guest exit focus
outline to match its existing palette:

```css
body.guest-mode {
  position: relative;
}

body.guest-mode::before {
  content: '';
  position: fixed;
  z-index: 3000;
  inset: 0.5rem;
  border: 3px solid #f59e0b;
  border-radius: 10px;
  pointer-events: none;
}

.guest-mode-label,
.guest-mode-exit {
  position: fixed;
  z-index: 3001;
  top: 0.85rem;
}

.guest-mode-label {
  left: 50%;
  transform: translateX(-50%);
  padding: 0.2rem 0.75rem;
  border: 1px solid #f59e0b;
  border-radius: 999px;
  background: #0f172a;
  color: #f59e0b;
  font-size: 0.8rem;
  font-weight: 800;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  white-space: nowrap;
}

.guest-mode-exit {
  right: 1.25rem;
  padding: 0.45rem 0.7rem;
  border: 1px solid #f59e0b;
  border-radius: 7px;
  background: #f59e0b;
  color: #020617;
  font-size: 0.78rem;
  font-weight: 800;
  text-decoration: none;
  white-space: nowrap;
}

.guest-mode-exit:hover {
  background: #fbbf24;
}

.guest-mode-exit:focus-visible {
  outline: 3px solid #60a5fa;
  outline-offset: 2px;
}

@media (max-width: 600px) {
  .guest-mode-label {
    top: 0.85rem;
  }

  .guest-mode-exit {
    top: 2.75rem;
  }
}
```

Also add:

```css
.guest-mode .assignee-card {
  cursor: default;
}

.guest-mode .card {
  cursor: pointer;
}

.guest-mode .assignment-chip {
  cursor: default;
}

.guest-mode .assignment-chip:hover {
  background: #fff;
  border-color: #cbd5e1;
  color: #0f172a;
}
```

## Frontend Behavior Changes

js/ui.js creates the guest border, centered label, and exit control when the
server returns isGuest. The exit control clears TSUGuest and navigates to the
root index page.

js/section-command.js and assignments-board/app.js opt into the shared guest
presentation. The operational dashboard already uses the shared read-only
helper.

## Database Changes

No database scripts or schema changes are required.

## Deployment Validation

1. Back up the current deployed files.
2. Replace the application files listed above.
3. Merge the anonymous guest location entries into web.config.
4. Open index.html and select View as Guest.
5. Verify the orange border, centered Guest Mode label, and Exit Guest Mode
   button appear on the operational dashboard.
6. Verify the exit button clears guest mode and returns to index.html.
7. Verify the same guest frame appears on Section Command and the Assignments
   Board.
8. Verify public-board-v2.html does not receive the guest frame.
9. Verify guest mutation controls remain unavailable.
10. Verify Request Access still starts CAC authentication and the existing
    display-name/access workflow.

ASP.NET precompilation and JavaScript syntax checks passed in the development
checkout. Live AFNET IIS, browser, and database acceptance testing remains
required before release.
