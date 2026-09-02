(function () {
  "use strict";

  // Pages live either at the application root or one level down in
  // /assignments-board/. Every URL below is stored relative to the app root,
  // so subdirectory pages need a "../" prefix.
  const BASE = window.location.pathname.indexOf("/assignments-board/") !== -1 ? "../" : "";

  async function whoami() {
    return window.TSU.api.get(BASE + "api/auth/whoami.ashx");
  }

  function goTo(appRootRelativeUrl) {
    window.location.replace(BASE + (appRootRelativeUrl || "access-pending.html"));
  }

  /**
   * Guards a page against the server's authorization decision.
   *
   * The server returns user.allowedPages and user.routeTarget from a single
   * rule set (CurrentUser.GetAllowedPages). A user is only ever redirected to
   * their own routeTarget, which is by definition in their allowedPages, so a
   * redirect loop between two guarded pages cannot occur.
   *
   * @param {string} pageKey one of: section-command, section-dashboard,
   *                         user-admin, assignments-board
   */
  async function requirePage(pageKey) {
    const data = await whoami();
    const user = data.user;
    const allowed = (user && user.allowedPages) || [];

    if (!user || allowed.indexOf(pageKey) === -1) {
      goTo(user && user.routeTarget);
      return null;
    }

    window.TSU.ui.setAdminLinks(user);
    window.TSU.ui.renderUserMeta(user);
    return user;
  }

  window.TSU = window.TSU || {};
  window.TSU.auth = { whoami, requirePage };
}());
