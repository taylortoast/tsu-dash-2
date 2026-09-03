(async function () {
  "use strict";

  const state = {
    user: null,
    posts: [],
    rumpProjects: [],
    selectedPostId: null,
    filter: "all",
    search: ""
  };

  const q = (selector) => document.querySelector(selector);
  const fields = {
    postId: q("[data-post-id]"),
    title: q("[data-title]"),
    pointOfContact: q("[data-poc]"),
    description: q("[data-description]"),
    latestUpdate: q("[data-latest-update]"),
    estimatedCompletionDate: q("[data-completion]"),
    isActive: q("[data-status]")
  };

  async function init() {
    state.user = await window.TSU.auth.requirePage("section-dashboard");
    if (!state.user) return;

    q("[data-page-title]").textContent = state.user.isGuest ? "Public Operational Dashboard" : `${state.user.sectionCode} Operational Dashboard`;
    q("[data-section-badge]").textContent = state.user.isGuest ? "Guest View" : `Section: ${state.user.sectionCode}`;
    if (state.user.isGuest) q("[data-editor-title]").textContent = "Post Details (Read Only)";
    window.TSU.ui.setReadOnly(state.user);
    
    const userSection = state.user.sectionCode || "";
    const showRumpFeature = userSection.includes('TSUL') || userSection.includes('TSUS');
    
    q("[data-rump-panel]").hidden = !showRumpFeature;
    if (showRumpFeature) {
      await loadRumpProjects();
    }

    q("[data-latest-update-field]").hidden = state.user.sectionCode === "TSU";
    q("[data-preview-latest-row]").hidden = state.user.sectionCode === "TSU";

    // Gate the assignments board link on actual board access, not on the
    // section string - a TSUI user without the flag would otherwise see a link
    // whose API returns 403.
    const allowedPages = state.user.allowedPages || [];
    q("[data-section2-tsui-only]").hidden = allowedPages.indexOf("assignments-board") === -1;

    bindEvents();
    clearForm();
    await loadPosts();
  }

  function bindEvents() {
    q("[data-post-form]").addEventListener("submit", savePost);
    q("[data-new-post]").addEventListener("click", () => {
      state.selectedPostId = null;
      clearForm();
      renderList();
    });
    q("[data-clear-form]").addEventListener("click", clearForm);
    q("[data-deactivate]").addEventListener("click", deactivateSelected);
    q("[data-renew-post]").addEventListener("click", renewSelected);
    q("[data-open-completion-picker]").addEventListener("click", openCompletionPicker);
    q("[data-rump-pull]").addEventListener("click", pullFromRump);
    q("[data-search]").addEventListener("input", (event) => {
      state.search = event.target.value.toLowerCase();
      renderList();
    });
    q("[data-rump-select]").addEventListener("change", prefillFromRump);

    document.querySelectorAll("[data-filter]").forEach((button) => {
      button.addEventListener("click", () => {
        state.filter = button.dataset.filter;
        document.querySelectorAll("[data-filter]").forEach((node) => node.classList.toggle("active", node === button));
        renderList();
      });
    });

    Object.values(fields).forEach((field) => {
      field.addEventListener("input", updatePreview);
    });
  }

  ///////////////////////////
  // START OF RUMP PREFILL
  ////////////////////////////

  // Helper function to format Microsoft's /Date(ticks)/ format
  function formatJsonDate(jsonDate) {
    if (!jsonDate) return "";
    try {
      // Extracts the numeric ticks from strings like "/Date(1672531200000)/"
      const ticks = parseInt(jsonDate.substr(6));
      const date = new Date(ticks);
      // Format to YYYY-MM-DD, which is the required format for <input type="date">
      const year = date.getUTCFullYear();
      const month = String(date.getUTCMonth() + 1).padStart(2, '0');
      const day = String(date.getUTCDate()).padStart(2, '0');
      return `${year}-${month}-${day}`;
    } catch (e) {
      console.error("Could not parse date:", jsonDate, e);
      return "";
    }
  }

  async function loadRumpProjects() {
    const handlerUrl = 'https://wwwmil.keesler.af.mil/RUMP/sqltest/handler.ashx?action=queryData&table=Projects';
    try {
      const response = await fetch(handlerUrl, {
        method: 'GET',
        credentials: 'include',
        headers: { 'Accept': 'application/json' }
      });
      if (!response.ok) throw new Error(`HTTP error: ${response.status}`);
      const data = await response.json();
      if (data.Error) throw new Error(data.Error);

      state.rumpProjects = data.Data || [];
      const select = q("[data-rump-select]");
      select.innerHTML = `<option value="">Select a RUMP Project</option>`;

      state.rumpProjects.forEach(project => {
        const optionText = `${project.WorkOrder}: ${project.ProjectName}`;
        const option = window.TSU.ui.el("option", "", optionText);
        option.value = project.ProjectID;
        select.appendChild(option);
      });
    } catch (error) {
      console.error("Failed to load RUMP projects:", error);
      window.TSU.ui.setMessage(`Could not load RUMP projects. ${error.message}`, "error");
    }
  }

  function prefillFromRump(event) {
    const selectedProjectId = event.target.value;
    const project = state.rumpProjects.find(p => p.ProjectID == selectedProjectId);

    if (project) {
      // UPDATED: All fields are now correctly mapped and populated
      fields.title.value = `${project.WorkOrder}: ${project.ProjectName}` || "";
      fields.pointOfContact.value = project.POC_FullName || "";
      fields.description.value = project.DescriptionOfTrainer || "";
      fields.latestUpdate.value = project.Report || "";
      fields.estimatedCompletionDate.value = formatJsonDate(project.RequiredDate);
      
      updatePreview();
    }
  }

  ///////////////////////////
  // END OF RUMP PREFILL
  ////////////////////////////

  // (The rest of your existing code remains unchanged)

  async function loadPosts() {
    try {
      const data = await window.TSU.api.get("api/posts/list.ashx");
      state.posts = data.posts || [];
      renderList();
    } catch (error) {
      window.TSU.ui.setMessage(error.message, "error");
    }
  }

  function renderList() {
    const list = q("[data-post-list]");
    list.innerHTML = "";
    const posts = state.posts.filter(matchesFilter);

    if (!posts.length) {
      list.appendChild(window.TSU.ui.el("div", "empty-state", "No posts match the current filters"));
      return;
    }

    posts.forEach((post) => {
      const row = window.TSU.ui.el("article", "row-card");
      row.classList.toggle("selected", post.postId === state.selectedPostId);
      const top = window.TSU.ui.el("div", "row-top");
      top.appendChild(window.TSU.ui.el("h3", "row-title", post.title));
      top.appendChild(window.TSU.ui.badge(post.isActive ? "Active" : "Inactive", post.isActive ? "active-badge" : "inactive-badge"));
      row.appendChild(top);
      row.appendChild(window.TSU.ui.el("p", "row-dates", `Updated ${window.TSU.dates.formatDate(post.updatedUtc)} - Est. ${window.TSU.dates.formatDate(post.estimatedCompletionDate)}`));

      const badges = window.TSU.ui.el("div", "badge-row");
      const refresh = window.TSU.dates.refreshStatus(post.updatedUtc);
      const completion = window.TSU.dates.completionStatus(post.estimatedCompletionDate);
      if (post.isActive) badges.appendChild(window.TSU.ui.badge(refresh.label, refresh.className));
      if (completion.key === "due-soon" || completion.key === "expired") badges.appendChild(window.TSU.ui.badge(completion.label, completion.className));
      row.appendChild(badges);
      row.addEventListener("click", () => selectPost(post));
      list.appendChild(row);
    });
  }

  function matchesFilter(post) {
    const text = `${post.title} ${post.pointOfContact} ${post.description} ${post.latestUpdate}`.toLowerCase();
    if (state.search && !text.includes(state.search)) return false;
    const refresh = window.TSU.dates.refreshStatus(post.updatedUtc);
    const completion = window.TSU.dates.completionStatus(post.estimatedCompletionDate);

    if (state.filter === "active") return post.isActive;
    if (state.filter === "inactive") return !post.isActive;
    if (state.filter === "needs-refresh") return refresh.key === "needs-refresh";
    if (state.filter === "due-soon") return completion.key === "due-soon";
    if (state.filter === "expired") return completion.key === "expired";
    return true;
  }

  function selectPost(post) {
    state.selectedPostId = post.postId;
    fields.postId.value = post.postId;
    fields.title.value = post.title;
    fields.pointOfContact.value = post.pointOfContact;
    fields.description.value = post.description;
    fields.latestUpdate.value = post.latestUpdate;
    fields.estimatedCompletionDate.value = post.estimatedCompletionDate;
    fields.isActive.value = String(post.isActive);
    q("[data-last-updated]").textContent = `Last Updated: ${window.TSU.dates.formatDate(post.updatedUtc)}`;
    updatePreview();
    renderList();
  }

  function readForm() {
    return {
      postId: Number(fields.postId.value || 0),
      title: fields.title.value.trim(),
      pointOfContact: fields.pointOfContact.value.trim(),
      description: fields.description.value.trim(),
      latestUpdate: state.user && state.user.sectionCode === "TSU" ? "" : fields.latestUpdate.value.trim(),
      estimatedCompletionDate: fields.estimatedCompletionDate.value,
      isActive: fields.isActive.value === "true"
    };
  }

  async function savePost(event) {
    event.preventDefault();
    const post = readForm();
    const errors = window.TSU.validation.validatePost(post, {
      requireLatestUpdate: !(state.user && state.user.sectionCode === "TSU")
    });
    if (errors.length) {
      window.TSU.ui.setMessage(errors.join(" "), "error");
      return;
    }

    try {
      if (post.postId) {
        await window.TSU.api.post("api/posts/update.ashx", post);
        window.TSU.ui.setMessage("Post updated.", "success");
      } else {
        const data = await window.TSU.api.post("api/posts/create.ashx", post);
        state.selectedPostId = data.postId;
        window.TSU.ui.setMessage("Post created.", "success");
      }
      await loadPosts();
      const selected = state.posts.find((item) => item.postId === state.selectedPostId);
      if (selected) selectPost(selected);
    } catch (error) {
      window.TSU.ui.setMessage(error.message, "error");
    }
  }

  async function deactivateSelected() {
    const postId = Number(fields.postId.value || 0);
    if (!postId) {
      window.TSU.ui.setMessage("Select a post before deactivating.", "error");
      return;
    }

    try {
      await window.TSU.api.post("api/posts/set-status.ashx", { postId, isActive: false });
      window.TSU.ui.setMessage("Post deactivated.", "success");
      await loadPosts();
      const selected = state.posts.find((item) => item.postId === postId);
      if (selected) selectPost(selected);
    } catch (error) {
      window.TSU.ui.setMessage(error.message, "error");
    }
  }

  async function renewSelected() {
    const postId = Number(fields.postId.value || 0);
    if (!postId) {
      window.TSU.ui.setMessage("Select a post before renewing.", "error");
      return;
    }

    try {
      await window.TSU.api.post("api/posts/renew.ashx", { postId });
      window.TSU.ui.setMessage("Post renewed.", "success");
      await loadPosts();
      const selected = state.posts.find((item) => item.postId === postId);
      if (selected) selectPost(selected);
    } catch (error) {
      window.TSU.ui.setMessage(error.message, "error");
    }
  }

  function openCompletionPicker() {
    fields.estimatedCompletionDate.focus();
    if (typeof fields.estimatedCompletionDate.showPicker === "function") {
      fields.estimatedCompletionDate.showPicker();
    }
  }

  async function searchRump() {
    try {
      const data = await window.TSU.api.get("api/rump/search.ashx");
      window.TSU.ui.setMessage(data.message || "No RUMP records are configured yet.", "success");
    } catch (error) {
      window.TSU.ui.setMessage(error.message, "error");
    }
  }

  async function pullFromRump() {
    try {
      const data = await window.TSU.api.post("api/rump/pull.ashx", {});
      const imported = data.importedCount || 0;
      window.TSU.ui.setMessage(imported ? `${imported} RUMP post draft${imported === 1 ? "" : "s"} created.` : (data.message || "No RUMP records were imported."), imported ? "success" : "error");
      await loadPosts();
    } catch (error) {
      window.TSU.ui.setMessage(error.message, "error");
    }
  }

  function clearForm() {
    fields.postId.value = "";
    fields.title.value = "";
    fields.pointOfContact.value = "";
    fields.description.value = "";
    fields.latestUpdate.value = "";
    fields.estimatedCompletionDate.value = "";
    fields.isActive.value = "true";
    q("[data-rump-select]").value = "";
    q("[data-last-updated]").textContent = "Last Updated: Not selected";
    updatePreview();
  }

  function updatePreview() {
    const post = readForm();
    const completion = window.TSU.dates.completionStatus(post.estimatedCompletionDate);
    q("[data-preview-title]").textContent = post.title || "Untitled Post";
    q("[data-preview-poc]").textContent = post.pointOfContact || "Name / Office";
    q("[data-preview-description]").textContent = post.description || "Description text will appear here.";
    q("[data-preview-latest]").textContent = post.latestUpdate || "Latest update text will appear here.";
    q("[data-preview-completion]").textContent = `Est. Complete: ${window.TSU.dates.formatDate(post.estimatedCompletionDate)}`;
    const selected = state.posts.find((item) => item.postId === state.selectedPostId);
    q("[data-preview-updated]").textContent = selected ? `Updated ${window.TSU.dates.formatDate(selected.updatedUtc)}` : "Updated Today";
    q("[data-preview-card]").classList.toggle("due-soon", completion.key === "due-soon");
    q("[data-preview-completion]").classList.toggle("due-soon-date", completion.key === "due-soon");
  }

  await init();
}());
