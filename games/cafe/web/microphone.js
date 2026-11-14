// A single local-only microphone owner. Late permissions and canceled captures cannot submit audio.
(() => {
  let current = null;
  function cleanup(session) {
    session.source?.disconnect();
    session.node?.disconnect();
    session.stream?.getTracks().forEach((track) => track.stop());
    session.context?.close().catch(() => {});
  }
  function cancel() {
    const session = current;
    current = null;
    if (session) cleanup(session);
  }
  function finish(session) {
    if (current !== session) return;
    current = null;
    const sourceRate = session.context.sampleRate;
    const sourceLength = session.chunks.reduce(
      (sum, chunk) => sum + chunk.length,
      0,
    );
    const source = new Float32Array(sourceLength);
    let offset = 0;
    for (const chunk of session.chunks) {
      source.set(chunk, offset);
      offset += chunk.length;
    }
    const rate = 16000;
    const length = Math.floor((sourceLength * rate) / sourceRate);
    const wave = new Uint8Array(44 + length * 2);
    const view = new DataView(wave.buffer);
    for (const [offset, text] of [
      [0, "RIFF"],
      [8, "WAVEfmt "],
      [36, "data"],
    ]) {
      for (let index = 0; index < text.length; index++)
        wave[offset + index] = text.charCodeAt(index);
    }
    for (const [offset, value] of [
      [4, wave.length - 8],
      [16, 16],
      [24, rate],
      [28, rate * 2],
      [40, length * 2],
    ])
      view.setUint32(offset, value, true);
    for (const [offset, value] of [
      [20, 1],
      [22, 1],
      [32, 2],
      [34, 16],
    ])
      view.setUint16(offset, value, true);
    for (let index = 0; index < length; index++) {
      const position = (index * sourceRate) / rate;
      const left = Math.floor(position);
      const fraction = position - left;
      const sample =
        source[left] * (1 - fraction) +
        source[Math.min(left + 1, sourceLength - 1)] * fraction;
      view.setInt16(
        44 + index * 2,
        Math.round(Math.max(-1, Math.min(1, sample)) * 32767),
        true,
      );
    }
    cleanup(session);
    session.done(wave);
  }
  async function start(ready, activity, done, failed, maxSeconds = 20) {
    cancel();
    const session = { chunks: [], frames: 0, done, failed };
    current = session;
    try {
      session.context = new AudioContext({ sampleRate: 16000 });
      await session.context.resume();
      if (current !== session) return;
      session.stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          channelCount: 1,
          echoCancellation: true,
          noiseSuppression: true,
        },
      });
      if (current !== session) {
        cleanup(session);
        return;
      }
      await session.context.audioWorklet.addModule("/capture-worklet.js");
      if (current !== session) {
        cleanup(session);
        return;
      }
      session.source = session.context.createMediaStreamSource(session.stream);
      session.node = new AudioWorkletNode(session.context, "cafe-capture", {
        processorOptions: { maxSeconds },
      });
      session.node.port.onmessage = ({ data }) => {
        if (current !== session) return;
        if (data.done) {
          finish(session);
          return;
        }
        session.chunks.push(data.samples);
        session.frames += data.samples.length;
        // Meter updates at most ten times per second, independently of PCM collection.
        const seconds = session.frames / session.context.sampleRate;
        if (seconds - (session.lastMeter ?? -1) >= 0.1) {
          session.lastMeter = seconds;
          activity(
            Math.min(
              1,
              data.samples.reduce(
                (peak, value) => Math.max(peak, Math.abs(value)),
                0,
              ) * 4,
            ),
            seconds,
          );
        }
      };
      const silent = session.context.createGain();
      silent.gain.value = 0;
      session.source
        .connect(session.node)
        .connect(silent)
        .connect(session.context.destination);
      ready();
    } catch (error) {
      if (current !== session) {
        cleanup(session);
        return;
      }
      cancel();
      failed(
        error.name === "NotAllowedError"
          ? "Microphone permission denied. Allow access and retry recording."
          : "Microphone unavailable. Check your input device and retry recording.",
      );
    }
  }
  function stop() {
    if (current?.node) {
      current.node.port.postMessage("stop");
      return;
    }
    const session = current;
    cancel();
    session?.failed(
      "Recording stopped before microphone was ready. Wait for the indicator and retry.",
    );
  }
  window.contextSpeakMicrophone = { start, stop, cancel };
  window.addEventListener("pagehide", cancel);
})();
