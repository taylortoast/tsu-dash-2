(async function () {
  "use strict";

  const guestButton = document.querySelector("[data-guest-access]");
  const requestAccess = document.querySelector("[data-request-access]");

  function clearGuestMode() {
    document.cookie = "TSUGuest=; Max-Age=0; Path=/; SameSite=Strict";
  }

  if (requestAccess) requestAccess.addEventListener("click", clearGuestMode);

  if (guestButton) {
    guestButton.addEventListener("click", () => {
      document.cookie = "TSUGuest=1; Max-Age=28800; Path=/; SameSite=Strict";
      window.location.replace("section-dashboard.html");
    });
  }
}());
