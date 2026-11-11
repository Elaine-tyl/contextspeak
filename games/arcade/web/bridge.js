/* The engine never receives credentials or recordings. The website owns all I/O. */
(() => {
  const channel = "contextspeak-arcade";
  let latest;
  let callback;
  window.ContextSpeakArcade = {
    subscribe(receive) {
      callback = receive;
      if (latest) callback(JSON.stringify(latest));
    },
    emit(action, answerJson, roundId) {
      // A delayed action from the previous board cannot answer the new question.
      if (action !== "ready" && roundId !== latest?.round?.id) return;
      const message = { channel, action };
      if (action !== "ready") message.roundId = roundId;
      if (action === "submit") message.answer = JSON.parse(answerJson);
      window.parent.postMessage(message, window.location.origin);
    },
    // Read-only runtime status helps diagnose an exported engine without test cheats.
    report(json) {
      const status = JSON.parse(json);
      document.getElementById("arcade-status").textContent =
        JSON.stringify(status);
    },
  };
  window.addEventListener("message", (event) => {
    if (
      event.source !== window.parent ||
      event.origin !== window.location.origin
    )
      return;
    if (event.data?.channel !== channel || event.data?.action !== "state")
      return;
    latest = event.data.state;
    if (callback) callback(JSON.stringify(latest));
  });
})();
