/**
 * The shared picture library for listening games. Each picture is a small,
 * original SVG (viewBox 0 0 200 120). The website renders it as an image and the
 * Godot arcade loads the same SVG at runtime (games/arcade/pictures.json is
 * generated from this file by `pnpm arcade:pictures`).
 */
const INK = "#243d35";
const C = {
  red: "#e8504f",
  yellow: "#f2b544",
  green: "#3f9a63",
  leaf: "#8fcf8a",
  blue: "#2f55c4",
  sky: "#8fc3e6",
  orange: "#e8892b",
  brown: "#a86b3c",
  tan: "#d9a066",
  pink: "#f49ac1",
  grey: "#9aa5ad",
  light: "#e6ebee",
  white: "#ffffff",
  cream: "#fff4dc",
  purple: "#8e6cd8",
};
const backgrounds = {
  everyday: "#eef4fb",
  food: "#fdf0e1",
  home: "#f4efe8",
  travel: "#e8eefc",
  animals: "#eef6ef",
  clothes: "#f6eef8",
};
type Theme = keyof typeof backgrounds;

function svg(theme: Theme, body: string) {
  return (
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 120">` +
    `<rect x="4" y="4" width="192" height="112" rx="18" fill="${backgrounds[theme]}"/>` +
    `<g stroke="${INK}" stroke-width="3.5" stroke-linejoin="round" stroke-linecap="round">${body}</g></svg>`
  );
}

const art = {
  // ---------------------------------------------------------------- everyday
  umbrella: [
    "Umbrella",
    svg(
      "everyday",
      `<path d="M44 62a56 44 0 0 1 112 0q-14-10-28 0q-14-10-28 0q-14-10-28 0q-14-10-28 0z" fill="${C.red}"/><path d="M100 18v68q0 14 12 14t12-10" fill="none"/><path d="M86 20l-8 42M114 20l8 42" fill="none" stroke="${C.white}" stroke-width="3"/><g stroke="${C.sky}" stroke-width="3"><path d="M30 24l-4 9M168 30l-4 9M40 84l-4 9M164 76l-4 9"/></g>`,
    ),
  ],
  book: [
    "Book",
    svg(
      "everyday",
      `<path d="M40 30q30-8 58 4v62q-28-12-58-4z" fill="${C.white}"/><path d="M160 30q-30-8-58 4v62q28-12 58-4z" fill="${C.white}"/><path d="M36 92q32-8 64 6q32-14 64-6v6q-32-8-64 6q-32-14-64-6z" fill="${C.blue}"/><g stroke-width="2.5" opacity=".55"><path d="M50 46q18-4 38 2M50 58q18-4 38 2M50 70q18-4 38 2M112 48q18-6 38-2M112 60q18-6 38-2M112 72q18-6 38-2"/></g><path d="M134 32v22l6-6 6 6V31" fill="${C.orange}"/>`,
    ),
  ],
  bridge: [
    "Bridge",
    svg(
      "everyday",
      `<rect x="8" y="78" width="184" height="34" rx="10" fill="${C.sky}" stroke="none"/><path d="M14 72h172v10H14z" fill="${C.red}"/><path d="M58 22v56M142 22v56" stroke-width="9" stroke="${C.red}"/><path d="M58 22v56M142 22v56" fill="none" stroke-width="2" opacity=".35"/><path d="M14 66q44-4 44-44q0 34 42 42q42-8 42-42q0 40 44 44" fill="none"/><g stroke-width="2" opacity=".6"><path d="M80 52v20M100 60v12M120 52v20M36 60v12M164 60v12"/></g><path d="M30 96h16M90 100h20M150 94h16" stroke="${C.white}" stroke-width="3"/>`,
    ),
  ],
  bicycle: [
    "Bicycle",
    svg(
      "everyday",
      `<circle cx="58" cy="76" r="24" fill="${C.white}"/><circle cx="142" cy="76" r="24" fill="${C.white}"/><circle cx="58" cy="76" r="4" fill="${INK}"/><circle cx="142" cy="76" r="4" fill="${INK}"/><path d="M58 76l26-36h40l-26 36zM84 40l14 36M124 40l18 36" fill="none" stroke="${C.green}" stroke-width="6"/><path d="M80 32h16M122 40l-4-14h12" fill="none"/><circle cx="98" cy="76" r="7" fill="${C.yellow}"/>`,
    ),
  ],
  phone: [
    "Phone",
    svg(
      "everyday",
      `<rect x="72" y="14" width="56" height="94" rx="12" fill="${INK}"/><rect x="78" y="24" width="44" height="70" rx="4" fill="${C.sky}" stroke="none"/><rect x="84" y="32" width="14" height="14" rx="3" fill="${C.yellow}" stroke="none"/><rect x="102" y="32" width="14" height="14" rx="3" fill="${C.pink}" stroke="none"/><rect x="84" y="52" width="14" height="14" rx="3" fill="${C.leaf}" stroke="none"/><rect x="102" y="52" width="14" height="14" rx="3" fill="${C.white}" stroke="none"/><circle cx="100" cy="101" r="3" fill="${C.white}" stroke="none"/>`,
    ),
  ],
  // ---------------------------------------------------------------- food
  apple: [
    "Apple",
    svg(
      "food",
      `<path d="M100 38q-26-16-40 8q-12 24 6 46q14 16 34 6q20 10 34-6q18-22 6-46q-14-24-40-8z" fill="${C.red}"/><path d="M100 38q0-14 8-22" fill="none" stroke="${C.brown}" stroke-width="4"/><path d="M106 26q16-10 26 2q-14 10-26-2z" fill="${C.leaf}"/><path d="M74 52q-6 12-2 24" fill="none" stroke="${C.white}" stroke-width="4" opacity=".7"/>`,
    ),
  ],
  bread: [
    "Bread",
    svg(
      "food",
      `<path d="M44 58q0-26 56-26t56 26q0 10-10 12v24q0 6-6 6H60q-6 0-6-6V70q-10-2-10-12z" fill="${C.tan}"/><path d="M60 70h80" fill="none" opacity=".4"/><path d="M78 46l-6 10M100 44v12M122 46l6 10" fill="none" stroke="${C.cream}" stroke-width="4"/>`,
    ),
  ],
  cake: [
    "Cake",
    svg(
      "food",
      `<rect x="52" y="58" width="96" height="42" rx="6" fill="${C.cream}"/><path d="M52 66q12 10 24 0t24 0t24 0t24 0v-8H52z" fill="${C.pink}"/><rect x="66" y="40" width="68" height="20" rx="5" fill="${C.cream}"/><path d="M66 46q11 8 22 0t22 0t24 0v-6H66z" fill="${C.pink}"/><path d="M100 24v16" stroke-width="5" stroke="${C.blue}"/><path d="M100 12q6 6 0 12q-6-6 0-12z" fill="${C.yellow}"/><g fill="${C.red}" stroke="none"><circle cx="70" cy="84" r="4"/><circle cx="100" cy="88" r="4"/><circle cx="130" cy="84" r="4"/></g>`,
    ),
  ],
  coffee: [
    "Coffee",
    svg(
      "food",
      `<path d="M62 46h64l-6 50q-1 8-9 8H77q-8 0-9-8z" fill="${C.white}"/><path d="M126 56h8q12 0 12 12t-12 12h-12" fill="none"/><path d="M66 54h56" fill="none" stroke="${C.brown}" stroke-width="6"/><path d="M50 106h100" fill="none"/><path d="M84 18q-8 8 0 16t0 14M104 18q-8 8 0 16t0 14" fill="none" stroke="${C.grey}" stroke-width="3"/>`,
    ),
  ],
  pear: [
    "Pear",
    svg(
      "food",
      `<path d="M100 26q-14 0-14 20q0 12-12 24q-14 18 0 30q12 10 26 8q14 2 26-8q14-12 0-30q-12-12-12-24q0-20-14-20z" fill="${C.leaf}"/><path d="M100 26q2-10 8-14" fill="none" stroke="${C.brown}" stroke-width="4"/><path d="M84 74q-4 10 2 18" fill="none" stroke="${C.white}" stroke-width="4" opacity=".7"/>`,
    ),
  ],
  orange: [
    "Orange",
    svg(
      "food",
      `<circle cx="100" cy="66" r="38" fill="${C.orange}"/><path d="M100 28q4-8 14-8q-2 10-14 8z" fill="${C.leaf}"/><g fill="${INK}" stroke="none" opacity=".25"><circle cx="86" cy="56" r="2"/><circle cx="110" cy="62" r="2"/><circle cx="96" cy="80" r="2"/><circle cx="118" cy="82" r="2"/><circle cx="80" cy="76" r="2"/></g><path d="M76 50q-6 8-4 18" fill="none" stroke="${C.white}" stroke-width="4" opacity=".7"/>`,
    ),
  ],
  banana: [
    "Banana",
    svg(
      "food",
      `<path d="M50 40q6 50 54 56q36 2 52-24q-26 16-56 6q-30-12-38-42q-6 0-12 4z" fill="${C.yellow}"/><path d="M50 40l-6-6M156 72l6 2" fill="none" stroke-width="5"/><path d="M64 58q12 22 40 28" fill="none" opacity=".35"/>`,
    ),
  ],
  grapes: [
    "Grapes",
    svg(
      "food",
      `<g fill="${C.purple}"><circle cx="86" cy="44" r="11"/><circle cx="108" cy="44" r="11"/><circle cx="130" cy="44" r="11"/><circle cx="97" cy="62" r="11"/><circle cx="119" cy="62" r="11"/><circle cx="108" cy="80" r="11"/><circle cx="86" cy="62" r="11" opacity="0"/><circle cx="108" cy="98" r="10"/></g><path d="M108 33q0-12 8-18" fill="none" stroke="${C.brown}" stroke-width="4"/><path d="M114 22q16-6 24 6q-14 6-24-6z" fill="${C.leaf}"/>`,
    ),
  ],
  lemon: [
    "Lemon",
    svg(
      "food",
      `<path d="M54 66q0-30 46-30t46 30q0 30-46 30t-46-30z" fill="${C.yellow}"/><path d="M54 66l-10-2M146 66l10-2" fill="none" stroke-width="5"/><path d="M74 54q-6 8-4 16" fill="none" stroke="${C.white}" stroke-width="4" opacity=".7"/>`,
    ),
  ],
  cheese: [
    "Cheese",
    svg(
      "food",
      `<path d="M44 84l112-44v54H44z" fill="${C.yellow}"/><path d="M44 84h112" fill="none"/><path d="M44 84l112-44" fill="${C.yellow}"/><g fill="${C.orange}" stroke="none" opacity=".75"><circle cx="76" cy="90" r="5"/><circle cx="112" cy="82" r="7"/><circle cx="140" cy="62" r="5"/><circle cx="132" cy="88" r="4"/></g>`,
    ),
  ],
  // ---------------------------------------------------------------- home
  key: [
    "Key",
    svg(
      "home",
      `<circle cx="62" cy="60" r="24" fill="${C.yellow}"/><circle cx="62" cy="60" r="9" fill="${backgrounds.home}"/><path d="M86 56h70v10h-8v14h-10V66h-8v10h-10V66H86z" fill="${C.yellow}"/>`,
    ),
  ],
  lamp: [
    "Lamp",
    svg(
      "home",
      `<path d="M72 20h56l16 40H56z" fill="${C.yellow}"/><path d="M100 60v36" fill="none" stroke-width="5"/><path d="M74 104q0-10 26-10t26 10z" fill="${C.brown}"/><path d="M86 72l-10 10M114 72l10 10M100 74v12" fill="none" stroke="${C.orange}" stroke-width="3" opacity=".8"/>`,
    ),
  ],
  clock: [
    "Clock",
    svg(
      "home",
      `<circle cx="100" cy="62" r="40" fill="${C.white}"/><circle cx="100" cy="62" r="40" fill="none" stroke-width="7" stroke="${C.blue}"/><path d="M100 62V36M100 62l18 10" fill="none" stroke-width="5"/><circle cx="100" cy="62" r="4" fill="${INK}"/><g stroke-width="3"><path d="M100 28v5M100 91v5M66 62h5M129 62h5"/></g>`,
    ),
  ],
  chair: [
    "Chair",
    svg(
      "home",
      `<rect x="72" y="16" width="56" height="44" rx="8" fill="${C.green}"/><rect x="64" y="60" width="72" height="14" rx="5" fill="${C.green}"/><path d="M70 74v32M130 74v32M80 74v22M120 74v22" fill="none" stroke-width="5" stroke="${C.brown}"/>`,
    ),
  ],
  table: [
    "Table",
    svg(
      "home",
      `<rect x="34" y="46" width="132" height="14" rx="5" fill="${C.tan}"/><path d="M48 60v44M152 60v44" fill="none" stroke-width="7" stroke="${C.brown}"/><path d="M88 46v-10h24v10" fill="${C.red}"/><path d="M100 36q-8-14 0-20q8 6 0 20z" fill="${C.leaf}"/>`,
    ),
  ],
  bed: [
    "Bed",
    svg(
      "home",
      `<path d="M30 40v62M170 70v32" fill="none" stroke-width="7" stroke="${C.brown}"/><rect x="30" y="66" width="140" height="22" rx="6" fill="${C.blue}"/><rect x="38" y="52" width="40" height="18" rx="8" fill="${C.white}"/><path d="M76 66q40-14 94 0" fill="${C.sky}"/>`,
    ),
  ],
  sofa: [
    "Sofa",
    svg(
      "home",
      `<rect x="40" y="36" width="120" height="40" rx="12" fill="${C.orange}"/><rect x="28" y="56" width="24" height="40" rx="10" fill="${C.orange}"/><rect x="148" y="56" width="24" height="40" rx="10" fill="${C.orange}"/><rect x="50" y="66" width="100" height="26" rx="8" fill="${C.cream}"/><path d="M100 66v26" fill="none"/><path d="M40 96v10M160 96v10" fill="none" stroke-width="5"/>`,
    ),
  ],
  door: [
    "Door",
    svg(
      "home",
      `<rect x="66" y="12" width="68" height="98" rx="6" fill="${C.brown}"/><rect x="76" y="24" width="48" height="30" rx="4" fill="${C.tan}"/><rect x="76" y="62" width="48" height="36" rx="4" fill="${C.tan}"/><circle cx="122" cy="62" r="4" fill="${C.yellow}"/>`,
    ),
  ],
  window: [
    "Window",
    svg(
      "home",
      `<rect x="52" y="18" width="96" height="84" rx="6" fill="${C.sky}"/><path d="M100 18v84M52 60h96" fill="none" stroke-width="5"/><circle cx="72" cy="38" r="9" fill="${C.yellow}" stroke="none"/><path d="M44 104h112" fill="none" stroke-width="6" stroke="${C.brown}"/><path d="M114 82q8-10 16 0" fill="none" stroke="${C.white}" stroke-width="3"/>`,
    ),
  ],
  // ---------------------------------------------------------------- travel
  train: [
    "Train",
    svg(
      "travel",
      `<rect x="40" y="28" width="120" height="58" rx="14" fill="${C.red}"/><rect x="52" y="40" width="30" height="20" rx="4" fill="${C.sky}"/><rect x="88" y="40" width="30" height="20" rx="4" fill="${C.sky}"/><rect x="124" y="40" width="24" height="20" rx="4" fill="${C.sky}"/><path d="M40 70h120" fill="none" stroke="${C.white}" stroke-width="4"/><circle cx="66" cy="90" r="9" fill="${INK}"/><circle cx="134" cy="90" r="9" fill="${INK}"/><path d="M24 104h152" fill="none" stroke-width="4"/>`,
    ),
  ],
  bus: [
    "Bus",
    svg(
      "travel",
      `<rect x="34" y="30" width="132" height="56" rx="10" fill="${C.yellow}"/><g fill="${C.sky}"><rect x="44" y="40" width="22" height="18" rx="3"/><rect x="72" y="40" width="22" height="18" rx="3"/><rect x="100" y="40" width="22" height="18" rx="3"/></g><rect x="130" y="40" width="26" height="40" rx="3" fill="${C.sky}"/><circle cx="62" cy="90" r="10" fill="${INK}"/><circle cx="138" cy="90" r="10" fill="${INK}"/><path d="M34 70h96" fill="none" opacity=".4"/>`,
    ),
  ],
  plane: [
    "Plane",
    svg(
      "travel",
      `<path d="M30 66q0-10 16-10h104q24 0 24 10t-24 10H46q-16 0-16-10z" fill="${C.white}"/><path d="M96 58l-24-34h16l34 34zM96 74l-24 30h16l34-30z" fill="${C.blue}"/><path d="M40 58l-10-20h12l16 20z" fill="${C.blue}"/><g fill="${C.sky}" stroke="none"><circle cx="130" cy="66" r="3"/><circle cx="142" cy="66" r="3"/><circle cx="118" cy="66" r="3"/></g>`,
    ),
  ],
  car: [
    "Car",
    svg(
      "travel",
      `<path d="M36 82V66q0-6 6-8l20-4 16-18q4-4 10-4h28q6 0 10 4l16 18h14q8 0 8 8v20z" fill="${C.blue}"/><path d="M82 54l10-14h24l12 14z" fill="${C.sky}"/><circle cx="68" cy="84" r="12" fill="${INK}"/><circle cx="68" cy="84" r="4" fill="${C.grey}" stroke="none"/><circle cx="138" cy="84" r="12" fill="${INK}"/><circle cx="138" cy="84" r="4" fill="${C.grey}" stroke="none"/><path d="M158 66h6" fill="none" stroke="${C.yellow}" stroke-width="4"/>`,
    ),
  ],
  boat: [
    "Boat",
    svg(
      "travel",
      `<rect x="8" y="86" width="184" height="26" rx="10" fill="${C.sky}" stroke="none"/><path d="M44 78h112l-16 18H60z" fill="${C.red}"/><path d="M100 18v60" fill="none" stroke-width="4"/><path d="M100 22l40 50h-40z" fill="${C.white}"/><path d="M96 30l-30 42h30z" fill="${C.yellow}"/><path d="M30 100q10-6 20 0t20 0M130 104q10-6 20 0t20 0" fill="none" stroke="${C.white}" stroke-width="3"/>`,
    ),
  ],
  truck: [
    "Truck",
    svg(
      "travel",
      `<rect x="30" y="34" width="90" height="50" rx="6" fill="${C.green}"/><path d="M120 50h26l16 18v16h-42z" fill="${C.yellow}"/><path d="M126 56h18l10 12h-28z" fill="${C.sky}"/><circle cx="58" cy="88" r="11" fill="${INK}"/><circle cx="140" cy="88" r="11" fill="${INK}"/><path d="M42 48h64" fill="none" stroke="${C.white}" stroke-width="3" opacity=".7"/>`,
    ),
  ],
  helicopter: [
    "Helicopter",
    svg(
      "travel",
      `<path d="M64 62q0-22 34-22h14q30 0 30 22t-30 22H98q-34 0-34-22z" fill="${C.orange}"/><path d="M118 48h14q10 0 10 12h-24z" fill="${C.sky}"/><path d="M64 60H28l-6-10" fill="none" stroke-width="5"/><path d="M104 40V26M50 22h108" fill="none" stroke-width="5"/><path d="M82 84l-6 14M126 84l6 14M64 98h84" fill="none" stroke-width="4"/>`,
    ),
  ],
  scooter: [
    "Scooter",
    svg(
      "travel",
      `<circle cx="56" cy="84" r="14" fill="${C.white}"/><circle cx="148" cy="84" r="14" fill="${C.white}"/><path d="M56 84h70q10 0 14-10l8-50" fill="none" stroke-width="6" stroke="${C.purple}"/><path d="M136 24h24" fill="none" stroke-width="6"/><path d="M60 76h56" fill="none" stroke-width="5" stroke="${C.purple}"/>`,
    ),
  ],
  // ---------------------------------------------------------------- animals
  cat: [
    "Cat",
    svg(
      "animals",
      `<path d="M72 50l-6-30 22 16M128 50l6-30-22 16" fill="${C.orange}"/><ellipse cx="100" cy="60" rx="34" ry="28" fill="${C.orange}"/><ellipse cx="100" cy="102" rx="40" ry="14" fill="${C.orange}"/><circle cx="88" cy="56" r="4" fill="${INK}"/><circle cx="112" cy="56" r="4" fill="${INK}"/><path d="M96 68l4 4 4-4" fill="${C.pink}"/><path d="M80 70H58M80 76l-20 6M120 70h22M120 76l20 6" fill="none" stroke-width="2"/>`,
    ),
  ],
  dog: [
    "Dog",
    svg(
      "animals",
      `<ellipse cx="100" cy="62" rx="32" ry="30" fill="${C.tan}"/><path d="M70 42q-16 10-10 40q12-4 16-22zM130 42q16 10 10 40q-12-4-16-22z" fill="${C.brown}"/><ellipse cx="100" cy="76" rx="16" ry="11" fill="${C.cream}"/><circle cx="88" cy="56" r="4" fill="${INK}"/><circle cx="112" cy="56" r="4" fill="${INK}"/><ellipse cx="100" cy="70" rx="6" ry="4" fill="${INK}"/><path d="M100 82q0 10 8 10" fill="none" stroke="${C.red}" stroke-width="4"/>`,
    ),
  ],
  fish: [
    "Fish",
    svg(
      "animals",
      `<path d="M52 60q30-34 80-6l28-18v48l-28-18q-50 28-80-6z" fill="${C.sky}"/><circle cx="72" cy="56" r="4" fill="${INK}"/><path d="M96 46q8 14 0 28M112 48q6 12 0 24" fill="none" stroke-width="2.5" opacity=".6"/><g fill="${C.white}" stroke-width="2"><circle cx="40" cy="38" r="5"/><circle cx="30" cy="24" r="3"/></g>`,
    ),
  ],
  bird: [
    "Bird",
    svg(
      "animals",
      `<ellipse cx="96" cy="64" rx="36" ry="28" fill="${C.blue}"/><circle cx="124" cy="46" r="16" fill="${C.blue}"/><path d="M138 44l16 4-16 6z" fill="${C.yellow}"/><circle cx="128" cy="42" r="3" fill="${INK}"/><path d="M70 60q16 22 40 6" fill="${C.sky}"/><path d="M60 62l-22-8 8 18z" fill="${C.blue}"/><path d="M90 92v12M104 92v12" fill="none" stroke="${C.orange}"/>`,
    ),
  ],
  rabbit: [
    "Rabbit",
    svg(
      "animals",
      `<ellipse cx="86" cy="26" rx="8" ry="22" fill="${C.white}"/><ellipse cx="112" cy="26" rx="8" ry="22" fill="${C.white}"/><ellipse cx="86" cy="28" rx="3" ry="14" fill="${C.pink}" stroke="none"/><ellipse cx="112" cy="28" rx="3" ry="14" fill="${C.pink}" stroke="none"/><circle cx="99" cy="62" r="24" fill="${C.white}"/><ellipse cx="100" cy="100" rx="34" ry="14" fill="${C.white}"/><circle cx="90" cy="58" r="3.5" fill="${INK}"/><circle cx="108" cy="58" r="3.5" fill="${INK}"/><path d="M96 68l3 3 3-3" fill="${C.pink}"/>`,
    ),
  ],
  duck: [
    "Duck",
    svg(
      "animals",
      `<rect x="8" y="88" width="184" height="24" rx="10" fill="${C.sky}" stroke="none"/><path d="M50 70q0 26 50 26t50-26q-30 6-44-6q-30-4-56 6z" fill="${C.yellow}"/><circle cx="118" cy="46" r="18" fill="${C.yellow}"/><path d="M134 46q16 0 20 6q-10 6-22 2z" fill="${C.orange}"/><circle cx="122" cy="42" r="3" fill="${INK}"/><path d="M74 72q20 10 36-2" fill="none" opacity=".5"/>`,
    ),
  ],
  // ---------------------------------------------------------------- clothes
  shirt: [
    "Shirt",
    svg(
      "clothes",
      `<path d="M76 20l-34 18 12 22 14-6v50h64V54l14 6 12-22-34-18q-6 12-24 12t-24-12z" fill="${C.blue}"/><path d="M100 32v72" fill="none" opacity=".35"/><g fill="${C.white}" stroke="none"><circle cx="100" cy="50" r="3"/><circle cx="100" cy="66" r="3"/><circle cx="100" cy="82" r="3"/></g>`,
    ),
  ],
  hat: [
    "Hat",
    svg(
      "clothes",
      `<path d="M68 76q-2-48 32-48t32 48z" fill="${C.purple}"/><path d="M68 66h64" fill="none" stroke="${C.yellow}" stroke-width="7"/><path d="M36 80q64 18 128 0q-8 12-64 14q-56-2-64-14z" fill="${C.purple}"/>`,
    ),
  ],
  shoe: [
    "Shoe",
    svg(
      "clothes",
      `<path d="M40 52q24 0 34 8q26 14 60 16q26 2 26 16v4H40z" fill="${C.red}"/><path d="M40 92h120v8H40z" fill="${C.white}"/><path d="M78 62l-6 8M90 68l-6 8M102 72l-6 8" fill="none" stroke="${C.white}" stroke-width="3"/>`,
    ),
  ],
  glasses: [
    "Glasses",
    svg(
      "clothes",
      `<circle cx="70" cy="64" r="22" fill="${C.sky}" fill-opacity=".45"/><circle cx="130" cy="64" r="22" fill="${C.sky}" fill-opacity=".45"/><path d="M92 62q8-8 16 0M48 58l-16-12M152 58l16-12" fill="none" stroke-width="5"/><path d="M60 56l8-6M120 56l8-6" fill="none" stroke="${C.white}" stroke-width="3"/>`,
    ),
  ],
  scarf: [
    "Scarf",
    svg(
      "clothes",
      `<path d="M56 34q44 22 88 0v18q-44 22-88 0z" fill="${C.red}"/><path d="M118 52l8 52h-22l-4-46z" fill="${C.red}"/><g stroke="${C.white}" stroke-width="4"><path d="M72 42v14M92 48v14M112 48v14M130 42v12"/></g><path d="M106 104v8M114 104v8M122 104v8" fill="none" stroke-width="3"/>`,
    ),
  ],
  glove: [
    "Glove",
    svg(
      "clothes",
      `<path d="M76 104V54q0-8 7-8t7 8V26q0-7 7-7t7 7v24V20q0-7 7-7t7 7v32V30q0-7 7-7t7 7v44q0 30-28 30z" fill="${C.green}"/><path d="M76 66q-16-14-22-2q8 20 22 28" fill="${C.green}"/><path d="M74 92h52" fill="none" stroke="${C.white}" stroke-width="5"/>`,
    ),
  ],
} as const satisfies Record<string, readonly [string, string]>;

export type PictureId = keyof typeof art;
export const pictureIds = Object.keys(art) as PictureId[];
export const pictureLibrary: Record<PictureId, { label: string; svg: string }> =
  Object.fromEntries(
    Object.entries(art).map(([id, [label, svg]]) => [id, { label, svg }]),
  ) as Record<PictureId, { label: string; svg: string }>;

/** Each level uses its own theme; levels 7–10 are 3 × 3 grids of similar items. */
export const pictureLevels: Record<
  number,
  { theme: string; pictures: PictureId[] }
> = {
  1: {
    theme: "Everyday things",
    pictures: ["umbrella", "book", "bridge", "bicycle"],
  },
  2: { theme: "Food", pictures: ["apple", "bread", "cake", "coffee"] },
  3: { theme: "At home", pictures: ["key", "lamp", "clock", "chair"] },
  4: {
    theme: "Getting around",
    pictures: ["train", "bus", "plane", "car", "boat", "bicycle"],
  },
  5: {
    theme: "Animals",
    pictures: ["cat", "dog", "fish", "bird", "rabbit", "duck"],
  },
  6: {
    theme: "Clothes",
    pictures: ["shirt", "hat", "shoe", "glasses", "scarf", "glove"],
  },
  7: {
    theme: "Fruit and food",
    pictures: [
      "apple",
      "pear",
      "orange",
      "banana",
      "grapes",
      "lemon",
      "bread",
      "cake",
      "cheese",
    ],
  },
  8: {
    theme: "Around the house",
    pictures: [
      "key",
      "lamp",
      "clock",
      "chair",
      "table",
      "bed",
      "sofa",
      "door",
      "window",
    ],
  },
  9: {
    theme: "Vehicles",
    pictures: [
      "train",
      "bus",
      "plane",
      "car",
      "boat",
      "bicycle",
      "truck",
      "helicopter",
      "scooter",
    ],
  },
  10: {
    theme: "Look-alikes",
    pictures: [
      "umbrella",
      "hat",
      "lamp",
      "glasses",
      "key",
      "clock",
      "book",
      "coffee",
      "phone",
    ],
  },
};
