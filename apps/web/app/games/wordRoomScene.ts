import * as THREE from "three";
import type { WordScene } from "@contextspeak/contracts";
import type { StudioActivity } from "./studioThemes";
import { buildWordRoom } from "./wordRoomModels";
import { freeScene } from "./sceneResources";

export type WordRoomState = {
  moving: boolean;
  activity: StudioActivity;
  score: number | null | undefined;
};
export type WordRoom = { update(state: WordRoomState): void; dispose(): void };
export function createWordRoom(
  host: HTMLElement,
  canvas: HTMLCanvasElement,
  description: WordScene,
  failed: () => void,
): WordRoom {
  const renderer = new THREE.WebGLRenderer({
    canvas,
    antialias: true,
    alpha: false,
  });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 1.5));
  renderer.setClearColor("#243b43");
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.05;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  const scene = new THREE.Scene();
  const art = buildWordRoom(description);
  scene.add(art.group);
  const camera = new THREE.PerspectiveCamera(43, 1, 0.1, 40);
  const base = new THREE.Vector3(6, 4.8, 7.6);
  camera.position.copy(base);
  const target = new THREE.Vector3(-0.2, 1.35, -0.4);
  camera.lookAt(target);
  scene.add(new THREE.HemisphereLight("#fff4df", "#4e6a72", 2.5));
  const sunlight = new THREE.DirectionalLight("#ffe3b4", 4);
  sunlight.position.set(1.5, 6, 4);
  sunlight.castShadow = true;
  sunlight.shadow.mapSize.set(1024, 1024);
  sunlight.shadow.camera.left = -7;
  sunlight.shadow.camera.right = 7;
  sunlight.shadow.camera.top = 6;
  sunlight.shadow.camera.bottom = -6;
  sunlight.shadow.normalBias = 0.035;
  scene.add(sunlight);
  const glow = new THREE.PointLight("#c6b5ff", 0.8, 5);
  art.focus.getWorldPosition(glow.position);
  glow.position.y += 1.3;
  glow.position.z += 1;
  scene.add(glow);
  let state: WordRoomState = {
    moving: false,
    activity: "ready",
    score: undefined,
  };
  let visible = !document.hidden,
    inView = true,
    disposed = false,
    tilt = 0;
  function draw(time: number) {
    if (disposed) return;
    const pulse =
      state.moving && state.activity === "listening"
        ? Math.sin(time * 0.004) * 0.3
        : 0;
    glow.color.set(
      state.score === 100
        ? "#bcf4cc"
        : state.score === 0
          ? "#f5b5b0"
          : "#c6b5ff",
    );
    glow.intensity = 0.8 + pulse;
    camera.position.x = base.x + (state.moving ? tilt : 0);
    camera.lookAt(target);
    renderer.render(scene, camera);
  }
  function loop() {
    renderer.setAnimationLoop(
      state.moving && visible && inView && !disposed ? draw : null,
    );
    if (visible && inView) draw(performance.now());
  }
  function resize() {
    const width = Math.max(1, host.clientWidth),
      height = Math.max(1, host.clientHeight);
    renderer.setSize(width, height, false);
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
    draw(performance.now());
  }
  function pointer(event: PointerEvent) {
    if (!state.moving || event.pointerType === "touch") return;
    const bounds = host.getBoundingClientRect();
    tilt = ((event.clientX - bounds.left) / bounds.width - 0.5) * 0.6;
    draw(performance.now());
  }
  function leave() {
    tilt = 0;
    loop();
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
  const resizeObserver = new ResizeObserver(resize);
  const viewport = new IntersectionObserver(([entry]) => {
    inView = entry.isIntersecting;
    loop();
  });
  function dispose() {
    if (disposed) return;
    disposed = true;
    renderer.setAnimationLoop(null);
    resizeObserver.disconnect();
    viewport.disconnect();
    document.removeEventListener("visibilitychange", visibility);
    host.removeEventListener("pointermove", pointer);
    host.removeEventListener("pointerleave", leave);
    canvas.removeEventListener("webglcontextlost", lost);
    sunlight.shadow.dispose();
    freeScene(scene);
    renderer.dispose();
    renderer.forceContextLoss();
  }
  document.addEventListener("visibilitychange", visibility);
  host.addEventListener("pointermove", pointer);
  host.addEventListener("pointerleave", leave);
  canvas.addEventListener("webglcontextlost", lost);
  resizeObserver.observe(host);
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
