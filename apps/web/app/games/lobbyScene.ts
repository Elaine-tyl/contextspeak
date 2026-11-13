import * as THREE from "three";
import { freeScene } from "./sceneResources";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";
import { buildPortal, type LobbyPortal } from "./lobbyArt";

export type LobbyScene = {
  motion(enabled: boolean): void;
  highlight(index: number | null): void;
  reset(): void;
  dispose(): void;
};
type Callbacks = {
  select(index: number): void;
  hover(index: number | null): void;
  project(index: number, x: number, y: number): void;
  failed(): void;
};

/** All rendering is local; HTML markers share the existing selection callback. */
export function createLobbyScene(
  host: HTMLElement,
  canvas: HTMLCanvasElement,
  modes: LobbyPortal[],
  callbacks: Callbacks,
): LobbyScene {
  const scene = new THREE.Scene();
  scene.background = new THREE.Color("#eaf2eb");
  scene.fog = new THREE.Fog("#eaf2eb", 35, 65);
  const renderer = new THREE.WebGLRenderer({
    canvas,
    antialias: true,
    alpha: false,
  });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 1.5));
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.1;
  const camera = new THREE.OrthographicCamera(-10, 10, 6, -6, 0.1, 100);
  camera.position.set(0, 18, 24);
  const controls = new OrbitControls(camera, canvas);
  controls.target.set(0, 0.3, 0);
  controls.enablePan = controls.enableZoom = false;
  controls.minAzimuthAngle = -0.22;
  controls.maxAzimuthAngle = 0.22;
  controls.minPolarAngle = 0.65;
  controls.maxPolarAngle = 1.05;
  // A touch drag should scroll the page, rather than trap the phone in the canvas.
  controls.enabled = matchMedia("(pointer: fine)").matches;
  canvas.style.touchAction = "pan-y";
  controls.update();

  scene.add(new THREE.HemisphereLight("#fff8e6", "#72947d", 2.6));
  const sun = new THREE.DirectionalLight("#fff3d8", 2.7);
  sun.position.set(-8, 16, 12);
  sun.castShadow = true;
  sun.shadow.mapSize.set(1024, 1024);
  Object.assign(sun.shadow.camera, {
    left: -15,
    right: 15,
    top: 15,
    bottom: -15,
  });
  sun.shadow.normalBias = 0.04;
  scene.add(sun);
  const ground = new THREE.Mesh(
    new THREE.PlaneGeometry(80, 80),
    new THREE.MeshStandardMaterial({ color: "#dce9df", roughness: 1 }),
  );
  ground.rotation.x = -Math.PI / 2;
  ground.position.y = -1.05;
  ground.receiveShadow = true;
  scene.add(ground);

  const islands = modes.map((mode, index) => {
    const island = buildPortal(mode, index);
    scene.add(island.group);
    return island;
  });
  const ray = new THREE.Raycaster();
  const pointer = new THREE.Vector2();
  const anchor = new THREE.Vector3();
  let width = 1,
    height = 1,
    hovered: number | null = null;
  let animated = true,
    visible = !document.hidden,
    intersecting = true,
    disposed = false;
  let down: { x: number; y: number; pointerId: number } | null = null;

  function render(time: number) {
    if (disposed) return;
    islands.forEach((island, index) => {
      island.prop.position.y = animated
        ? Math.sin(time * 0.0008 + index) * 0.07
        : 0;
      island.group.scale.setScalar(hovered === index ? 1.045 : 1);
    });
    renderer.render(scene, camera);
    islands.forEach((island, index) => {
      anchor.copy(island.group.position);
      anchor.y += 0.35;
      anchor.project(camera);
      callbacks.project(
        index,
        (anchor.x * 0.5 + 0.5) * width,
        (-anchor.y * 0.5 + 0.5) * height + (width < 720 ? 32 : 40),
      );
    });
  }
  function loop() {
    renderer.setAnimationLoop(
      animated && visible && intersecting && !disposed ? render : null,
    );
    if (visible && intersecting) render(performance.now());
  }
  function resize() {
    width = Math.max(1, host.clientWidth);
    height = Math.max(1, host.clientHeight);
    const columns = width < 720 ? 2 : 4;
    if (host.dataset.columns !== String(columns))
      host.dataset.columns = String(columns);
    const rows = Math.ceil(modes.length / columns);
    const spacing = columns === 2 ? 7.6 : 5.6;
    islands.forEach((island, index) =>
      island.group.position.set(
        ((index % columns) - (columns - 1) / 2) * 4.1,
        0,
        (Math.floor(index / columns) - (rows - 1) / 2) * spacing,
      ),
    );
    const aspect = width / height;
    const halfHeight = Math.max(
      ((rows - 1) * spacing * 0.64 + 4.6) / 2,
      (columns * 4.1 + 1.6) / (2 * aspect),
    );
    camera.left = -halfHeight * aspect;
    camera.right = halfHeight * aspect;
    camera.top = halfHeight;
    camera.bottom = -halfHeight;
    camera.updateProjectionMatrix();
    renderer.setSize(width, height, false);
    render(performance.now());
  }
  function highlight(index: number | null) {
    if (hovered === index) return;
    hovered = index;
    canvas.style.cursor = index === null ? "grab" : "pointer";
    callbacks.hover(index);
    render(performance.now());
  }
  function hit(event: PointerEvent) {
    const bounds = canvas.getBoundingClientRect();
    pointer.set(
      ((event.clientX - bounds.left) / width) * 2 - 1,
      (-(event.clientY - bounds.top) / height) * 2 + 1,
    );
    ray.setFromCamera(pointer, camera);
    const object = ray.intersectObjects(
      islands.map((island) => island.group),
      true,
    )[0]?.object;
    let parent: THREE.Object3D | undefined = object;
    while (parent && !parent.userData.kind) parent = parent.parent ?? undefined;
    return parent
      ? modes.findIndex((mode) => mode.kind === parent!.userData.kind)
      : null;
  }
  function move(event: PointerEvent) {
    if (!event.buttons) highlight(hit(event));
  }
  function leave() {
    down = null;
    highlight(null);
  }
  function press(event: PointerEvent) {
    down =
      event.button === 0 && event.isPrimary
        ? { x: event.clientX, y: event.clientY, pointerId: event.pointerId }
        : null;
  }
  function release(event: PointerEvent) {
    const start = down;
    down = null;
    if (
      !start ||
      event.button !== 0 ||
      !event.isPrimary ||
      event.pointerId !== start.pointerId ||
      Math.hypot(event.clientX - start.x, event.clientY - start.y) > 6
    ) {
      return;
    }
    const index = hit(event);
    if (index !== null && index >= 0) callbacks.select(index);
  }
  function cancel() {
    down = null;
  }
  function visibility() {
    visible = !document.hidden;
    loop();
  }
  function changed() {
    render(performance.now());
  }
  function lost(event: Event) {
    event.preventDefault();
    dispose();
    callbacks.failed();
  }
  const observer = new ResizeObserver(resize);
  const viewport = new IntersectionObserver(([entry]) => {
    intersecting = entry.isIntersecting;
    loop();
  });
  function dispose() {
    if (disposed) return;
    disposed = true;
    renderer.setAnimationLoop(null);
    observer.disconnect();
    viewport.disconnect();
    document.removeEventListener("visibilitychange", visibility);
    canvas.removeEventListener("pointermove", move);
    canvas.removeEventListener("pointerleave", leave);
    canvas.removeEventListener("pointerdown", press);
    canvas.removeEventListener("pointerup", release);
    canvas.removeEventListener("pointercancel", cancel);
    canvas.removeEventListener("webglcontextlost", lost);
    controls.removeEventListener("change", changed);
    controls.dispose();
    freeScene(scene);
    renderer.dispose();
    renderer.forceContextLoss();
  }
  controls.addEventListener("change", changed);
  document.addEventListener("visibilitychange", visibility);
  canvas.addEventListener("pointermove", move);
  canvas.addEventListener("pointerleave", leave);
  canvas.addEventListener("pointerdown", press);
  canvas.addEventListener("pointerup", release);
  canvas.addEventListener("pointercancel", cancel);
  canvas.addEventListener("webglcontextlost", lost);
  observer.observe(host);
  viewport.observe(host);
  resize();
  controls.saveState();
  loop();
  return {
    motion(enabled) {
      animated = enabled;
      loop();
    },
    highlight,
    reset() {
      highlight(null);
      controls.reset();
      render(performance.now());
    },
    dispose,
  };
}
