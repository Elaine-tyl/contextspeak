"use client";
import {
  createContext,
  useContext,
  useCallback,
  useEffect,
  useRef,
  useState,
  type PointerEvent,
  type ReactNode,
} from "react";
import { usePathname } from "next/navigation";

const preference = "contextspeak.motion";
const MotionState = createContext(false);
export function useSiteMotion() {
  return useContext(MotionState);
}
const tiltProperties = ["--tilt-x", "--tilt-y", "--glow-x", "--glow-y"];

export function SiteMotion({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const shell = useRef<HTMLDivElement>(null);
  const active = useRef<HTMLElement | null>(null);
  const frame = useRef(0);
  const [enabled, setEnabled] = useState(true);
  const [reduced, setReduced] = useState(false);
  const moving = enabled && !reduced;

  const reset = useCallback(() => {
    cancelAnimationFrame(frame.current);
    if (active.current) {
      active.current.removeAttribute("data-tilting");
      tiltProperties.forEach((property) =>
        active.current?.style.removeProperty(property),
      );
      active.current = null;
    }
  }, []);

  useEffect(() => {
    try {
      setEnabled(localStorage.getItem(preference) !== "off");
    } catch {
      /* Effects work without storage. */
    }
    const media = matchMedia("(prefers-reduced-motion: reduce)");
    const update = () => {
      setReduced(media.matches);
      reset();
    };
    update();
    media.addEventListener("change", update);
    window.addEventListener("scroll", reset, { passive: true });
    window.addEventListener("resize", reset);
    return () => {
      reset();
      media.removeEventListener("change", update);
      window.removeEventListener("scroll", reset);
      window.removeEventListener("resize", reset);
    };
  }, [reset]);

  useEffect(() => {
    reset();
    if (!moving) return;
    const entrance = shell.current?.querySelector(".app-main")?.animate?.(
      [
        { opacity: 0.8, translate: "0 8px" },
        { opacity: 1, translate: "0 0" },
      ],
      { duration: 260, easing: "ease-out" },
    );
    return () => entrance?.cancel();
  }, [pathname, moving, reset]);

  function track(event: PointerEvent<HTMLDivElement>) {
    if (
      !moving ||
      event.pointerType !== "mouse" ||
      !matchMedia("(hover: hover) and (pointer: fine)").matches
    )
      return;
    const target = event.target as HTMLElement;
    const card = target.closest<HTMLElement>("[data-depth]");
    // Never shift a surface while someone is editing, selecting text or using its controls.
    if (
      !card ||
      card.matches(":disabled") ||
      target.closest(
        "input, textarea, select, audio, .word-speaker, [contenteditable]",
      ) ||
      card.querySelector(
        "input:focus, textarea:focus, .word-speaker:focus, details[open]",
      ) ||
      event.buttons
    ) {
      reset();
      return;
    }
    if (active.current !== card) {
      reset();
      active.current = card;
    }
    const bounds = card.getBoundingClientRect();
    const x = Math.max(
      0,
      Math.min(1, (event.clientX - bounds.left) / bounds.width),
    );
    const y = Math.max(
      0,
      Math.min(1, (event.clientY - bounds.top) / bounds.height),
    );
    cancelAnimationFrame(frame.current);
    frame.current = requestAnimationFrame(() => {
      card.style.setProperty("--tilt-x", `${(0.5 - y) * 6}deg`);
      card.style.setProperty("--tilt-y", `${(x - 0.5) * 6}deg`);
      card.style.setProperty("--glow-x", `${x * 100}%`);
      card.style.setProperty("--glow-y", `${y * 100}%`);
      card.setAttribute("data-tilting", "true");
    });
  }

  function toggle() {
    reset();
    setEnabled(!enabled);
    try {
      localStorage.setItem(preference, enabled ? "off" : "on");
    } catch {
      /* Keep the current-session choice. */
    }
  }

  return (
    <MotionState.Provider value={moving}>
      <div
        className="app-shell"
        ref={shell}
        data-motion={moving ? "on" : "off"}
        onPointerMove={track}
        onPointerLeave={reset}
        onPointerDown={reset}
      >
        {children}
        <button
          className="motion-toggle"
          aria-label="3D effects"
          aria-pressed={moving}
          disabled={reduced}
          title={
            reduced
              ? "Reduced motion is enabled on your device."
              : "Turn decorative 3D motion on or off"
          }
          onClick={toggle}
        >
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.6"
            aria-hidden="true"
          >
            <path d="m12 2 9 5v10l-9 5-9-5V7zM3 7l9 5 9-5M12 12v10" />
          </svg>
          3D effects <span>{reduced ? "Reduced" : enabled ? "On" : "Off"}</span>
        </button>
      </div>
    </MotionState.Provider>
  );
}
