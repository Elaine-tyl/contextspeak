import * as THREE from "three";
import { RoundedBoxGeometry } from "three/addons/geometries/RoundedBoxGeometry.js";
import type { WordScene } from "@contextspeak/contracts";

type Position = [number, number, number];
function mesh(
  parent: THREE.Group,
  geometry: THREE.BufferGeometry,
  color: string,
  position: Position,
) {
  const object = new THREE.Mesh(
    geometry,
    new THREE.MeshStandardMaterial({ color, roughness: 0.78 }),
  );
  object.position.set(...position);
  object.castShadow = true;
  object.receiveShadow = true;
  parent.add(object);
  return object;
}
function box(
  parent: THREE.Group,
  size: Position,
  color: string,
  position: Position,
) {
  return mesh(
    parent,
    new RoundedBoxGeometry(...size, 1, Math.min(...size) * 0.12),
    color,
    position,
  );
}
function group(parent: THREE.Group, position: Position) {
  const object = new THREE.Group();
  object.position.set(...position);
  parent.add(object);
  return object;
}
function cabinet(
  parent: THREE.Group,
  position: Position,
  width = 1.5,
  height = 2.5,
) {
  const object = group(parent, position);
  box(object, [width, height, 0.65], "#b08c70", [0, height / 2, 0]);
  for (const side of [-1, 1]) {
    box(object, [width / 2 - 0.07, height - 0.14, 0.08], "#e6ded0", [
      (side * width) / 4,
      height / 2,
      0.36,
    ]);
    box(object, [0.035, 0.3, 0.05], "#806443", [
      side * 0.075,
      height / 2,
      0.43,
    ]);
  }
  return object;
}
function paper(parent: THREE.Group, position: Position) {
  const object = group(parent, position);
  box(object, [0.65, 0.025, 0.4], "#fff8e6", [0, 0, 0]);
  for (let line = 0; line < 4; line++)
    box(object, [0.42 - line * 0.045, 0.008, 0.015], "#7e8c85", [
      -line * 0.02,
      0.018,
      -0.1 + line * 0.065,
    ]);
  object.rotation.y = -0.2;
  return object;
}
function plant(parent: THREE.Group, position: Position) {
  const object = group(parent, position);
  mesh(
    object,
    new THREE.CylinderGeometry(0.23, 0.16, 0.42, 12),
    "#cf9b72",
    [0, 0.21, 0],
  );
  for (let i = 0; i < 5; i++) {
    const leaf = mesh(
      object,
      new THREE.SphereGeometry(0.15, 10, 8),
      i % 2 ? "#638967" : "#8ca76e",
      [
        Math.sin(i * 1.25) * 0.17,
        0.67 + Math.cos(i) * 0.12,
        Math.cos(i * 1.25) * 0.17,
      ],
    );
    leaf.scale.set(0.55, 2.3, 0.8);
    leaf.rotation.z = Math.sin(i * 1.25) * 0.55;
  }
}
function table(parent: THREE.Group, position: Position) {
  const object = group(parent, position);
  box(object, [1.6, 0.12, 1.15], "#cba47d", [0, 0.9, 0]);
  for (const x of [-0.6, 0.6])
    for (const z of [-0.4, 0.4])
      box(object, [0.09, 0.9, 0.09], "#765c46", [x, 0.45, z]);
  return object;
}

/** Original procedural furniture; no third-party art assets are bundled. */
export function buildWordRoom(scene: WordScene) {
  const root = new THREE.Group();
  const objects: Partial<Record<WordScene["focus"], THREE.Group>> = {};
  const walls = {
    bedroom: "#548183",
    kitchen: "#769281",
    cafe: "#987a68",
    market: "#718b71",
    apartment: "#6e858b",
    restaurant: "#a28278",
    airport: "#6c849b",
  };
  box(root, [8, 0.14, 5.6], "#c29b78", [0, -0.08, 0]);
  box(root, [8, 3.5, 0.15], walls[scene.room], [0, 1.7, -2.8]);
  box(root, [0.15, 3.5, 5.6], walls[scene.room], [-4, 1.7, 0]);
  for (let i = 0; i < 8; i++)
    box(root, [0.018, 0.01, 5.55], "#a57e5f", [-3.5 + i, 0, 0]);
  box(root, [7.9, 0.13, 0.08], "#e3d5bd", [0, 0.08, -2.65]);
  const window = group(root, [2.15, 2, -2.66]);
  objects.window = window;
  box(window, [1.65, 1.65, 0.08], "#f3e8d3", [0, 0, 0]);
  box(window, [1.45, 1.45, 0.1], "#b5dce1", [0, 0, 0.06]);
  box(window, [0.06, 1.5, 0.06], "#fff7e4", [0, 0, 0.15]);
  box(window, [1.5, 0.06, 0.06], "#fff7e4", [0, 0, 0.15]);
  box(window, [1.85, 0.08, 0.38], "#e8dac0", [0, -0.85, 0.1]);
  plant(root, [3.15, 0, -1.8]);
  const lamp = group(root, [-2.7, 3.1, -0.2]);
  mesh(
    lamp,
    new THREE.CylinderGeometry(0.22, 0.4, 0.3, 16),
    "#efd0a0",
    [0, 0, 0],
  );
  box(lamp, [0.025, 0.5, 0.025], "#765d48", [0, 0.35, 0]);

  if (scene.room === "bedroom") {
    objects.wardrobe = cabinet(root, [-2.35, 0, -2.15]);
    const bed = group(root, [0.45, 0, -0.45]);
    box(bed, [1.85, 0.35, 2.4], "#9f795f", [0, 0.3, 0]);
    box(bed, [1.75, 0.22, 2.3], "#f5eddf", [0, 0.58, 0]);
    objects.blanket = group(bed, [0, 0.74, 0.25]);
    box(objects.blanket, [1.78, 0.08, 1.55], "#b69ac3", [0, 0, 0]);
    box(bed, [1.1, 0.15, 0.48], "#fff8eb", [0, 0.78, -0.78]);
    const mirror = group(root, [-0.3, 1.8, -2.55]);
    objects.mirror = mirror;
    mesh(
      mirror,
      new THREE.TorusGeometry(0.5, 0.055, 8, 40),
      "#d5bc87",
      [0, 0, 0],
    );
    mesh(mirror, new THREE.CircleGeometry(0.48, 40), "#c6e5e5", [0, 0, 0.01]);
    table(root, [2.6, 0, 0.5]);
  } else if (["kitchen", "cafe", "market"].includes(scene.room)) {
    const counter = group(root, [-0.8, 0, -1.65]);
    objects.counter = counter;
    box(
      counter,
      [4.8, 1.15, 0.95],
      scene.room === "cafe" ? "#99745a" : "#dfd4bf",
      [0, 0.58, 0],
    );
    box(counter, [5, 0.12, 1.08], "#cba77e", [0, 1.2, 0]);
    objects.receipt = paper(counter, [1.65, 1.28, 0]);
    if (scene.room === "kitchen") {
      objects.cupboard = cabinet(root, [-2.5, 1.8, -2.3], 1.7, 1.05);
      const kettle = group(counter, [-0.45, 1.3, 0]);
      objects.kettle = kettle;
      mesh(
        kettle,
        new THREE.SphereGeometry(0.24, 16, 12),
        "#b6cec6",
        [0, 0.25, 0],
      );
      mesh(
        kettle,
        new THREE.CylinderGeometry(0.12, 0.16, 0.08, 12),
        "#766459",
        [0, 0.47, 0],
      );
      mesh(
        kettle,
        new THREE.TorusGeometry(0.22, 0.045, 6, 20),
        "#74665c",
        [0.18, 0.28, 0],
      );
      const spout = mesh(
        kettle,
        new THREE.CylinderGeometry(0.045, 0.09, 0.25, 10),
        "#b6cec6",
        [-0.23, 0.32, 0],
      );
      spout.rotation.z = -0.9;
      const toaster = group(counter, [0.65, 1.3, 0]);
      objects.toaster = toaster;
      box(toaster, [0.65, 0.38, 0.4], "#c7a388", [0, 0.2, 0]);
      for (const z of [-0.1, 0.1])
        box(toaster, [0.43, 0.015, 0.045], "#564e48", [0, 0.4, z]);
      objects.fan = group(root, [0.2, 2.35, -2.55]);
      mesh(
        objects.fan,
        new THREE.TorusGeometry(0.3, 0.04, 8, 28),
        "#dfdcd0",
        [0, 0, 0],
      );
      for (let i = 0; i < 3; i++) {
        const blade = box(objects.fan, [0.12, 0.38, 0.04], "#b6c8bc", [
          Math.sin(i * 2.1) * 0.1,
          Math.cos(i * 2.1) * 0.1,
          0,
        ]);
        blade.rotation.z = -i * 2.1;
      }
    } else if (scene.room === "cafe") {
      box(root, [1.9, 1.15, 0.08], "#334e49", [-1.3, 2.3, -2.65]);
      for (let i = 0; i < 4; i++)
        box(root, [1.1 - i * 0.1, 0.035, 0.02], "#d8c9a0", [
          -1.45,
          2.6 - i * 0.2,
          -2.58,
        ]);
      const pastry = group(counter, [-0.4, 1.3, 0]);
      objects.pastry = pastry;
      mesh(
        pastry,
        new THREE.CylinderGeometry(0.43, 0.43, 0.04, 24),
        "#eae4d6",
        [0, 0, 0],
      );
      for (const x of [-0.18, 0.18]) {
        const croissant = mesh(
          pastry,
          new THREE.TorusGeometry(0.13, 0.07, 8, 16, Math.PI * 1.5),
          "#dba361",
          [x, 0.1, 0],
        );
        croissant.rotation.x = Math.PI / 2;
      }
      box(counter, [0.8, 0.8, 0.55], "#7a8780", [-1.8, 1.65, -0.05]);
      box(counter, [0.65, 0.45, 0.14], "#3f4f4a", [-1.8, 1.65, 0.3]);
      table(root, [2, 0, 1.1]);
    } else {
      const basket = group(counter, [-0.8, 1.3, 0]);
      objects.basket = basket;
      box(basket, [1, 0.1, 0.65], "#ad8755", [0, 0, 0]);
      for (const x of [-0.5, 0.5])
        box(basket, [0.08, 0.4, 0.65], "#b39462", [x, 0.2, 0]);
      for (const z of [-0.3, 0.3])
        box(basket, [1, 0.4, 0.07], "#c2a271", [0, 0.2, z]);
      const produce = group(basket, [0, 0.25, 0]);
      objects.produce = produce;
      for (let i = 0; i < 9; i++)
        mesh(
          produce,
          new THREE.SphereGeometry(0.13, 10, 8),
          i % 2 ? "#bd6660" : "#92a263",
          [
            ((i % 3) - 1) * 0.25,
            0.07 + Math.floor(i / 6) * 0.08,
            (Math.floor(i / 3) - 1) * 0.18,
          ],
        );
      const scales = group(counter, [0.9, 1.3, 0]);
      objects.scales = scales;
      box(scales, [0.65, 0.17, 0.5], "#819c98", [0, 0.1, 0]);
      mesh(
        scales,
        new THREE.CylinderGeometry(0.33, 0.23, 0.1, 20),
        "#c6cebd",
        [0, 0.25, 0],
      );
    }
  } else if (scene.room === "apartment" || scene.room === "restaurant") {
    const desk = table(root, [-1.2, 0, -1]);
    objects.document = paper(desk, [0, 1, -0.1]);
    objects.receipt = objects.document;
    const sofa = group(root, [1.4, 0, 0.1]);
    box(sofa, [2.1, 0.4, 1], "#b1b595", [0, 0.45, 0]);
    box(sofa, [2.1, 0.8, 0.22], "#a4aa8c", [0, 0.95, -0.4]);
    for (const x of [-1, 1])
      box(sofa, [0.22, 0.65, 1.1], "#9da583", [x, 0.73, 0]);
    for (const x of [-0.5, 0.5])
      box(sofa, [0.85, 0.13, 0.85], "#d5cfad", [x, 0.7, 0.1]);
    const menu = group(desk, [0.55, 1.27, -0.2]);
    objects.menu = menu;
    box(menu, [0.4, 0.55, 0.03], "#e5ceaa", [0, 0, 0]);
    mesh(
      desk,
      new THREE.CylinderGeometry(0.3, 0.3, 0.04, 24),
      "#f3e7cd",
      [-0.45, 1, 0.1],
    );
  } else {
    const counter = group(root, [-1, 0, -1.6]);
    objects.counter = counter;
    box(counter, [4.8, 1.15, 0.9], "#b9c8c8", [0, 0.58, 0]);
    box(root, [2.5, 0.95, 0.08], "#334b60", [-1.4, 2.4, -2.65]);
    for (let i = 0; i < 4; i++)
      box(root, [1.8 - i * 0.1, 0.025, 0.02], "#d7e1c7", [
        -1.5,
        2.7 - i * 0.18,
        -2.58,
      ]);
    objects.document = paper(counter, [0.7, 1.2, 0]);
    objects.receipt = objects.document;
    const suitcase = group(root, [1.9, 0, 0.1]);
    objects.suitcase = suitcase;
    box(suitcase, [0.65, 0.95, 0.35], "#bda0ba", [0, 0.6, 0]);
    box(suitcase, [0.08, 0.45, 0.06], "#626567", [-0.18, 1.2, 0]);
    box(suitcase, [0.08, 0.45, 0.06], "#626567", [0.18, 1.2, 0]);
    box(suitcase, [0.45, 0.06, 0.08], "#515a5a", [0, 1.42, 0]);
    for (const x of [-0.24, 0.24])
      mesh(suitcase, new THREE.SphereGeometry(0.08, 10, 8), "#465050", [
        x,
        0.07,
        0,
      ]);
  }
  if (scene.focus === "building") {
    box(root, [1.05, 0.85, 0.75], "#b69c7b", [2.7, 0.425, -0.5]);
    const model = group(root, [2.7, 1.15, -0.5]);
    objects.building = model;
    box(model, [0.6, 0.55, 0.55], "#e1d7b4", [0, 0, 0]);
    const roof = mesh(
      model,
      new THREE.ConeGeometry(0.5, 0.35, 4),
      "#7c9a83",
      [0, 0.44, 0],
    );
    roof.rotation.y = Math.PI / 4;
  }
  return { group: root, focus: objects[scene.focus] ?? objects.window! };
}
