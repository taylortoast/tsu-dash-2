(function () {
  "use strict";

  function el(tag, className, text) {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (text !== undefined && text !== null) node.textContent = text;
    return node;
  }

  function setMessage(text, type) {
    const box = document.querySelector("[data-message]");
    if (!box) return;
    box.textContent = text || "";
    box.className = `message ${type || ""}`.trim();
    box.hidden = !text;
  }

  function badge(text, className) {
    const node = el("span", `badge ${className || ""}`.trim(), text);
    return node;
  }

  function setAdminLinks(user) {
    document.querySelectorAll("[data-admin-only]").forEach((node) => {
      node.hidden = user && user.isGuest && node.hasAttribute("data-guest-visible")
        ? false
        : !(user && user.isActive && user.isAdmin);
    });
  }

  function renderUserMeta(user, suffix) {
    const meta = document.querySelector("[data-user-meta]");
    if (!meta || !user) return;
    const name = user.displayName || user.windowsUserName || "Authenticated User";
    const section = user.isGuest ? "Read-only guest view" : (user.sectionCode ? `Section: ${user.sectionCode}` : "No section assigned");
    meta.textContent = suffix ? `${name} - ${section} - ${suffix}` : `${name} - ${section}`;
  }

  function setReadOnly(user) {
    if (!user || !user.isGuest) return;
    document.body.classList.add("guest-mode");
    document.querySelectorAll("[data-guest-disabled]").forEach((node) => { node.disabled = true; });
    document.querySelectorAll("[data-guest-hidden]").forEach((node) => { node.hidden = true; });

    if (document.querySelector("[data-guest-label]")) return;

    const label = document.createElement("div");
    label.className = "guest-mode-label";
    label.dataset.guestLabel = "1";
    label.textContent = "Guest Mode";
    label.setAttribute("role", "status");
    document.body.appendChild(label);

    const exit = document.createElement("a");
    exit.className = "guest-mode-exit";
    exit.dataset.guestExit = "1";
    exit.href = window.location.pathname.indexOf("/assignments-board/") !== -1
      ? "../index.html"
      : "index.html";
    exit.textContent = "Exit Guest Mode";
    exit.addEventListener("click", (event) => {
      event.preventDefault();
      document.cookie = "TSUGuest=; Max-Age=0; Path=/; SameSite=Strict";
      window.location.replace(exit.href);
    });
    document.body.appendChild(exit);
  }

  window.TSU = window.TSU || {};
  window.TSU.ui = { el, setMessage, badge, setAdminLinks, renderUserMeta, setReadOnly };
}());
