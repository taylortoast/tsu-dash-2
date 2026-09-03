(async function () {
  "use strict";

  const $ = (selector) => document.querySelector(selector);
  const nameForm = $("[data-name-form]");
  const nameInput = $("#display-name");

  function showError(message) {
    const box = $("[data-message]");
    box.hidden = false;
    box.textContent = message;
    box.className = "message error";
  }

  async function load() {
    try {
      const data = await window.TSU.api.get("api/auth/whoami.ashx");
      const user = data.user;
      $("[data-display-name]").textContent = user.displayName || "Unknown";
      $("[data-windows-user]").textContent = user.windowsUserName || "Unknown";
      $("[data-account-status]").textContent = labelStatus(user);
      $("[data-section]").textContent = user.sectionCode || "Not Assigned";
      $("[data-access-pill]").textContent = labelStatus(user);
      if (nameForm) nameForm.hidden = Boolean(user.displayName);

      if (!user.displayName && nameForm) {
        nameForm.hidden = false;
        if (!nameForm.dataset.bound) {
          nameForm.dataset.bound = "1";
          nameForm.addEventListener("submit", async function (event) {
            event.preventDefault();
            const displayName = nameInput.value.trim();
            if (!displayName) return showError("Enter your display name.");
            try {
              await window.TSU.api.post("api/auth/update-display-name.ashx", { displayName });
              await load();
            } catch (error) {
              showError(error.message);
            }
          });
        }
        nameInput.focus();
        return;
      }

      // The server owns the routing rules (CurrentUser.GetAllowedPages); this
      // page only obeys the target it is handed.
      if (user && user.routeTarget && user.routeTarget !== "access-pending.html") {
        window.location.replace(user.routeTarget);
      }
    } catch (error) {
      window.TSU.ui.setMessage(error.message, "error");
    }
  }

  function labelStatus(user) {
    if (!user) return "Unknown";
    if (!user.isActive) return "Pending Activation";
    if (!user.sectionCode) return "Pending Section Assignment";
    if (user.accessStatus === "section-disabled") return "Assigned Section Unavailable";
    // Activated and assigned, yet the router still has nowhere to send them.
    // This should be unreachable, but the label must not claim otherwise.
    if (!user.routeTarget || user.routeTarget === "access-pending.html") {
      return "Active - Awaiting Access Grant";
    }
    return "Active";
  }

  $("[data-check-access]").addEventListener("click", load);
  await load();
}());
