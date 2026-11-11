/* Accessible controls mirror the Unity-owned state. No HTML/API client grades answers. */
(() => {
  const get = (id) => document.getElementById(id);
  const command = (action, text = "") =>
    window.ContextSpeakUnity.instance?.SendMessage(
      window.ContextSpeakUnity.owner,
      "OnBrowserCommand",
      JSON.stringify({ action, text }),
    );
  const bridge = (window.ContextSpeakUnity = {
    instance: null,
    owner: "CafeRuntime",
    mode: "cafe",
    state(json) {
      const state = JSON.parse(json);
      const life = state.mode === "life";
      bridge.owner = life ? "LifeRuntime" : "CafeRuntime";
      if (bridge.mode !== (life ? "life" : "cafe")) {
        bridge.mode = life ? "life" : "cafe";
        const choices = life
          ? [
              ["continue", "Continue saved day"],
              ["home", "Home · practice"],
              ["market", "Market · practice"],
              ["cafe", "Cafe · practice"],
              ["friend", "Friend · practice"],
            ]
          : [
              ["continue", "Continue saved task"],
              ["counter", "Counter · practice"],
              ["friend", "Friend chat · practice"],
            ];
        get("stage").replaceChildren(
          ...choices.map(([value, text]) => new Option(text, value)),
        );
        const url = new URL(location.href);
        url.searchParams.set("world", bridge.mode);
        history.replaceState(null, "", url);
      }
      get("world-title").textContent = life
        ? "Life · Your first day"
        : "Unity · Forest Cafe";
      document.title = life
        ? "ContextSpeak Life · Your first day"
        : "ContextSpeak · Unity Forest Cafe";
      get("life").hidden = life;
      get("cafe").hidden = !life;
      get("reset").hidden = life;
      get("walk").hidden = !life;
      get("life-inventory").hidden = !life;
      get("wallet").textContent = state.wallet || "";
      get("bag").textContent = state.bag || "";
      get("travel-status").hidden = !life || state.dialogue || !state.status;
      get("travel-status").textContent = state.status || "";
      get("life-menu").hidden = !life || !state.menu;
      get("life-menu").textContent = state.menu || "";
      get("reply-label").textContent = life ? "Typed rehearsal" : "Your reply";
      get("send").textContent = life ? "Check typed rehearsal" : "Send reply";
      get("repair-note").hidden = !life || !state.repair;
      get("repair-note").textContent = state.repair
        ? `Try saying: ${state.repair}`
        : "";
      get("hear-repair").hidden = !life || !state.repair;
      get("hear-repair").disabled = state.busy || state.recording !== "idle";
      get("unity-status").textContent = json;
      for (const id of [
        "objective",
        "speaker",
        "line",
        "feedback",
        "transcript",
        "status",
      ])
        get(id).textContent = state[id];
      get("dialogue").hidden = !state.dialogue;
      get("interact").hidden = !state.nearby;
      get("interact").textContent = state.nearby;
      get("join").hidden = !state.join;
      get("send").disabled =
        state.busy || state.complete || state.recording !== "idle";
      get("record").disabled = state.busy || state.complete;
      get("hear").disabled = state.busy || state.recording !== "idle";
      get("reply").disabled =
        state.busy || state.complete || state.recording !== "idle";
      get("record").textContent =
        state.recording === "recording"
          ? "Stop recording"
          : state.recording === "requesting"
            ? "Cancel request"
            : "Record reply";
    },
    record(owner, generation) {
      const send = (method, value = "") =>
        bridge.instance?.SendMessage(
          owner,
          method,
          JSON.stringify({ generation, value }),
        );
      window.contextSpeakMicrophone.start(
        () => send("OnMicReady"),
        () => {},
        (bytes) => {
          let binary = "";
          for (let offset = 0; offset < bytes.length; offset += 8192)
            binary += String.fromCharCode(
              ...bytes.subarray(offset, offset + 8192),
            );
          send("OnMicData", btoa(binary));
        },
        (error) => send("OnMicError", error),
      );
    },
  });
  get("dialogue").addEventListener("submit", (event) => {
    event.preventDefault();
    const text = get("reply").value.trim();
    if (!text || get("send").disabled) return;
    command("send", text);
    get("reply").value = "";
  });
  for (const [id, action] of [
    ["record", "record"],
    ["hear", "hear"],
    ["close", "close"],
    ["join", "join"],
    ["reconnect", "connect"],
    ["reset", "reset"],
    ["interact", "interact"],
    ["life", "life"],
    ["cafe", "cafe"],
    ["walk", "walk"],
    ["hear-repair", "repair"],
  ])
    get(id).addEventListener("click", () => command(action));
  get("stage").addEventListener("change", () =>
    command("stage", get("stage").value),
  );
  // Register before Unity starts: HTML fields own their keys, the canvas owns movement.
  for (const type of ["keydown", "keyup", "keypress"]) {
    window.addEventListener(
      type,
      (event) => {
        if (
          !["INPUT", "TEXTAREA", "SELECT", "BUTTON"].includes(
            event.target?.tagName,
          )
        )
          return;
        event.stopImmediatePropagation();
        if (
          type === "keydown" &&
          event.key === "Enter" &&
          !event.shiftKey &&
          event.target === get("reply")
        ) {
          event.preventDefault();
          get("dialogue").requestSubmit();
        }
        if (type === "keydown" && event.key === "Escape") {
          command("close");
          get("unity-canvas").focus();
        }
      },
      true,
    );
  }
  const cancel = () => {
    window.contextSpeakMicrophone.cancel();
    command("close");
  };
  window.addEventListener("pagehide", cancel);
  document.addEventListener("visibilitychange", () => {
    if (document.hidden) cancel();
  });
})();
