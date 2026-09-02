(async function () {
  "use strict";

  const state = {
    users: [],
    sections: [],
    selectedUserId: null,
    filter: "all",
    search: ""
  };

  const q = (selector) => document.querySelector(selector);

  async function init() {
    const user = await window.TSU.auth.requirePage("user-admin");
    if (!user) return;
    window.TSU.ui.renderUserMeta(user, "CAC-authenticated users");
    bindEvents();
    await loadUsers();
  }

  function bindEvents() {
    q("[data-search]").addEventListener("input", (event) => {
      state.search = event.target.value.toLowerCase();
      renderUsers();
    });

    document.querySelectorAll("[data-filter]").forEach((button) => {
      button.addEventListener("click", () => {
        state.filter = button.dataset.filter;
        document.querySelectorAll("[data-filter]").forEach((node) => node.classList.toggle("active", node === button));
        renderUsers();
      });
    });

    q("[data-save-user]").addEventListener("click", saveUser);
    q("[data-activate-user]").addEventListener("click", () => setStatus(true));
    q("[data-deactivate-user]").addEventListener("click", () => setStatus(false));
    q("[data-delete-user]").addEventListener("click", deleteUser);
  }

  async function loadUsers() {
    try {
      const data = await window.TSU.api.get("api/users/list.ashx");
      state.users = data.users || [];
      state.sections = data.sections || [];
      renderSectionOptions();
      renderFilterButtons();
      renderUsers();
      if (state.selectedUserId) {
        const selected = state.users.find((user) => user.userId === state.selectedUserId);
        selected ? selectUser(selected) : clearSelection();
      }
    } catch (error) {
      window.TSU.ui.setMessage(error.message, "error");
    }
  }

  function renderSectionOptions() {
    const select = q("[data-section-select]");
    select.innerHTML = "";
    const blank = document.createElement("option");
    blank.value = "";
    blank.textContent = "Not Assigned";
    select.appendChild(blank);
    state.sections.forEach((section) => {
      const option = document.createElement("option");
      option.value = section.sectionCode;
      option.textContent = section.sectionCode;
      select.appendChild(option);
    });
  }

  function renderFilterButtons() {
    const filters = q("[data-filters]");
    filters.querySelectorAll("[data-section-filter]").forEach((button) => button.remove());

    state.sections.forEach((section) => {
      const button = window.TSU.ui.el("button", "filter", section.sectionCode);
      button.type = "button";
      button.dataset.filter = section.sectionCode;
      button.dataset.sectionFilter = "true";
      button.classList.toggle("active", state.filter === section.sectionCode);
      button.addEventListener("click", () => {
        state.filter = button.dataset.filter;
        document.querySelectorAll("[data-filter]").forEach((node) => node.classList.toggle("active", node === button));
        renderUsers();
      });
      filters.appendChild(button);
    });
  }

  function renderUsers() {
    const list = q("[data-user-list]");
    list.innerHTML = "";
    const users = state.users.filter(matchesFilter);
    if (!users.length) {
      list.appendChild(window.TSU.ui.el("div", "empty-state", "No users match the current filters"));
      return;
    }

    users.forEach((user) => {
      const row = window.TSU.ui.el("article", "row-card");
      row.classList.toggle("selected", user.userId === state.selectedUserId);
      const top = window.TSU.ui.el("div", "row-top");
      top.appendChild(window.TSU.ui.el("h3", "name", user.displayName || user.windowsUserName));
      top.appendChild(window.TSU.ui.badge(user.isActive ? "Active" : "Inactive", user.isActive ? "active-badge" : "inactive-badge"));
      row.appendChild(top);
      row.appendChild(window.TSU.ui.el("p", "identity", user.windowsUserName));
      const badges = window.TSU.ui.el("div", "badge-row");
      badges.appendChild(window.TSU.ui.badge(user.sectionCode || "Unassigned", "section-badge"));
      if (user.isAdmin) badges.appendChild(window.TSU.ui.badge("Admin", "warn-badge"));
      if (user.canAccessAssignmentsBoard) badges.appendChild(window.TSU.ui.badge("Board", "section-badge"));
      if (user.isTsuiAdmin) badges.appendChild(window.TSU.ui.badge("TSUI Admin", "warn-badge"));
      row.appendChild(badges);
      row.addEventListener("click", () => selectUser(user));
      list.appendChild(row);
    });
  }

  function matchesFilter(user) {
    const text = `${user.displayName || ""} ${user.windowsUserName || ""} ${user.sectionCode || ""}`.toLowerCase();
    if (state.search && !text.includes(state.search)) return false;
    if (state.filter === "active") return user.isActive;
    if (state.filter === "inactive") return !user.isActive;
    if (state.sections.some((section) => section.sectionCode === state.filter)) return user.sectionCode === state.filter;
    return true;
  }

  function selectUser(user) {
    state.selectedUserId = user.userId;
    q("[data-detail-name]").textContent = user.displayName || user.windowsUserName;
    q("[data-detail-identity]").textContent = user.windowsUserName;
    q("[data-detail-section]").textContent = user.sectionCode || "Not assigned";
    q("[data-detail-status]").textContent = user.isActive ? "Active" : "Inactive";
    q("[data-section-select]").value = user.sectionCode || "";
    q("[data-status-select]").value = String(user.isActive);
    q("[data-display-name-input]").value = user.displayName || "";
    q("[data-admin-select]").value = String(user.isAdmin);
    q("[data-board-select]").value = String(!!user.canAccessAssignmentsBoard);
    q("[data-tsui-admin-select]").value = String(!!user.isTsuiAdmin);
    q("[data-route-preview]").textContent = describeRoute(user);
    renderUsers();
  }

  function clearSelection() {
    state.selectedUserId = null;
    q("[data-detail-name]").textContent = "No user selected";
    q("[data-detail-identity]").textContent = "No user selected";
    q("[data-detail-section]").textContent = "Not assigned";
    q("[data-display-name-input]").value = "";
    q("[data-detail-status]").textContent = "Not selected";
    q("[data-section-select]").value = "";
    q("[data-status-select]").value = "false";
    q("[data-admin-select]").value = "false";
    q("[data-board-select]").value = "false";
    q("[data-tsui-admin-select]").value = "false";
    q("[data-route-preview]").textContent = "Select a user to preview routing.";
  }

  // The server computes routeTarget from the one authorization rule set, so
  // this preview cannot drift from where the user actually lands.
  function describeRoute(user) {
    const target = user.routeTarget;
    if (!target || target === "access-pending.html") {
      if (!user.isActive) return "Inactive. This user stays on the access-pending page.";
      if (!user.sectionCode) return "No section assigned. This user stays on the access-pending page.";
      return "This user has no dashboard access and stays on the access-pending page.";
    }
    return `This user lands on ${target} after sign-in.`;
  }

  function selectedPayload() {
    if (!state.selectedUserId) {
      throw new Error("Select a user first.");
    }
    return {
      userId: state.selectedUserId,
      displayName: q("[data-display-name-input]").value.trim(),
      sectionCode: q("[data-section-select]").value,
      isActive: q("[data-status-select]").value === "true",
      isAdmin: q("[data-admin-select]").value === "true",
      canAccessAssignmentsBoard: q("[data-board-select]").value === "true",
      isTsuiAdmin: q("[data-tsui-admin-select]").value === "true"
    };
  }

  async function saveUser() {
    try {
      await window.TSU.api.post("api/users/update.ashx", selectedPayload());
      window.TSU.ui.setMessage("User profile saved.", "success");
      await loadUsers();
    } catch (error) {
      window.TSU.ui.setMessage(error.message, "error");
    }
  }

  async function setStatus(isActive) {
    if (!state.selectedUserId) {
      window.TSU.ui.setMessage("Select a user first.", "error");
      return;
    }
    try {
      await window.TSU.api.post("api/users/set-status.ashx", { userId: state.selectedUserId, isActive });
      window.TSU.ui.setMessage(isActive ? "User activated." : "User deactivated.", "success");
      await loadUsers();
    } catch (error) {
      window.TSU.ui.setMessage(error.message, "error");
    }
  }

  async function deleteUser() {
    if (!state.selectedUserId) {
      window.TSU.ui.setMessage("Select a user first.", "error");
      return;
    }

    if (!window.confirm("Delete this user record? Users with post history cannot be deleted.")) return;

    try {
      await window.TSU.api.post("api/users/delete.ashx", { userId: state.selectedUserId });
      window.TSU.ui.setMessage("User deleted.", "success");
      clearSelection();
      await loadUsers();
    } catch (error) {
      window.TSU.ui.setMessage(error.message, "error");
    }
  }

  await init();
}());
