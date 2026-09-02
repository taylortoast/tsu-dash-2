(function () {
  "use strict";

  async function request(url, options) {
    const response = await fetch(url, {
      credentials: "same-origin",
      headers: { "Content-Type": "application/json" },
      ...options
    });

    const payload = await response.json().catch(() => ({
      ok: false,
      errors: ["The server returned a non-JSON response."]
    }));

    if (!response.ok || !payload.ok) {
      const message = payload.errors && payload.errors.length
        ? payload.errors.join(" ")
        : "Request failed.";
      throw new Error(message);
    }

    return payload.data;
  }

  window.TSU = window.TSU || {};
  window.TSU.api = {
    get(url) {
      return request(url, { method: "GET" });
    },
    post(url, body) {
      return request(url, { method: "POST", body: JSON.stringify(body || {}) });
    }
  };
}());
