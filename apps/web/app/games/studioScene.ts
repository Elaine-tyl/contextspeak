import * as THREE from "three";
import type { GameKind } from "@contextspeak/contracts";
import { buildPortal } from "./lobbyArt";
import { freeScene } from "./sceneResources";
import { studioThemes, type StudioActivity } from "./studioThemes";

type StudioState = { moving: boolean; activity: StudioActivity; count: number };
export type StudioScene = { update(state: StudioState): void; dispose(): void };

/** A local, cosmetic diorama. Input activity changes its lights, never the score. */
export function createStudioScene(
  host: HTMLElement,
  canvas: HTMLCanvasElement,
  kind: GameKind,
  level: number,
  failed: () => void,
): StudioScene {
  const renderer = new THREE.WebGLRenderer({
    canvas,
    alpha: true,
    antialias: true,
  });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 1.5));
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(34, 1, 0.1, 30);
  camera.position.set(0, 3.8, 6.5);
  camera.lookAt(0, 0.65, 0);
  scene.add(new THREE.HemisphereLight("#fff8ed", "#9aabb6", 3));
  const light = new THREE.DirectionalLight("#fff1d9", 3);
  light.position.set(-3, 5, 5);
  scene.add(light);
  const index = Object.keys(studioThemes).indexOf(kind);
  const art = buildPortal({ kind, label: "", level }, index);
  scene.add(art.group);
  const sparks = new THREE.Group();
  const sparkGeometry = new THREE.SphereGeometry(0.055, 6, 4);
  for (let i = 0; i < 12; i++) {
    const spark = new THREE.Mesh(
      sparkGeometry,
      new THREE.MeshBasicMaterial({ color: studioThemes[kind].accent }),
    );
    const angle = (i * Math.PI) / 6;
    spark.position.set(Math.cos(angle) * 1.9, 0.55, Math.sin(angle) * 1.9);
    sparks.add(spark);
  }
  scene.add(sparks);
  let state: StudioState = { moving: false, activity: "ready", count: 0 };
  let visible = !document.hidden;
  let inView = true;
  let disposed = false;
  let tilt = 0;

  function render(time: number) {
    if (disposed) return;
    const active = state.moving && state.activity !== "recording";
    const playing = state.activity === "listening";
    art.prop.position.y = active
      ? Math.sin(time * 0.0015) * (playing ? 0.12 : 0.035)
      : 0;
    art.group.rotation.y = state.moving ? tilt : 0;
    art.ring.scale.setScalar(
      playing && active ? 1 + Math.sin(time * 0.006) * 0.07 : 1,
    );
    sparks.rotation.y = active && playing ? Math.sin(time * 0.001) * 0.25 : 0;
    sparks.children.forEach((spark, i) => {
      // Count is only interaction activity, not correctness or microphone volume.
      spark.scale.setScalar(
        state.activity === "checked" || i < Math.min(state.count, 12)
          ? 1.7
          : 0.65,
      );
    });
    renderer.render(scene, camera);
  }
  function loop() {
    renderer.setAnimationLoop(
      state.moving &&
        state.activity !== "recording" &&
        visible &&
        inView &&
        !disposed
        ? render
        : null,
    );
    if (visible && inView) render(performance.now());
  }
  function resize() {
    const width = Math.max(host.clientWidth, 1);
    const height = Math.max(host.clientHeight, 1);
    renderer.setSize(width, height, false);
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
    render(performance.now());
  }
  function pointer(event: PointerEvent) {
    if (
      !state.moving ||
      state.activity === "recording" ||
      event.pointerType === "touch"
    )
      return;
    const bounds = canvas.getBoundingClientRect();
    tilt = ((event.clientX - bounds.left) / bounds.width - 0.5) * 0.45;
    render(performance.now());
  }
  function leave() {
    tilt = 0;
    render(performance.now());
  }
  function visibility() {
    visible = !document.hidden;
    loop();
  }
  function lost(event: Event) {
    event.preventDefault();
    dispose();
    failed();
  }
  const observer = new ResizeObserver(resize);
  const viewport = new IntersectionObserver(([entry]) => {
    inView = entry.isIntersecting;
    loop();
  });
  function dispose() {
    if (disposed) return;
    disposed = true;
    renderer.setAnimationLoop(null);
    observer.disconnect();
    viewport.disconnect();
    document.removeEventListener("visibilitychange", visibility);
    canvas.removeEventListener("pointermove", pointer);
    canvas.removeEventListener("pointerleave", leave);
    canvas.removeEventListener("webglcontextlost", lost);
    freeScene(scene);
    renderer.dispose();
    renderer.forceContextLoss();
  }
  document.addEventListener("visibilitychange", visibility);
  canvas.addEventListener("pointermove", pointer);
  canvas.addEventListener("pointerleave", leave);
  canvas.addEventListener("webglcontextlost", lost);
  observer.observe(host);
  viewport.observe(host);
  resize();
  return {
    update(value) {
      state = value;
      loop();
    },
    dispose,
  };
}
