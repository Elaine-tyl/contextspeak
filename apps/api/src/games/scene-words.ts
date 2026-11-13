import type { WordScene } from "@contextspeak/contracts";
import type { Challenge } from "./catalog";

type Entry = {
  word: string;
  room: WordScene["room"];
  focus: WordScene["focus"];
  sentence: string;
  meaning: string;
  translation: string;
  example: string;
  phrases: string[];
};
// Original, reviewable teaching material. The brackets mark one spoken word;
// word notes are private catalog data until the server has checked an answer.
const levels: Entry[][] = [
  [
    {
      word: "wardrobe",
      room: "bedroom",
      focus: "wardrobe",
      sentence: "There is a wooden [wardrobe] beside the bed.",
      meaning: "A tall piece of furniture used for storing clothes.",
      translation: "衣柜",
      example: "She hung her jacket in the wardrobe.",
      phrases: ["a fitted wardrobe", "wardrobe doors"],
    },
    {
      word: "mirror",
      room: "bedroom",
      focus: "mirror",
      sentence: "She checked her hair in the [mirror] before leaving.",
      meaning: "A surface that reflects an image of a person or object.",
      translation: "镜子",
      example: "There is a round mirror above the desk.",
      phrases: ["a full-length mirror", "look in the mirror"],
    },
    {
      word: "blanket",
      room: "bedroom",
      focus: "blanket",
      sentence: "It is getting cold, so put another [blanket] on the bed.",
      meaning: "A warm covering used on a bed or around the body.",
      translation: "毯子",
      example: "I wrapped a soft blanket around my shoulders.",
      phrases: ["a wool blanket", "a warm blanket"],
    },
  ],
  [
    {
      word: "kettle",
      room: "kitchen",
      focus: "kettle",
      sentence: "Fill the [kettle] with water so we can make some tea.",
      meaning: "A container or appliance used to boil water.",
      translation: "烧水壶",
      example: "The kettle switched off when the water boiled.",
      phrases: ["an electric kettle", "put the kettle on"],
    },
    {
      word: "cupboard",
      room: "kitchen",
      focus: "cupboard",
      sentence: "The clean plates are in the [cupboard] above the counter.",
      meaning:
        "A piece of furniture with doors and shelves for storing things.",
      translation: "橱柜",
      example: "Please put the mugs back in the cupboard.",
      phrases: ["a kitchen cupboard", "a cupboard shelf"],
    },
    {
      word: "toaster",
      room: "kitchen",
      focus: "toaster",
      sentence: "Put two slices of bread into the [toaster] for breakfast.",
      meaning:
        "An appliance that heats bread until it becomes crisp and brown.",
      translation: "烤面包机",
      example: "The toaster has a setting for thicker bread.",
      phrases: ["a two-slice toaster", "unplug the toaster"],
    },
  ],
  [
    {
      word: "receipt",
      room: "cafe",
      focus: "receipt",
      sentence: "Keep the [receipt] in case you need to check what you paid.",
      meaning: "A record showing that a payment has been made.",
      translation: "收据；小票",
      example: "The cashier emailed me a receipt.",
      phrases: ["an itemised receipt", "keep your receipt"],
    },
    {
      word: "pastry",
      room: "cafe",
      focus: "pastry",
      sentence:
        "She ordered a coffee and a fresh [pastry] from the display case.",
      meaning:
        "A baked food made from dough, often with a sweet or savoury filling.",
      translation: "酥皮点心；糕点",
      example: "We shared a flaky pastry with our tea.",
      phrases: ["a fresh pastry", "a pastry chef"],
    },
    {
      word: "counter",
      room: "cafe",
      focus: "counter",
      sentence:
        "Please collect your drinks from the [counter] near the window.",
      meaning:
        "A long surface where customers are served or goods are displayed.",
      translation: "柜台",
      example: "The barista placed my cup on the counter.",
      phrases: ["the service counter", "pay at the counter"],
    },
  ],
  [
    {
      word: "produce",
      room: "market",
      focus: "produce",
      sentence: "This stall sells fresh [produce] from farms outside the city.",
      meaning: "Fruit, vegetables and other food grown on farms.",
      translation: "农产品；新鲜果蔬",
      example: "The market stocks locally grown produce.",
      phrases: ["fresh produce", "seasonal produce"],
    },
    {
      word: "scales",
      room: "market",
      focus: "scales",
      sentence:
        "The seller put the apples on the [scales] to work out their weight.",
      meaning: "Equipment used to measure how heavy something is.",
      translation: "秤",
      example: "Check that the scales read zero before weighing the fruit.",
      phrases: ["digital scales", "a set of scales"],
    },
    {
      word: "basket",
      room: "market",
      focus: "basket",
      sentence:
        "Take a [basket] if you only need a few things from the market.",
      meaning:
        "A container, often with a handle, used to carry or store things.",
      translation: "篮子",
      example: "I carried the vegetables home in a basket.",
      phrases: ["a shopping basket", "a woven basket"],
    },
  ],
  [
    {
      word: "deposit",
      room: "apartment",
      focus: "document",
      sentence:
        "Before moving into the apartment, we paid a refundable [deposit] to cover possible damage.",
      meaning:
        "Money paid in advance as security, sometimes returned when conditions are met.",
      translation: "押金；保证金",
      example: "We received the full deposit back after the inspection.",
      phrases: ["a refundable deposit", "a security deposit"],
    },
    {
      word: "tenancy",
      room: "apartment",
      focus: "document",
      sentence:
        "Read the [tenancy] agreement carefully before you sign it and collect the apartment keys.",
      meaning:
        "The arrangement or period under which a person rents a property.",
      translation: "租赁关系；租期",
      example: "The tenancy begins on the first day of June.",
      phrases: ["a tenancy agreement", "a fixed-term tenancy"],
    },
    {
      word: "utilities",
      room: "apartment",
      focus: "receipt",
      sentence:
        "The rent covers the apartment, but [utilities] such as water and electricity cost extra.",
      meaning:
        "Services such as electricity, gas and water supplied to homes and businesses.",
      translation: "水、电、燃气等公用服务",
      example: "We share the monthly cost of utilities.",
      phrases: ["utility bills", "pay for utilities"],
    },
  ],
  [
    {
      word: "allergens",
      room: "restaurant",
      focus: "menu",
      sentence:
        "The restaurant lists common [allergens] on its menu so customers can avoid ingredients that may make them ill.",
      meaning: "Substances that can cause an allergic reaction in some people.",
      translation: "过敏原",
      example: "Ask the server which allergens are present in the dessert.",
      phrases: ["common allergens", "allergen information"],
    },
    {
      word: "substitution",
      room: "restaurant",
      focus: "menu",
      sentence:
        "If you cannot eat dairy, the chef can suggest a [substitution] that still works well in the dish.",
      meaning: "The act of using one thing in place of another.",
      translation: "替换；替代品",
      example: "Oat milk is a possible substitution for regular milk.",
      phrases: ["an ingredient substitution", "make a substitution"],
    },
    {
      word: "reservation",
      room: "restaurant",
      focus: "document",
      sentence:
        "The restaurant was busy, so we made a [reservation] to ensure that a table would be available.",
      meaning:
        "An arrangement to keep a table, room or seat available for someone.",
      translation: "预订",
      example: "I called to confirm our dinner reservation.",
      phrases: ["make a reservation", "confirm a reservation"],
    },
  ],
  [
    {
      word: "itinerary",
      room: "airport",
      focus: "document",
      sentence:
        "Check your [itinerary] before leaving, because it lists the flight times, hotel bookings and onward travel arrangements.",
      meaning: "A plan showing the route and details of a journey.",
      translation: "行程；行程表",
      example: "Our itinerary includes a night in Vancouver.",
      phrases: ["a detailed itinerary", "a revised itinerary"],
    },
    {
      word: "departure",
      room: "airport",
      focus: "suitcase",
      sentence:
        "The airline announced a later [departure], giving passengers another hour before their flight was due to leave.",
      meaning: "The act or scheduled time of leaving a place.",
      translation: "出发；离境",
      example: "Please arrive at the airport well before departure.",
      phrases: ["departure time", "the departure gate"],
    },
    {
      word: "reimbursement",
      room: "airport",
      focus: "receipt",
      sentence:
        "After the cancellation, we submitted our hotel receipts to request [reimbursement] for the extra accommodation costs.",
      meaning:
        "Money paid back to someone for expenses they have already covered.",
      translation: "报销；费用偿还",
      example: "Keep all receipts when applying for reimbursement.",
      phrases: ["claim reimbursement", "travel reimbursement"],
    },
  ],
  [
    {
      word: "accessibility",
      room: "cafe",
      focus: "building",
      sentence:
        "The café improved its [accessibility] by adding a ramp and making the entrance wide enough for wheelchair users.",
      meaning:
        "How easily a place or service can be reached and used, including by people with disabilities.",
      translation: "无障碍程度；可及性",
      example: "The renovation made accessibility a priority.",
      phrases: ["improve accessibility", "accessibility requirements"],
    },
    {
      word: "ventilation",
      room: "kitchen",
      focus: "fan",
      sentence:
        "The kitchen needs better [ventilation] to replace stale air and remove steam while meals are being prepared.",
      meaning: "The movement or supply of fresh air through an enclosed space.",
      translation: "通风",
      example: "Opening the upper window improves ventilation.",
      phrases: ["natural ventilation", "a ventilation system"],
    },
    {
      word: "refurbishment",
      room: "cafe",
      focus: "counter",
      sentence:
        "The café closed temporarily for [refurbishment], replacing worn furniture and repairing the interior before reopening.",
      meaning:
        "Work done to repair and improve the appearance or condition of a building.",
      translation: "翻新；整修",
      example: "The library stayed open during its refurbishment.",
      phrases: ["a major refurbishment", "refurbishment work"],
    },
  ],
  [
    {
      word: "insulation",
      room: "apartment",
      focus: "building",
      sentence:
        "Better [insulation] in the walls reduces heat loss, helping the apartment remain comfortable without using as much energy.",
      meaning:
        "Material or protection that slows the transfer of heat, sound or electricity.",
      translation: "隔热；隔音；绝缘材料",
      example: "The new roof insulation helped lower our heating bills.",
      phrases: ["thermal insulation", "improve insulation"],
    },
    {
      word: "condensation",
      room: "apartment",
      focus: "window",
      sentence:
        "On cold mornings, [condensation] forms on the inside of the window when warm, moist air meets the cooler glass.",
      meaning:
        "Water droplets formed when water vapour cools and becomes liquid.",
      translation: "冷凝；凝结水",
      example: "We wiped the condensation from the bathroom mirror.",
      phrases: ["window condensation", "prevent condensation"],
    },
    {
      word: "maintenance",
      room: "apartment",
      focus: "document",
      sentence:
        "Regular [maintenance] can identify small problems with the heating system before they develop into expensive failures.",
      meaning:
        "Work that keeps equipment or a building in good working condition.",
      translation: "维护；保养",
      example: "The lift is closed for routine maintenance.",
      phrases: ["routine maintenance", "maintenance costs"],
    },
  ],
  [
    {
      word: "sustainability",
      room: "cafe",
      focus: "building",
      sentence:
        "The café evaluated the [sustainability] of its operations by considering energy use, waste reduction and the long-term effects of its purchasing decisions.",
      meaning:
        "The ability to continue an activity over time while limiting harm to resources, people and the environment.",
      translation: "可持续性",
      example:
        "The project considers both financial and environmental sustainability.",
      phrases: ["environmental sustainability", "long-term sustainability"],
    },
    {
      word: "retrofitting",
      room: "apartment",
      focus: "building",
      sentence:
        "Rather than demolishing the older apartments, the council proposed [retrofitting] them with improved heating controls and energy-saving windows.",
      meaning:
        "Adding newer equipment or features to something that was built earlier.",
      translation: "对既有建筑或设备进行改装升级",
      example: "Retrofitting older homes can improve their energy performance.",
      phrases: [
        "retrofitting existing buildings",
        "energy-efficient retrofitting",
      ],
    },
    {
      word: "efficiency",
      room: "apartment",
      focus: "window",
      sentence:
        "The new heating system improved energy [efficiency], providing the same level of comfort while requiring less electricity than the previous installation.",
      meaning:
        "Achieving a result with less wasted time, energy or other resources.",
      translation: "效率；效能",
      example: "Good insulation can improve a building's energy efficiency.",
      phrases: ["energy efficiency", "improve efficiency"],
    },
  ],
];
export const sceneWordChallenges: Challenge[] = levels.flatMap(
  (entries, index) =>
    entries.map((entry, item) => {
      const [before, after] = entry.sentence.split(`[${entry.word}]`);
      if (after === undefined)
        throw new Error(`Missing scene gap: ${entry.word}`);
      const audioText = before + entry.word + after;
      return {
        id: `game-scene-words-${index + 1}-${item}-v1`,
        kind: "scene-words",
        track: "listening",
        level: index + 1,
        title: `In the ${entry.room === "cafe" ? "café" : entry.room}`,
        topic: entry.room[0].toUpperCase() + entry.room.slice(1),
        instruction:
          "Listen to the sentence. Type the missing word, then check your answer.",
        audioText,
        gapText: [before, after],
        gapAnswers: [entry.word],
        answer: audioText,
        scene: { room: entry.room, focus: entry.focus },
        sceneWordNotes: {
          word: entry.word,
          meaning: entry.meaning,
          translation: entry.translation,
          example: entry.example,
          phrases: entry.phrases,
        },
        explanation:
          "Each scene has one missing word. Case and punctuation are ignored; the word must match what you heard.",
      };
    }),
);
