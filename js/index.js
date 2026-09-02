(async function () {
  "use strict";

  const form = document.querySelector("[data-name-form]");
  const input = document.querySelector("#display-name");
  const box = document.querySelector("[data-message]");

  function showError(message) {
    if (!box) return;
    box.hidden = false;
    box.textContent = message;
    box.className = "message error";
  }

  try {
    const data = await window.TSU.api.get("api/auth/route.ashx");
    const user = data.user || {};

    if (!user.displayName && form) {
      form.hidden = false;
      input.focus();

      form.addEventListener("submit", async (event) => {
        event.preventDefault();

        const displayName = input.value.trim();
        if (!displayName) {
          showError("Enter your name.");
          input.focus();
          return;
        }

        try {
          await window.TSU.api.post("api/auth/update-display-name.ashx", {
            displayName
          });

          window.location.replace(data.target || "access-pending.html");
        } catch (error) {
          showError(error.message);
        }
      });

      return;
    }

    window.location.replace(data.target || "access-pending.html");
  } catch (error) {
    showError(error.message);
  }
}());
