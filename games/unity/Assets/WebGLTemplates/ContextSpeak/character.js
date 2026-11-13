/* Accessible browser controls share the validated Unity character profile and real 3D preview. */
(() => {
  const get = (id) => document.getElementById(id);
  const send = (action, text = "") => {
    const bridge = window.ContextSpeakUnity;
    bridge.instance?.SendMessage(
      "CharacterCreator",
      "OnBrowserCommand",
      JSON.stringify({ action, text }),
    );
  };
  for (const [id, action] of [
    ["character-name", "name"],
    ["character-height", "height"],
    ["character-build", "build"],
    ["character-rotation", "rotate"],
  ])
    get(id).addEventListener("input", (event) =>
      send(action, event.target.value),
    );
  get("character-band").addEventListener("change", (event) =>
    send("band", event.target.value),
  );
  for (const [id, action, text] of [
    ["character-girl", "look", "Girl"],
    ["character-boy", "look", "Boy"],
    ["character-left", "turn-left", ""],
    ["character-right", "turn-right", ""],
    ["character-reset", "reset-preview", ""],
    ["character-cancel", "cancel", ""],
  ])
    get(id).addEventListener("click", () => send(action, text));
  get("character-creation").addEventListener("submit", (event) => {
    event.preventDefault();
    send("confirm");
  });

  window.ContextSpeakCharacter = {
    render(state) {
      const profile = state.profile;
      for (const [id, value] of [
        ["character-name", profile.Name],
        ["character-height", profile.Height],
        ["character-build", profile.Build],
        ["character-band", profile.Band],
        ["character-rotation", state.rotation],
      ]) {
        const input = get(id);
        if (document.activeElement !== input) input.value = String(value);
      }
      get("character-girl").setAttribute(
        "aria-pressed",
        String(profile.Look === 0),
      );
      get("character-boy").setAttribute(
        "aria-pressed",
        String(profile.Look === 1),
      );
      get("character-cancel").hidden = !state.canCancel;
      get("character-fields").disabled = state.busy;
      get("character-status").textContent = state.status || "";
      get("character-height-value").textContent =
        Math.round(profile.Height * 100) + "%";
      get("character-build-value").textContent =
        Math.round(profile.Build * 100) + "%";
    },
  };
})();
