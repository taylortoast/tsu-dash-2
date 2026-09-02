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
      node.hidden = !(user && user.isActive && user.isAdmin);
    });
  }

  function renderUserMeta(user, suffix) {
    const meta = document.querySelector("[data-user-meta]");
    if (!meta || !user) return;
    const name = user.displayName || user.windowsUserName || "Authenticated User";
    const section = user.sectionCode ? `Section: ${user.sectionCode}` : "No section assigned";
    meta.textContent = suffix ? `${name} - ${section} - ${suffix}` : `${name} - ${section}`;
  }

  window.TSU = window.TSU || {};
  window.TSU.ui = { el, setMessage, badge, setAdminLinks, renderUserMeta };
}());
