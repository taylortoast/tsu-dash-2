(async function () {
  "use strict";

  const board = document.querySelector("[data-board]");
  const realtimeStatus = document.querySelector("[data-realtime-status]");
  const ticker = {
    container: document.querySelector("[data-ticker]"),
    title: document.querySelector("[data-ticker-title]"),
    count: document.querySelector("[data-ticker-count]"),
    date: document.querySelector("[data-ticker-date]"),
    poc: document.querySelector("[data-ticker-poc]"),
    text: document.querySelector("[data-ticker-text]")
  };

  let posts = [];
  let tickerPosts = [];
  let tickerIndex = 0;
  let tickerTimer = null;
  let realtimeSocket = null;
  let reconnectTimer = null;
  let reconnectAttempts = 0;
   let pingInterval = null;

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
      const allPosts = data.posts || [];
      const sections = publicColumnSections(data.sections || []);
      const sectionCodes = sections.map((section) => section.sectionCode);
      posts = allPosts.filter((post) => sectionCodes.includes(post.sectionCode));
      tickerPosts = allPosts.filter((post) => post.sectionCode === "TSU");
      render(posts, sections);
      startTicker();
    } catch (error) {
      board.innerHTML = "";
      board.appendChild(window.TSU.ui.el("div", "empty-state", error.message));
      setTickerEmpty(error.message);
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
      
      // ==========================================
      // START: ADDED HEARTBEAT TO PREVENT TIMEOUT
      // ==========================================
      if (pingInterval) window.clearInterval(pingInterval);
      pingInterval = window.setInterval(() => {
        if (realtimeSocket && realtimeSocket.readyState === WebSocket.OPEN) {
          // Send a tiny empty message or a 'ping' type
          realtimeSocket.send(JSON.stringify({ type: "ping" }));
        }
      }, 45000); // 45 seconds
      // ==========================================
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
      // ==========================================
      // START: CLEAR HEARTBEAT ON CLOSE
      // ==========================================
      if (pingInterval) {
        window.clearInterval(pingInterval);
        pingInterval = null;
      }
      // ==========================================

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


  function publicColumnSections(sections) {
    return sections.filter((section) => section.sectionCode !== "TSU" && section.isEnabled && section.isPublicVisible);
  }

  function render(currentPosts, sections) {
    board.innerHTML = "";
    board.dataset.columnCount = String(sections.length);
    if (!sections.length) {
      board.appendChild(window.TSU.ui.el("div", "empty-state", "No section columns are currently enabled for public display."));
      return;
    }

    sections.forEach((sectionInfo) => {
      const code = sectionInfo.sectionCode;
      const section = window.TSU.ui.el("section", "board-v2-column");
      section.appendChild(window.TSU.ui.el("h2", "board-v2-title", code));

      const windowNode = window.TSU.ui.el("div", "board-v2-window");
      const stack = window.TSU.ui.el("div", "board-v2-stack");
      const sectionPosts = currentPosts.filter((post) => post.sectionCode === code);

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
    const card = window.TSU.ui.el("article", completion.key === "due-soon" ? "board-v2-card due-soon" : "board-v2-card");
    const head = window.TSU.ui.el("div", "card-head");
    head.appendChild(window.TSU.ui.el("h3", "card-title", post.title));
    head.appendChild(window.TSU.ui.el("span", `pill ${refresh.className}`, `Updated ${window.TSU.dates.formatDate(post.updatedUtc)}`));
    card.appendChild(head);
    card.appendChild(paragraph("poc", "POC:", post.pointOfContact));
    const cardData = window.TSU.ui.el("div", "card-data");
    cardData.appendChild(stackedParagraph("desc", "Description", post.description));
    cardData.appendChild(stackedParagraph("update", "Latest Update", post.latestUpdate));
    card.appendChild(cardData);
    card.appendChild(window.TSU.ui.el("div", completion.key === "due-soon" ? "complete-pill due-soon-date" : "complete-pill", `Est. Complete: ${window.TSU.dates.formatDate(post.estimatedCompletionDate)}`));
    return card;
  }

  function paragraph(className, label, text) {
    const node = window.TSU.ui.el("p", className);
    node.appendChild(window.TSU.ui.el("span", "label", label));
    node.appendChild(document.createTextNode(` ${text || ""}`));
    return node;
  }

  function stackedParagraph(className, label, text) {
    const node = window.TSU.ui.el("div", `stacked-field ${className}`);
    node.appendChild(window.TSU.ui.el("div", "label", label));
    node.appendChild(window.TSU.ui.el("p", "field-text", text || ""));
    return node;
  }

  function startTicker() {
    if (tickerTimer) window.clearInterval(tickerTimer);
    tickerIndex = 0;

    if (!tickerPosts.length) {
      setTickerEmpty("No active TSU posts are currently available for the ticker.");
      return;
    }

    showTickerPost();
    tickerTimer = window.setInterval(() => {
      tickerIndex = (tickerIndex + 1) % tickerPosts.length;
      showTickerPost();
    }, 10000);
  }

  function showTickerPost() {
    const post = tickerPosts[tickerIndex];
    transitionTicker(() => {
      ticker.title.textContent = post.title;
      ticker.text.textContent = post.description || "";
      ticker.count.textContent = `${tickerIndex + 1} of ${tickerPosts.length}`;
      ticker.date.textContent = `Updated ${formatDateTime(post.updatedUtc)} - Est. Complete ${window.TSU.dates.formatDate(post.estimatedCompletionDate)}`;
      ticker.poc.textContent = `POC: ${post.pointOfContact || "Not listed"}`;
    });
  }

  function setTickerEmpty(message) {
    transitionTicker(() => {
      ticker.title.textContent = "No TSU Flight Information";
      ticker.count.textContent = "";
      ticker.date.textContent = "";
      ticker.poc.textContent = "";
      ticker.text.textContent = message;
    });
  }

  function transitionTicker(updateContent) {
    ticker.container.classList.add("is-fading");
    window.setTimeout(() => {
      updateContent();
      ticker.container.classList.remove("is-fading");
    }, 250);
  }

  function formatDateTime(value) {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return "Updated date unavailable";
    return date.toLocaleString("en-US", {
      month: "short",
      day: "numeric",
      hour: "numeric",
      minute: "2-digit"
    });
  }

  function setRealtimeStatus(text) {
    if (realtimeStatus) realtimeStatus.textContent = text;
  }

  updateClock();
  setInterval(updateClock, 1000);
  await loadBoard();
  connectRealtime();
}());
