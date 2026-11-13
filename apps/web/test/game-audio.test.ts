import { describe, expect, it, vi } from "vitest";
import { GameAudio } from "../app/games/gameAudio";
function fixture() {
  const oscillator = {
    type: "sine",
    frequency: { setValueAtTime: vi.fn() },
    connect: vi.fn(),
    start: vi.fn(),
    stop: vi.fn(),
  };
  const gain = {
    gain: { setValueAtTime: vi.fn(), exponentialRampToValueAtTime: vi.fn() },
    connect: vi.fn(),
  };
  const context = {
    currentTime: 0,
    state: "running",
    destination: {},
    createOscillator: vi.fn(() => oscillator),
    createGain: vi.fn(() => gain),
    resume: vi.fn(async () => {}),
    close: vi.fn(async () => {}),
  };
  const factory = vi.fn(() => context as unknown as AudioContext);
  return { audio: new GameAudio(factory), factory, context, oscillator };
}
describe("game sound", () => {
  it("never creates audio before a gesture, and muting suppresses feedback", async () => {
    const { audio, factory, oscillator } = fixture();
    audio.play("success");
    expect(factory).not.toHaveBeenCalled();
    await audio.unlock();
    audio.play("success");
    expect(oscillator.start).toHaveBeenCalledTimes(3);
    audio.muted = true;
    expect(oscillator.stop.mock.calls.length).toBeGreaterThan(3);
    audio.play("select");
    expect(oscillator.start).toHaveBeenCalledTimes(3);
    audio.dispose();
  });
  it("closes its context and cannot restart after disposal", async () => {
    const { audio, factory, context } = fixture();
    await audio.unlock();
    audio.dispose();
    await audio.unlock();
    audio.play("select");
    expect(context.close).toHaveBeenCalledTimes(1);
    expect(factory).toHaveBeenCalledTimes(1);
    expect(context.createOscillator).not.toHaveBeenCalled();
  });
  it("keeps gameplay usable when audio is unavailable or blocked", async () => {
    const audio = new GameAudio(() => {
      throw new Error("Audio blocked");
    });
    await expect(audio.unlock()).resolves.toBeUndefined();
    expect(() => audio.play("retry")).not.toThrow();
    audio.dispose();
  });
});
