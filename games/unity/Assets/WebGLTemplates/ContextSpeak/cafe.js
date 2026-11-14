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
    characterBusy: false,
    ready(instance) {
      bridge.instance = instance;
      // Airport/city/location presenters are native-only and may send no CS_State snapshot.
      document.body.classList.toggle(
        "native-unity-ui",
        bridge.mode !== "character",
      );
    },
    state(json) {
      const state = JSON.parse(json);
      const creating = state.mode === "character";
      // Native UI owns every game mode; HTML is retained only for the character bridge.
      document.body.classList.toggle("native-unity-ui", !creating);
      // The outgoing additive scene may publish a final frame during loading.
      // Keep the creator open until Unity confirms a save/cancel transition.
      if (bridge.mode === "character" && !creating && !bridge.characterBusy)
        return;
      document.body.classList.toggle("character-creation", creating);
      get("character-creation").hidden = !creating;
      if (creating) {
        bridge.owner = "CharacterCreator";
        bridge.mode = "character";
        bridge.characterBusy = state.busy === true;
        document.body.classList.toggle("forest-slice", false);
        document.title = "ContextSpeak · Create your character";
        window.ContextSpeakCharacter?.render(state);
        get("unity-status").textContent = json;
        return;
      }
      const slice = state.mode === "slice";
      document.body.classList.toggle("forest-slice", slice);
      if (slice) {
        bridge.owner = "ForestCafeSlice";
        if (bridge.mode !== "slice") {
          bridge.mode = "slice";
          document.title = "ContextSpeak · Forest Café";
          const url = new URL(location.href);
          url.searchParams.set("world", "slice");
          history.replaceState(null, "", url);
        }
        get("unity-status").textContent = json;
        return;
      }
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
      get("life-menu-text").textContent = state.menu || "";
      renderMenuItems(life ? state.menuItems || [] : []);
      get("menu-open").hidden = !life || state.dialogue;
      get("menu-open").textContent = state.menuOpen
        ? "Close menu · M"
        : "Menu · M";
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
    record(owner, generation, maxSeconds = 20) {
      const send = (method, value = "") =>
        bridge.instance?.SendMessage(
          owner,
          method,
          JSON.stringify({ generation, value }),
        );
      window.contextSpeakMicrophone.start(
        () => send("OnMicReady"),
        (level) => bridge.instance?.SendMessage(owner, "OnMicActivity", JSON.stringify({ generation, level })),
        (bytes) => {
          let binary = "";
          for (let offset = 0; offset < bytes.length; offset += 8192)
            binary += String.fromCharCode(
              ...bytes.subarray(offset, offset + 8192),
            );
          send("OnMicData", btoa(binary));
        },
        (error) => send("OnMicError", error),
        maxSeconds,
      );
    },
  });
  // Menu rows: what it is, the price, a phrase to say, and (for drinks) "Pick this".
  // Picking only plans the order; the spoken order with Maya still buys it.
  let menuSignature = "";
  function renderMenuItems(items) {
    const signature = items
      .map((item) => item.id + item.planned + item.affordable)
      .join("|");
    if (signature === menuSignature) return;
    menuSignature = signature;
    get("life-menu-items").replaceChildren(
      ...items.map((item) => {
        const row = document.createElement("li");
        row.className = item.planned ? "menu-item planned" : "menu-item";
        const title = document.createElement("strong");
        title.textContent = item.name;
        const price = document.createElement("span");
        price.className = "menu-price";
        price.textContent = item.price;
        const head = document.createElement("div");
        head.className = "menu-item-head";
        head.append(title, price);
        const note = document.createElement("p");
        note.textContent = item.note;
        const phrase = document.createElement("p");
        phrase.className = "menu-phrase";
        phrase.textContent = `Say: “${item.phrase}”`;
        row.append(head, note, phrase);
        if (item.plannable) {
          const pick = document.createElement("button");
          pick.type = "button";
          pick.textContent = item.planned
            ? "Picked ✓"
            : item.affordable
              ? "Pick this"
              : "Over budget";
          pick.disabled = !item.affordable && !item.planned;
          pick.setAttribute("aria-pressed", String(item.planned));
          pick.addEventListener("click", () => command("plan", item.id));
          row.append(pick);
        }
        return row;
      }),
    );
  }
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
    ["slice", "slice"],
    ["walk", "walk"],
    ["menu-open", "menu"],
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
