(function () {
  "use strict";

  const DAY_MS = 24 * 60 * 60 * 1000;

  function startOfToday() {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return today;
  }

  function parseDateOnly(value) {
    if (!value) return null;
    const parts = value.split("-").map(Number);
    if (parts.length !== 3) return null;
    return new Date(parts[0], parts[1] - 1, parts[2]);
  }

  function formatDate(value, options) {
    const date = value && value.indexOf("T") >= 0 ? new Date(value) : parseDateOnly(value);
    if (!date || Number.isNaN(date.getTime())) return "Not set";
    return date.toLocaleDateString("en-US", options || { month: "short", day: "numeric" });
  }

  function daysUntil(dateOnlyValue) {
    const date = parseDateOnly(dateOnlyValue);
    if (!date) return null;
    return Math.round((date - startOfToday()) / DAY_MS);
  }

  function daysSince(dateTimeValue) {
    const date = new Date(dateTimeValue);
    if (Number.isNaN(date.getTime())) return null;
    date.setHours(0, 0, 0, 0);
    return Math.max(0, Math.round((startOfToday() - date) / DAY_MS));
  }

  function refreshStatus(updatedUtc) {
    const age = daysSince(updatedUtc);
    if (age === null) return { key: "unknown", label: "Updated", className: "inactive-badge" };
    if (age <= 5) return { key: "fresh", label: "Fresh", className: "active-badge" };
    if (age === 6) return { key: "near-stale", label: "Near Stale", className: "warn-badge" };
    return { key: "needs-refresh", label: "Needs Refresh", className: "danger-badge" };
  }

  function completionStatus(dateOnlyValue) {
    const diff = daysUntil(dateOnlyValue);
    if (diff === null) return { key: "unknown", label: "Completion Not Set", className: "inactive-badge" };
    if (diff < 0) return { key: "expired", label: "Expired Completion", className: "danger-badge" };
    if (diff <= 1) return { key: "due-soon", label: "Due Soon", className: "soon-badge" };
    return { key: "normal", label: "On Track", className: "active-badge" };
  }

  window.TSU = window.TSU || {};
  window.TSU.dates = {
    formatDate,
    daysUntil,
    daysSince,
    refreshStatus,
    completionStatus,
    parseDateOnly
  };
}());
