const statuses = [
  "Proposed",
  "Assigned / In Progress",
  "Customer Review"
];

let projects = [];
let assignees = [];
let currentUser = null;
let sections = [];
let activeProjectId = null;

const board = document.getElementById("board");
const assigneeList = document.getElementById("assigneeList");
const searchInput = document.getElementById("searchInput");
const currentProjectUpdate = document.getElementById("currentProjectUpdate");
const addProjectBtn = document.getElementById("addProjectBtn");
const refreshBoardBtn = document.getElementById("refreshBoardBtn");

const detailPanel = document.getElementById("detailPanel");
const overlay = document.getElementById("overlay");
const closePanelBtn = document.getElementById("closePanelBtn");
const saveProjectBtn = document.getElementById("saveProjectBtn");

const detailTitle = document.getElementById("detailTitle");
const detailSection = document.getElementById("detailSection");
const detailCustomer = document.getElementById("detailCustomer");
const detailDescription = document.getElementById("detailDescription");
const detailEstimatedDate = document.getElementById("detailEstimatedDate");
const detailLeadAssignees = document.getElementById("detailLeadAssignees");
const detailHelperAssignees = document.getElementById("detailHelperAssignees");
const notesList = document.getElementById("notesList");
const newNote = document.getElementById("newNote");
const panelTitle = document.getElementById("panelTitle");

async function loadBoardData() {
  const response = await fetch("../api/board/projects.ashx", {
    credentials: "same-origin"
  });

  const payload = await response.json();

  if (!payload.ok) {
    throw new Error(
      payload.errors && payload.errors.length
        ? payload.errors.join(" ")
        : "Failed to load board data."
    );
  }

  const data = payload.data || {};
  currentUser = data.user || null;
  sections = data.sections || [];

  projects = (data.projects || []).map(mapApiProject);
  assignees = mapApiWorkers(data.workers || [], projects);

  window.TSU.ui.setReadOnly(currentUser);
  if (currentUser && currentUser.isGuest) {
    document.querySelector(".topbar-left p").textContent = "Live TSUI projects. Guest view is read-only.";
    document.querySelector(".assignee-panel-header .small").textContent = "Assignments are visible but cannot be changed in guest view.";
    newNote.disabled = true;
    saveProjectBtn.hidden = true;
  }

  renderBackLink();
  renderAssignees();
  renderBoard();
}

// Only offer the dashboard link to users the router would actually let in.
// A TSUI board worker without TSUI admin belongs on this page alone.
function renderBackLink() {
  const backBtn = document.getElementById("backToDashboardBtn");
  if (!backBtn) return;
  const allowedPages = (currentUser && currentUser.allowedPages) || [];
  backBtn.hidden = allowedPages.indexOf("section-dashboard") === -1;
}

function mapApiProject(item) {
  return {
    id: item.postId,
    section: item.sectionName || "",
    sectionCode: item.sectionCode || "",
    title: item.title || "",
    customer: item.pointOfContact || "",
    description: item.description || "",
    latestUpdate: item.latestUpdate || "",
    estimatedCompletionDate: item.estimatedCompletionDate || "",
    status: item.category || "Proposed",
    leadAssignments: Array.isArray(item.leadAssignments) ? item.leadAssignments.slice() : [],
    helperAssignments: Array.isArray(item.helperAssignments) ? item.helperAssignments.slice() : [],
    notes: Array.isArray(item.notes)
      ? item.notes.map(function (note) {
          return {
            projectNoteId: note.projectNoteId,
            noteText: note.noteText || "",
            createdByDisplayName: note.createdByDisplayName || "",
            createdUtc: note.createdUtc || ""
          };
        })
      : []
  };
}

function mapApiWorkers(workerList, projectList) {
  return workerList.map(function (worker) {
    return {
      id: worker.projectWorkerId,
      name: worker.displayName,
      assigned: projectList
        .filter(function (project) {
          return countsTowardActiveLoad(project.status) &&
            (project.leadAssignments || []).indexOf(worker.displayName) !== -1;
        })
        .map(function (project) {
          return project.id;
        }),
      sub_assigned: projectList
        .filter(function (project) {
          return countsTowardActiveLoad(project.status) &&
            (project.helperAssignments || []).indexOf(worker.displayName) !== -1;
        })
        .map(function (project) {
          return project.id;
        })
    };
  });
}

function getProjectAssignments(projectId) {
  const project = projects.find(function (p) {
    return p.id === projectId;
  });

  return {
    leads: project ? (project.leadAssignments || []) : [],
    helpers: project ? (project.helperAssignments || []) : []
  };
}

function getSearchTerm() {
  return searchInput ? searchInput.value.trim().toLowerCase() : "";
}

function getFilteredProjects() {
  const term = getSearchTerm();

  return projects.filter(function (project) {
    const { leads, helpers } = getProjectAssignments(project.id);

    return (
      !term ||
      project.title.toLowerCase().includes(term) ||
      project.customer.toLowerCase().includes(term) ||
      project.description.toLowerCase().includes(term) ||
      project.latestUpdate.toLowerCase().includes(term) ||
      leads.join(" ").toLowerCase().includes(term) ||
      helpers.join(" ").toLowerCase().includes(term)
    );
  });
}
function getSortedAssignees() {
  return [...assignees].sort(function (a, b) {
const aActive = (a.assigned.length * 2) + a.sub_assigned.length;
const bActive = (b.assigned.length * 2) + b.sub_assigned.length;

    const aIsFree = aActive === 0 ? 0 : 1;
    const bIsFree = bActive === 0 ? 0 : 1;

    if (aIsFree !== bIsFree) {
      return aIsFree - bIsFree;
    }

    if (aActive !== bActive) {
      return aActive - bActive;
    }

    return a.name.localeCompare(b.name);
  });
}

function renderAssignees() {
  const sortedAssignees = getSortedAssignees();

  assigneeList.innerHTML = sortedAssignees.map(function (worker) {
    const activeLoad = worker.assigned.length + worker.sub_assigned.length;
    const availabilityClass =
      activeLoad === 0 ? "available" :
      activeLoad === 1 ? "partial" :
      "busy";

    return `
      <div class="assignee-card" draggable="${!(currentUser && currentUser.isGuest)}" data-worker="${escapeHtml(worker.name)}">
        <div class="assignee-row">
          <div class="assignee-name-wrap">
            <span class="availability-dot ${availabilityClass}" aria-hidden="true"></span>
            <div class="assignee-name">${escapeHtml(worker.name)}</div>
          </div>
          <div class="assignee-metrics">
            <span><strong>Lead:</strong> ${worker.assigned.length}</span>
            <span><strong>Helper:</strong> ${worker.sub_assigned.length}</span>
          </div>
        </div>
      </div>
    `;
  }).join("");

  if (currentUser && currentUser.isGuest) return;
  document.querySelectorAll(".assignee-card").forEach(function (card) {
    card.addEventListener("dragstart", function (e) {
      e.dataTransfer.setData("text/worker", e.currentTarget.dataset.worker);
      document.body.classList.add("dragging-worker");
    });

    card.addEventListener("dragend", function () {
      document.body.classList.remove("dragging-worker");
      clearWorkerDragStates();
    });
  });
}

function clearWorkerDragStates() {
  document.querySelectorAll(".card.worker-drag-over").forEach(function (card) {
    card.classList.remove("worker-drag-over");
  });

  document.querySelectorAll(".assignment-zone.drag-over").forEach(function (zone) {
    zone.classList.remove("drag-over");
  });
}

function renderBoard() {
  const filtered = getFilteredProjects();

  board.innerHTML = statuses.map(function (status) {
    const items = filtered.filter(function (project) {
      return project.status === status;
    });

    return `
      <section class="column">
        <div class="column-header">
          <div class="column-title">${status}</div>
          <div class="count">${items.length}</div>
        </div>
        <div class="column-body" data-status="${status}">
          ${items.map(renderCard).join("")}
        </div>
      </section>
    `;
  }).join("");

  attachCardEvents();
  attachColumnDropEvents();
  attachAssignmentZoneEvents();
}

function renderCard(project) {
  const latestWorkNote =
    project.notes && project.notes.length
      ? project.notes[project.notes.length - 1].noteText
      : "";

  const leads = project.leadAssignments || [];
  const helpers = project.helperAssignments || [];

  return `
    <article class="card" data-id="${project.id}">
      <div class="card-header">
        <h3>${escapeHtml(project.title)}</h3>
        <div class="card-customer">${escapeHtml(project.customer)}</div>
      </div>

      <div class="card-section project-update">
        <div class="card-label">Current Project Update</div>
        <div class="card-text clamped-text">
          ${escapeHtml(project.latestUpdate || "No current update available.")}
        </div>
      </div>

      <div class="card-section work-note">
        <div class="card-label">Latest Work Note</div>
        <div class="card-text clamped-text">
          ${escapeHtml(latestWorkNote || "No work notes yet.")}
        </div>
      </div>

      <div class="card-assignments">
        <div class="assignment-line">
          <span class="assignment-key">Lead</span>
          <span class="assignment-value">${escapeHtml(leads.length ? leads.join(", ") : "None assigned")}</span>
        </div>
        <div class="assignment-line">
          <span class="assignment-key">Helpers</span>
          <span class="assignment-value">${escapeHtml(helpers.length ? helpers.join(", ") : "None assigned")}</span>
        </div>
      </div>

      <div class="assignment-dropzones"${currentUser && currentUser.isGuest ? " hidden" : ""}>
        <div class="assignment-zone lead-zone" data-role="lead" data-id="${project.id}">
          Drop as Lead
        </div>
        <div class="assignment-zone helper-zone" data-role="helper" data-id="${project.id}">
          Drop as Helper
        </div>
      </div>

      <div class="card-footer">
        <span class="eta-pill">
  <span class="eta-label">ETA:</span>
  <span class="eta-value">${escapeHtml(formatDate(project.estimatedCompletionDate))}</span>
</span>
      </div>
    </article>
  `;
}

function attachCardEvents() {
  document.querySelectorAll(".card").forEach(function (card) {
    card.addEventListener("click", function (e) {
      if (e.target.closest(".assignment-zone")) return;
      const id = Number(e.currentTarget.dataset.id);
      openDetailPanel(id);
    });

    if (currentUser && currentUser.isGuest) return;
    card.addEventListener("dragover", function (e) {
      if (e.dataTransfer.types.includes("text/worker")) {
        e.preventDefault();
        card.classList.add("worker-drag-over");
      }
    });

    card.addEventListener("dragleave", function (e) {
      if (!card.contains(e.relatedTarget)) {
        card.classList.remove("worker-drag-over");
      }
    });

    card.addEventListener("drop", function (e) {
      const workerName = e.dataTransfer.getData("text/worker");
      if (workerName) {
        e.preventDefault();
        card.classList.add("worker-drag-over");
      }
    });
  });
}

function attachColumnDropEvents() {
  if (currentUser && currentUser.isGuest) return;
  document.querySelectorAll(".column-body").forEach(function (column) {
    column.addEventListener("dragover", function (e) {
      e.preventDefault();
      column.classList.add("drag-over");
    });

    column.addEventListener("dragleave", function () {
      column.classList.remove("drag-over");
    });

    column.addEventListener("drop", async function (e) {
      e.preventDefault();
      column.classList.remove("drag-over");

      const projectIdText = e.dataTransfer.getData("text/project");
      if (!projectIdText) return;

      const projectId = Number(projectIdText);
      const newStatus = column.dataset.status;
      const project = projects.find(function (p) {
        return p.id === projectId;
      });

      if (!project || project.status === newStatus) return;

      try {
        const response = await fetch("../api/board/set-category.ashx", {
          method: "POST",
          credentials: "same-origin",
          headers: {
            "Content-Type": "application/json"
          },
          body: JSON.stringify({
            postId: projectId,
            category: newStatus
          })
        });

        const payload = await response.json();

        if (!payload.ok) {
          throw new Error(
            payload.errors && payload.errors.length
              ? payload.errors.join(" ")
              : "Failed to save project category."
          );
        }

        await loadBoardData();

        if (activeProjectId === projectId) {
          openDetailPanel(projectId);
        }
      } catch (error) {
        console.error("Failed to persist category change:", error);
        alert(error.message || "Failed to save project category.");
      }
    });
  });

  document.querySelectorAll(".card").forEach(function (card) {
    card.setAttribute("draggable", "true");
    card.addEventListener("dragstart", function (e) {
      e.dataTransfer.setData("text/project", e.currentTarget.dataset.id);
    });
  });
}

function renderAssignmentChips(names, role, projectId) {
  if (!names.length) {
    return `<span class="assignment-chip empty">None assigned</span>`;
  }

  if (currentUser && currentUser.isGuest) {
    return `<div class="assignment-chip-list">${names.map(function (name) {
      return `<span class="assignment-chip">${escapeHtml(name)}</span>`;
    }).join("")}</div>`;
  }

  return `
    <div class="assignment-chip-list">
      ${names.map(function (name) {
        return `
          <button
            type="button"
            class="assignment-chip"
            data-worker="${escapeHtml(name)}"
            data-role="${escapeHtml(role)}"
            data-project-id="${projectId}"
            title="Click to unassign"
          >
            ${escapeHtml(name)}
          </button>
        `;
      }).join("")}
    </div>
  `;
}

async function unassignWorkerFromProject(workerName, projectId, role) {
  const assignmentRole = role === "lead" ? "Lead" : "Helper";

  try {
    const response = await fetch("../api/board/unassign-worker.ashx", {
      method: "POST",
      credentials: "same-origin",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        postId: projectId,
        workerName: workerName,
        assignmentRole: assignmentRole
      })
    });

    const payload = await response.json();

    if (!payload.ok) {
      throw new Error(
        payload.errors && payload.errors.length
          ? payload.errors.join(" ")
          : "Failed to unassign worker."
      );
    }

    await loadBoardData();

    if (activeProjectId === projectId) {
      openDetailPanel(projectId);
    }
  } catch (error) {
    console.error("unassignWorkerFromProject failed:", error);
    alert(error.message || "Failed to unassign worker.");
  }
}

function attachAssignmentZoneEvents() {
  if (currentUser && currentUser.isGuest) return;
  document.querySelectorAll(".assignment-zone").forEach(function (zone) {
    zone.addEventListener("dragover", function (e) {
      if (e.dataTransfer.types.includes("text/worker")) {
        e.preventDefault();
        e.stopPropagation();
        zone.classList.add("drag-over");
        const card = zone.closest(".card");
        if (card) card.classList.add("worker-drag-over");
      }
    });

    zone.addEventListener("dragleave", function () {
      zone.classList.remove("drag-over");
    });

    zone.addEventListener("drop", function (e) {
      const workerName = e.dataTransfer.getData("text/worker");
      if (!workerName) return;

      e.preventDefault();
      e.stopPropagation();
      zone.classList.remove("drag-over");

      const card = zone.closest(".card");
      if (card) card.classList.remove("worker-drag-over");

      const projectId = Number(zone.dataset.id);
      const role = zone.dataset.role;

      assignWorkerToProject(workerName, projectId, role);
      clearWorkerDragStates();
    });
  });
}

async function assignWorkerToProject(workerName, projectId, role) {
  const assignmentRole = role === "lead" ? "Lead" : "Helper";

  try {
    const response = await fetch("../api/board/assign-worker.ashx", {
      method: "POST",
      credentials: "same-origin",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        postId: projectId,
        workerName: workerName,
        assignmentRole: assignmentRole
      })
    });

    const payload = await response.json();

    if (!payload.ok) {
      throw new Error(
        payload.errors && payload.errors.length
          ? payload.errors.join(" ")
          : "Failed to assign worker."
      );
    }

    await loadBoardData();

    if (activeProjectId === projectId) {
      openDetailPanel(projectId);
    }
  } catch (error) {
    console.error("assignWorkerToProject failed:", error);
    alert(error.message || "Failed to assign worker.");
  }
}

function openDetailPanel(projectId) {
  const project = projects.find(function (p) {
    return p.id === projectId;
  });
  if (!project) return;

  const { leads, helpers } = getProjectAssignments(projectId);

  activeProjectId = projectId;
  panelTitle.textContent = "Project Review";
  detailTitle.textContent = project.title;
  detailSection.textContent = project.section;
  detailCustomer.textContent = project.customer;
  detailDescription.textContent = project.description;
  detailEstimatedDate.textContent = formatDate(project.estimatedCompletionDate);

  detailLeadAssignees.innerHTML = renderAssignmentChips(leads, "lead", projectId);
  detailHelperAssignees.innerHTML = renderAssignmentChips(helpers, "helper", projectId);

  currentProjectUpdate.textContent =
    project.latestUpdate && project.latestUpdate.trim()
      ? project.latestUpdate
      : "No current project update available.";

  const noteItems = [];

  if (project.notes.length) {
    noteItems.push.apply(noteItems, project.notes.map(function (note) {
      const byline = note.createdByDisplayName
        ? "<strong>" + escapeHtml(note.createdByDisplayName) + ":</strong> "
        : "";

      return '<div class="note">' + byline + escapeHtml(note.noteText) + '</div>';
    }));
  }

  notesList.innerHTML = noteItems.length
    ? noteItems.join("")
    : `<div class="small">No work notes yet.</div>`;

  newNote.value = "";

  detailPanel.classList.add("open");
  overlay.classList.add("show");
  detailPanel.setAttribute("aria-hidden", "false");

  attachAssignmentChipEvents();
}

function closeDetailPanel() {
  detailPanel.classList.remove("open");
  overlay.classList.remove("show");
  detailPanel.setAttribute("aria-hidden", "true");
  activeProjectId = null;
}

function attachAssignmentChipEvents() {
  if (currentUser && currentUser.isGuest) return;
  document.querySelectorAll(".assignment-chip[data-worker]").forEach(function (chip) {
    chip.addEventListener("click", function () {
      const workerName = chip.dataset.worker;
      const role = chip.dataset.role;
      const projectId = Number(chip.dataset.projectId);

      unassignWorkerFromProject(workerName, projectId, role);
    });
  });
}

async function saveActiveProject() {
  const project = projects.find(function (p) {
    return p.id === activeProjectId;
  });
  if (!project) return;

  const noteText = newNote.value.trim();

  if (!noteText) {
    openDetailPanel(project.id);
    return;
  }

  try {
    const response = await fetch("../api/board/add-note.ashx", {
      method: "POST",
      credentials: "same-origin",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        postId: project.id,
        noteText: noteText
      })
    });

    const payload = await response.json();

    if (!payload.ok) {
      throw new Error(
        payload.errors && payload.errors.length
          ? payload.errors.join(" ")
          : "Failed to save project note."
      );
    }

    await loadBoardData();
    openDetailPanel(project.id);
  } catch (error) {
    console.error("saveActiveProject failed:", error);
    alert(error.message || "Failed to save project note.");
  }
}

function addProject() {
  alert("Add Project is not wired to backend yet.");
}

function formatDate(dateString) {
  const date = new Date(dateString + "T00:00:00");
  return date.toLocaleDateString("en-US", {
    year: "numeric",
    month: "short",
    day: "numeric"
  });
}

async function refreshBoard() {
  if (refreshBoardBtn) {
    refreshBoardBtn.disabled = true;
    refreshBoardBtn.textContent = "Refreshing...";
  }

  try {
    await loadBoardData();

    if (activeProjectId !== null) {
      openDetailPanel(activeProjectId);
    }
  } catch (error) {
    console.error("refreshBoard failed:", error);
    alert(error.message || "Failed to refresh board.");
  } finally {
    if (refreshBoardBtn) {
      refreshBoardBtn.disabled = false;
      refreshBoardBtn.textContent = "Refresh Board";
    }
  }
}

function escapeHtml(str) {
  return String(str)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

function countsTowardActiveLoad(status) {
  return status === "Proposed" || status === "Assigned / In Progress";
}

if (searchInput) {
  searchInput.addEventListener("input", renderBoard);
}

if (addProjectBtn) {
  addProjectBtn.addEventListener("click", addProject);
}

if (refreshBoardBtn) {
  refreshBoardBtn.addEventListener("click", function () {
    refreshBoard();
  });
}

closePanelBtn.addEventListener("click", closeDetailPanel);
overlay.addEventListener("click", closeDetailPanel);
saveProjectBtn.addEventListener("click", saveActiveProject);

// Guard the page before loading it, so a user who deep-links here without
// board access is routed to their own landing page instead of being shown a
// board frame full of 403 errors. auth.js resolves the "../" prefix itself.
window.TSU.auth
  .requirePage("assignments-board")
  .then(function (user) {
    if (!user) return null;
    return loadBoardData();
  })
  .catch(function (error) {
    console.error(error);
    board.innerHTML = '<div class="empty-state">' + escapeHtml(error.message) + "</div>";
  });
