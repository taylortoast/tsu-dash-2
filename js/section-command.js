(async function () {
  "use strict";

  const state = { user: null, posts: [], sections: [], filters: {} };
  const grid = document.querySelector("[data-command-grid]");

  async function init() {
    state.user = await window.TSU.auth.requirePage("section-command");
    if (!state.user) return;
    window.TSU.ui.setReadOnly(state.user);
    window.TSU.ui.renderUserMeta(state.user, state.user.isGuest ? "Read-only guest view" : "All Section Posts");
    await loadPosts();
  }

  async function loadPosts() {
    try {
      const data = await window.TSU.api.get("api/posts/list.ashx?includeDisabled=1");
      state.sections = operationalSections(data.sections || []);
      state.sections.forEach((section) => {
        if (!state.filters[section.sectionCode]) state.filters[section.sectionCode] = "all";
      });
      const sectionCodes = state.sections.map((section) => section.sectionCode);
      state.posts = (data.posts || []).filter((post) => sectionCodes.includes(post.sectionCode));
      render();
    } catch (error) {
      grid.innerHTML = "";
      grid.appendChild(window.TSU.ui.el("div", "empty-state", error.message));
    }
  }

  function render() {
    grid.innerHTML = "";
    state.sections.forEach((section) => grid.appendChild(renderColumn(section)));
  }

  function operationalSections(sections) {
    return sections.filter((section) => section.sectionCode !== "TSU");
  }

  function renderColumn(sectionInfo) {
    const sectionCode = sectionInfo.sectionCode;
    const posts = state.posts.filter((post) => post.sectionCode === sectionCode);
    const visible = posts.filter((post) => {
      const filter = state.filters[sectionCode];
      if (filter === "active") return post.isActive;
      if (filter === "inactive") return !post.isActive;
      return true;
    });

    const column = window.TSU.ui.el("section", "section-command-column");
    const header = window.TSU.ui.el("div", "section-header");
    header.appendChild(window.TSU.ui.el("h2", "section-title", sectionCode));
    const meta = window.TSU.ui.el("div", "section-meta");
    meta.appendChild(window.TSU.ui.el("span", "section-count", `${posts.length} posts`));
    if (!state.user.isGuest) {
      meta.appendChild(sectionEnabledButton(sectionInfo));
      meta.appendChild(publicDisplayButton(sectionInfo));
    }
    header.appendChild(meta);
    column.appendChild(header);
    column.appendChild(window.TSU.ui.el("p", "column-note", state.user.isGuest ? "Live project information. Guest view is read-only." : "Control section availability, public display, and post status. Closing a section preserves its data."));

    const filters = window.TSU.ui.el("div", "filters");
    ["all", "active", "inactive"].forEach((filter) => {
      const button = window.TSU.ui.el("button", "filter", label(filter));
      button.type = "button";
      button.classList.toggle("active", state.filters[sectionCode] === filter);
      button.addEventListener("click", () => {
        state.filters[sectionCode] = filter;
        render();
      });
      filters.appendChild(button);
    });
    column.appendChild(filters);

    const list = window.TSU.ui.el("div", "list");
    if (!visible.length) {
      list.appendChild(window.TSU.ui.el("div", "empty-state", "No posts"));
    } else {
      visible.forEach((post) => list.appendChild(renderPost(post)));
    }
    column.appendChild(list);
    return column;
  }

  function publicDisplayButton(sectionInfo) {
    const isVisible = Boolean(sectionInfo.isPublicVisible);
    const button = window.TSU.ui.el("button", isVisible ? "display-toggle enabled" : "display-toggle", isVisible ? "Public On" : "Public Off");
    button.type = "button";
    button.title = isVisible ? "Remove this section column from the public dashboard" : "Add this section column to the public dashboard";
    button.addEventListener("click", () => setPublicDisplay(sectionInfo, !isVisible));
    return button;
  }

  function sectionEnabledButton(sectionInfo) {
    const isEnabled = Boolean(sectionInfo.isEnabled);
    const button = window.TSU.ui.el("button", isEnabled ? "display-toggle enabled" : "display-toggle", isEnabled ? "Section On" : "Section Off");
    button.type = "button";
    button.title = isEnabled ? "Close this section on user-facing pages" : "Reopen this section on user-facing pages";
    button.addEventListener("click", () => setEnabled(sectionInfo, !isEnabled));
    return button;
  }

  function renderPost(post) {
    const completion = window.TSU.dates.completionStatus(post.estimatedCompletionDate);
    const refresh = window.TSU.dates.refreshStatus(post.updatedUtc);
    const card = window.TSU.ui.el("article", "post-card");
    card.classList.toggle("inactive", !post.isActive);
    card.classList.toggle("due-soon", completion.key === "due-soon");

    const top = window.TSU.ui.el("div", "row-top");
    top.appendChild(window.TSU.ui.el("h3", "post-title", post.title));
    top.appendChild(window.TSU.ui.badge(post.isActive ? "Active" : "Inactive", post.isActive ? "active-badge" : "inactive-badge"));
    card.appendChild(top);

    const dates = window.TSU.ui.el("div", "date-grid");
    dates.appendChild(dateBox("Updated", window.TSU.dates.formatDate(post.updatedUtc)));
    dates.appendChild(dateBox("Complete", window.TSU.dates.formatDate(post.estimatedCompletionDate)));
    card.appendChild(dates);

    const warnings = window.TSU.ui.el("div", "warning-row");
    if (post.isActive && refresh.key === "needs-refresh") warnings.appendChild(window.TSU.ui.badge(refresh.label, "danger-badge"));
    if (completion.key === "due-soon" || completion.key === "expired") warnings.appendChild(window.TSU.ui.badge(completion.label, completion.className));
    card.appendChild(warnings);

    if (state.user.isGuest) return card;
    const button = window.TSU.ui.el("button", post.isActive ? "danger" : "primary", post.isActive ? "Deactivate Post" : "Reactivate Post");
    button.type = "button";
    if (!post.isActive && completion.key === "expired") {
      button.textContent = "Update Completion Date Required";
      button.className = "secondary";
      button.disabled = true;
    } else {
      button.addEventListener("click", () => setStatus(post, !post.isActive));
    }
    card.appendChild(button);
    return card;
  }

  function dateBox(labelText, value) {
    const box = window.TSU.ui.el("div", "date-box");
    box.appendChild(window.TSU.ui.el("span", "date-label", labelText));
    box.appendChild(document.createTextNode(value));
    return box;
  }

  async function setStatus(post, isActive) {
    try {
      await window.TSU.api.post("api/posts/set-status.ashx", { postId: post.postId, isActive });
      await loadPosts();
    } catch (error) {
      window.alert(error.message);
    }
  }

  async function setPublicDisplay(sectionInfo, isPublicVisible) {
    try {
      await window.TSU.api.post("api/sections/set-public-display.ashx", {
        sectionCode: sectionInfo.sectionCode,
        isPublicVisible
      });
      sectionInfo.isPublicVisible = isPublicVisible;
      render();
    } catch (error) {
      window.alert(error.message);
    }
  }

  async function setEnabled(sectionInfo, isEnabled) {
    try {
      await window.TSU.api.post("api/sections/set-enabled.ashx", {
        sectionCode: sectionInfo.sectionCode,
        isEnabled
      });
      sectionInfo.isEnabled = isEnabled;
      render();
    } catch (error) {
      window.alert(error.message);
    }
  }

  function label(filter) {
    if (filter === "all") return "All";
    if (filter === "active") return "Active";
    return "Inactive";
  }

  await init();
}());
