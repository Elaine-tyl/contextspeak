// One browser voice owner, independent of Godot's Web sample playback mode.
(() => {
  let context;
  let current;
  function unlock() {
    context ??= new AudioContext();
    return context.resume();
  }
  function stop() {
    const session = current;
    current = null;
    session?.controller.abort();
    if (session?.source) {
      session.source.onended = null;
      session.source.stop();
      session.source.disconnect();
    }
  }
  async function speak(token, text, started, ended, failed) {
    stop();
    const session = { controller: new AbortController() };
    current = session;
    try {
      await unlock();
      if (current !== session) return;
      const response = await fetch("/speech", {
        method: "POST",
        signal: session.controller.signal,
        headers: {
          "Content-Type": "application/json",
          "X-ContextSpeak-Client": "web",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({ text }),
      });
      if (!response.ok) throw new Error("Local voice unavailable");
      const bytes = await response.arrayBuffer();
      if (bytes.byteLength > 3200000) throw new Error("Voice line is too long");
      const audio = await context.decodeAudioData(bytes);
      if (current !== session) return;
      session.source = context.createBufferSource();
      session.source.buffer = audio;
      session.source.connect(context.destination);
      session.source.onended = () => {
        if (current !== session) return;
        current = null;
        session.source.disconnect();
        ended();
      };
      session.source.start();
      started();
    } catch {
      if (current !== session) return;
      stop();
      failed();
    }
  }
  window.contextSpeakVoice = { speak, stop, unlock };
  window.addEventListener("pagehide", () => {
    stop();
    context?.close();
  });
})();
