// Audio runs independently of the game's render loop; buffers are transferred, not copied per sample.
class CafeCapture extends AudioWorkletProcessor {
  constructor() {
    super();
    this.active = true;
    this.frames = 0;
    this.port.onmessage = () => {
      this.active = false;
      this.port.postMessage({ done: true });
    };
  }
  process(inputs) {
    if (!this.active) return false;
    const channels = inputs[0];
    if (!channels?.[0]) return true;
    const count = Math.min(channels[0].length, 20 * sampleRate - this.frames);
    if (count > 0) {
      const mono = new Float32Array(count);
      for (let index = 0; index < count; index++) {
        mono[index] =
          channels.reduce((sum, channel) => sum + channel[index], 0) /
          channels.length;
      }
      this.frames += count;
      this.port.postMessage({ samples: mono }, [mono.buffer]);
    }
    if (this.frames >= 20 * sampleRate) {
      this.active = false;
      this.port.postMessage({ done: true });
    }
    return this.active;
  }
}
registerProcessor("cafe-capture", CafeCapture);
