import type { Page } from "@playwright/test";
export async function installAudioFixtures(page: Page) {
  await page.addInitScript(() => {
    (window as any).nativeAudio = {
      Recorder: window.MediaRecorder,
      microphone: navigator.mediaDevices.getUserMedia.bind(
        navigator.mediaDevices,
      ),
    };
    const state: any = ((window as any).audioFixture = {
      stops: 0,
      pauses: 0,
      deny: false,
      holdAudio: false,
      spoken: [] as string[],
    });
    const pause = HTMLMediaElement.prototype.pause;
    HTMLMediaElement.prototype.pause = function () {
      state.pauses++;
      pause.call(this);
    };
    Object.defineProperty(window, "Audio", {
      value: class {
        onended: any;
        onerror: any;
        src = "";
        pause() {
          state.pauses++;
        }
        removeAttribute() {}
        load() {}
        async play() {
          state.spoken.push("AI");
          state.finishAudio = () => this.onended?.(new Event("ended"));
          if (!state.holdAudio) setTimeout(state.finishAudio, 0);
        }
      },
    });
    Object.defineProperty(window, "speechSynthesis", {
      get() {
        throw new Error("Device speech must not be used");
      },
    });
    Object.defineProperty(navigator.mediaDevices, "getUserMedia", {
      configurable: true,
      value: async () => {
        if (state.deny) throw new DOMException("Denied", "NotAllowedError");
        const stream = { getTracks: () => [{ stop: () => state.stops++ }] };
        if (state.delay)
          return new Promise((resolve) => {
            state.grant = () => resolve(stream);
          });
        return stream;
      },
    });
    function silentRecording() {
      const buffer = new ArrayBuffer(1644);
      const view = new DataView(buffer);
      for (const [offset, text] of [
        [0, "RIFF"],
        [8, "WAVE"],
        [12, "fmt "],
        [36, "data"],
      ] as const)
        for (let index = 0; index < text.length; index++)
          view.setUint8(offset + index, text.charCodeAt(index));
      view.setUint32(4, 1636, true);
      view.setUint32(16, 16, true);
      view.setUint16(20, 1, true);
      view.setUint16(22, 1, true);
      view.setUint32(24, 8000, true);
      view.setUint32(28, 16000, true);
      view.setUint16(32, 2, true);
      view.setUint16(34, 16, true);
      view.setUint32(40, 1600, true);
      return new Blob([buffer], { type: "audio/wav" });
    }
    class Recorder {
      state = "inactive";
      mimeType = "audio/wav";
      ondataavailable: any;
      onstop: any;
      onerror: any;
      static isTypeSupported() {
        return true;
      }
      start() {
        this.state = "recording";
      }
      stop() {
        this.state = "inactive";
        this.ondataavailable?.({
          data: silentRecording(),
        });
        this.onstop?.(new Event("stop"));
      }
    }
    Object.defineProperty(window, "MediaRecorder", {
      configurable: true,
      value: Recorder,
    });
  });
}
