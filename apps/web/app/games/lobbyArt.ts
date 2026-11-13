import * as THREE from "three";
import type { GameKind } from "@contextspeak/contracts";

export type LobbyPortal = { kind: GameKind; label: string; level: number };
const colors = ["#3b9174", "#5078d5", "#e6a551", "#9781cc"];

function add(
  parent: THREE.Group,
  geometry: THREE.BufferGeometry,
  color: string,
  x: number,
  y: number,
  z: number,
) {
  const mesh = new THREE.Mesh(
    geometry,
    new THREE.MeshStandardMaterial({ color, roughness: 0.72 }),
  );
  mesh.position.set(x, y, z);
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  parent.add(mesh);
  return mesh;
}

function letter(
  parent: THREE.Group,
  text: string,
  color: string,
  x: number,
  y: number,
  z: number,
) {
  const canvas = document.createElement("canvas");
  canvas.width = canvas.height = 128;
  const context = canvas.getContext("2d")!;
  context.fillStyle = color;
  context.fillRect(0, 0, 128, 128);
  context.fillStyle = "#ffffff";
  context.font = "bold 80px system-ui";
  context.textAlign = "center";
  context.textBaseline = "middle";
  context.fillText(text, 64, 68);
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  const cube = add(
    parent,
    new THREE.BoxGeometry(0.55, 0.55, 0.55),
    color,
    x,
    y,
    z,
  );
  const face = new THREE.Mesh(
    new THREE.PlaneGeometry(0.54, 0.54),
    new THREE.MeshBasicMaterial({ map: texture }),
  );
  face.position.z = 0.281;
  cube.add(face);
  return cube;
}

/** Original toy-like props for each practice mode; no downloaded art or answer text. */
export function buildPortal(portal: LobbyPortal, index: number) {
  const group = new THREE.Group();
  group.userData.kind = portal.kind;
  const color = colors[index % colors.length];
  add(
    group,
    new THREE.CylinderGeometry(1.45, 1.12, 0.55, 8),
    "#c4b49a",
    0,
    0,
    0,
  );
  add(
    group,
    new THREE.CylinderGeometry(1.5, 1.45, 0.18, 8),
    "#a9cda5",
    0,
    0.36,
    0,
  );
  const ring = add(
    group,
    new THREE.TorusGeometry(1.38, 0.045, 6, 40),
    color,
    0,
    0.48,
    0,
  );
  ring.rotation.x = Math.PI / 2;
  const prop = new THREE.Group();
  group.add(prop);
  switch (portal.kind) {
    case "scene-words": {
      add(prop, new THREE.BoxGeometry(1.1, 0.9, 0.85), "#eee0c3", 0, 1.02, 0);
      const roof = add(
        prop,
        new THREE.ConeGeometry(0.95, 0.55, 4),
        color,
        0,
        1.73,
        0,
      );
      roof.rotation.y = Math.PI / 4;
      add(
        prop,
        new THREE.BoxGeometry(0.3, 0.55, 0.04),
        "#92765c",
        -0.2,
        0.86,
        0.45,
      );
      add(
        prop,
        new THREE.BoxGeometry(0.3, 0.3, 0.04),
        "#a6d6de",
        0.28,
        1.15,
        0.45,
      );
      break;
    }
    case "dictation":
    case "comprehension":
    case "reading-question":
    case "reading-order":
    case "evidence-hunt":
    case "sentence-puzzle": {
      const book = add(
        prop,
        new THREE.BoxGeometry(1.15, 0.2, 0.95),
        color,
        0,
        0.65,
        0,
      );
      book.rotation.y = -0.18;
      add(prop, new THREE.BoxGeometry(1.07, 0.13, 0.88), "#fffcf0", 0, 0.8, 0);
      const pencil = add(
        prop,
        new THREE.CylinderGeometry(0.08, 0.08, 1.35, 6),
        "#edb654",
        0.1,
        1.28,
        0.1,
      );
      pencil.rotation.z = -0.55;
      break;
    }
    case "picture": {
      add(
        prop,
        new THREE.BoxGeometry(1.55, 1.12, 0.14),
        "#fffaf1",
        0,
        1.08,
        -0.12,
      );
      add(prop, new THREE.BoxGeometry(1.3, 0.9, 0.08), "#a6d4dc", 0, 1.1, 0);
      add(prop, new THREE.ConeGeometry(0.4, 0.7, 4), color, -0.25, 1, 0.2);
      add(
        prop,
        new THREE.SphereGeometry(0.17, 12, 8),
        "#ffd977",
        0.4,
        1.36,
        0.12,
      );
      break;
    }
    case "response":
    case "minimal-pairs": {
      add(
        prop,
        new THREE.CylinderGeometry(0.1, 0.1, 0.8, 12),
        "#ddd8c9",
        0,
        0.95,
        0,
      );
      add(prop, new THREE.SphereGeometry(0.36, 16, 12), color, 0, 1.45, 0);
      add(
        prop,
        new THREE.CylinderGeometry(0.46, 0.5, 0.15, 16),
        "#faf1d8",
        0,
        0.57,
        0,
      );
      const sound = add(
        prop,
        new THREE.TorusGeometry(0.58, 0.045, 6, 32, Math.PI),
        "#efbd61",
        0.05,
        1.4,
        0,
      );
      sound.rotation.z = -Math.PI / 2;
      break;
    }
    case "pk": {
      add(
        prop,
        new THREE.CylinderGeometry(0.5, 0.22, 0.7, 12),
        "#ecc15e",
        0,
        1.2,
        0,
      );
      add(
        prop,
        new THREE.CylinderGeometry(0.1, 0.1, 0.35, 12),
        "#d5953a",
        0,
        0.76,
        0,
      );
      add(prop, new THREE.BoxGeometry(0.85, 0.16, 0.7), "#f7e8c8", 0, 0.54, 0);
      break;
    }
    default: {
      const letters =
        portal.kind === "true-false"
          ? ["✓", "×"]
          : portal.kind === "riddle"
            ? ["?"]
            : portal.kind === "word-context"
              ? ["?", "A"]
              : ["A", "B", "C"];
      letters.forEach((glyph, i) => {
        const block = letter(
          prop,
          glyph,
          colors[(index + i) % colors.length],
          (i - (letters.length - 1) / 2) * 0.62,
          0.86 + (i % 2) * 0.16,
          0,
        );
        block.rotation.y = (i - 1) * 0.12;
      });
    }
  }
  // Ten small lights represent the existing unlocked level, not a new score.
  for (let step = 0; step < 10; step++) {
    const angle = Math.PI * (0.08 + step / 11);
    add(
      group,
      new THREE.SphereGeometry(0.055, 6, 4),
      step < portal.level ? "#fff0a5" : "#79937f",
      Math.cos(angle) * 1.15,
      0.53,
      Math.sin(angle) * 1.15,
    );
  }
  add(
    group,
    new THREE.CylinderGeometry(0.07, 0.1, 0.5, 6),
    "#946c47",
    -0.95,
    0.7,
    -0.65,
  );
  add(
    group,
    new THREE.ConeGeometry(0.32, 0.8, 7),
    "#629967",
    -0.95,
    1.23,
    -0.65,
  );
  return { group, prop, ring };
}
