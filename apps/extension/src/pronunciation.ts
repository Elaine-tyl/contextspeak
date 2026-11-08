export function mountPronunciation(
  button: HTMLButtonElement,
  text: string,
  readLanguage: () => string,
  report: (message: string) => void,
) {
  let language = "";
  let voice: SpeechSynthesisVoice | undefined;
  let current: SpeechSynthesisUtterance | undefined;
  let deadline: ReturnType<typeof setTimeout>;
  let disposed = false;
  const synth = window.speechSynthesis;
  function stop() {
    const owned = current;
    current = undefined;
    clearTimeout(deadline);
    if (owned) synth.cancel();
  }
  function update() {
    if (disposed) return;
    const next = readLanguage();
    if (next !== language) {
      stop();
      report("");
    }
    language = next;
    voice = synth
      ?.getVoices()
      .find(
        (candidate) =>
          candidate.localService &&
          candidate.lang.split("-")[0].toLowerCase() ===
            language.split("-")[0].toLowerCase(),
      );
    button.disabled = !voice || !!current;
    button.title = voice
      ? "Listen to selected text"
      : "No matching on-device voice. Wait for language detection or install a device voice.";
  }
  function play() {
    if (!voice || current || disposed) return;
    const utterance = new SpeechSynthesisUtterance(text);
    utterance.voice = voice;
    utterance.lang = voice.lang;
    current = utterance;
    button.disabled = true;
    report("Playing selected text…");
    function finish(message: string) {
      if (current !== utterance || disposed) return;
      current = undefined;
      clearTimeout(deadline);
      report(message);
      update();
    }
    utterance.onend = () => finish("");
    utterance.onerror = () => finish("Audio interrupted. Try listening again.");
    deadline = setTimeout(() => {
      finish("Audio did not finish. Check your device voice and retry.");
      synth.cancel();
    }, 120000);
    try {
      synth.speak(utterance);
    } catch {
      finish("Audio could not start. Check your device voice and retry.");
    }
  }
  button.addEventListener("click", play);
  synth?.addEventListener("voiceschanged", update);
  update();
  return {
    update,
    dispose() {
      disposed = true;
      stop();
      button.removeEventListener("click", play);
      synth?.removeEventListener("voiceschanged", update);
    },
  };
}
