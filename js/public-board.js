(async function () {
  "use strict";

  const board = document.querySelector("[data-board]");
  const realtimeStatus = document.querySelector("[data-realtime-status]");
  let realtimeSocket = null;
  let reconnectTimer = null;
  let reconnectAttempts = 0;

  function updateClock() {
    const clock = document.getElementById("clock");
    clock.textContent = new Date().toLocaleString("en-US", {
      weekday: "long",
      month: "long",
      day: "numeric",
      year: "numeric",
      hour: "numeric",
      minute: "2-digit"
    });
  }

  async function loadBoard() {
    try {
      const data = await window.TSU.api.get("api/public-board/active-posts.ashx");
      render(data.posts || [], publicColumnSections(data.sections || []));
    } catch (error) {
      board.innerHTML = "";
      const empty = window.TSU.ui.el("div", "empty-state", error.message);
      board.appendChild(empty);
    }
  }

  function connectRealtime() {
    if (!("WebSocket" in window)) {
      setRealtimeStatus("Realtime unavailable");
      return;
    }

    if (realtimeSocket && (realtimeSocket.readyState === WebSocket.OPEN || realtimeSocket.readyState === WebSocket.CONNECTING)) {
      return;
    }

    const socketUrl = new URL("api/public-board/socket.ashx", window.location.href);
    socketUrl.protocol = window.location.protocol === "https:" ? "wss:" : "ws:";

    setRealtimeStatus("Realtime connecting");
    realtimeSocket = new WebSocket(socketUrl.href);

    realtimeSocket.onopen = () => {
      reconnectAttempts = 0;
      setRealtimeStatus("Realtime connected");
    };

    realtimeSocket.onmessage = async (event) => {
      let message;
      try {
        message = JSON.parse(event.data);
      } catch {
        return;
      }

      if (message.type === "public-board-changed") {
        setRealtimeStatus("Realtime update received");
        await loadBoard();
        setRealtimeStatus("Realtime connected");
      }
    };

    realtimeSocket.onerror = () => {
      setRealtimeStatus("Realtime error");
    };

    realtimeSocket.onclose = () => {
      reconnectAttempts += 1;
      if (reconnectAttempts > 6) {
        setRealtimeStatus("Realtime disconnected");
        return;
      }

      const delay = Math.min(60000, 5000 * reconnectAttempts);
      setRealtimeStatus(`Realtime reconnecting in ${Math.round(delay / 1000)}s`);
      if (reconnectTimer) window.clearTimeout(reconnectTimer);
      reconnectTimer = window.setTimeout(connectRealtime, delay);
    };
  }

  function setRealtimeStatus(text) {
    if (realtimeStatus) realtimeStatus.textContent = text;
  }

  function publicColumnSections(sections) {
    return sections.filter((section) => section.sectionCode !== "TSU" && section.isEnabled && section.isPublicVisible);
  }

  function render(posts, sections) {
    board.innerHTML = "";
    board.dataset.columnCount = String(sections.length);
    if (!sections.length) {
      board.appendChild(window.TSU.ui.el("div", "empty-state", "No section columns are currently enabled for public display."));
      return;
    }

    sections.forEach((sectionInfo) => {
      const code = sectionInfo.sectionCode;
      const section = window.TSU.ui.el("section", "section-column");
      section.appendChild(window.TSU.ui.el("h2", "section-title", code));

      const windowNode = window.TSU.ui.el("div", "card-window");
      const stack = window.TSU.ui.el("div", "card-stack");
      const sectionPosts = posts.filter((post) => post.sectionCode === code);

      if (!sectionPosts.length) {
        stack.appendChild(window.TSU.ui.el("div", "empty-state", "No active public posts"));
      } else {
        sectionPosts.forEach((post) => stack.appendChild(renderCard(post)));
        if (sectionPosts.length > 3) {
          sectionPosts.forEach((post) => stack.appendChild(renderCard(post)));
          stack.classList.add("scrolling");
        }
      }

      windowNode.appendChild(stack);
      section.appendChild(windowNode);
      board.appendChild(section);
    });
  }

  function renderCard(post) {
    const completion = window.TSU.dates.completionStatus(post.estimatedCompletionDate);
    const refresh = window.TSU.dates.refreshStatus(post.updatedUtc);
    const card = window.TSU.ui.el("article", completion.key === "due-soon" ? "info-card due-soon" : "info-card");
    const head = window.TSU.ui.el("div", "card-head");
    head.appendChild(window.TSU.ui.el("h3", "card-title", post.title));
    head.appendChild(window.TSU.ui.el("span", `pill ${refresh.className}`, `Updated ${window.TSU.dates.formatDate(post.updatedUtc)}`));
    card.appendChild(head);
    card.appendChild(paragraph("poc", "POC:", post.pointOfContact));
    const cardData = window.TSU.ui.el("div", "card-data");
    cardData.appendChild(stackedParagraph("desc", "Description", post.description));
    cardData.appendChild(stackedParagraph("update", "Latest Update", post.latestUpdate));
    card.appendChild(cardData);
    const complete = window.TSU.ui.el("div", completion.key === "due-soon" ? "complete-pill due-soon-date" : "complete-pill", `Est. Complete: ${window.TSU.dates.formatDate(post.estimatedCompletionDate)}`);
    card.appendChild(complete);
    return card;
  }

  function paragraph(className, label, text) {
    const node = window.TSU.ui.el("p", className);
    const labelNode = window.TSU.ui.el("span", "label", label);
    node.appendChild(labelNode);
    node.appendChild(document.createTextNode(` ${text || ""}`));
    return node;
  }

  function stackedParagraph(className, label, text) {
    const node = window.TSU.ui.el("div", `stacked-field ${className}`);
    node.appendChild(window.TSU.ui.el("div", "label", label));
    node.appendChild(window.TSU.ui.el("p", "field-text", text || ""));
    return node;
  }

  updateClock();
  setInterval(updateClock, 1000);
  await loadBoard();
  connectRealtime();
}());
