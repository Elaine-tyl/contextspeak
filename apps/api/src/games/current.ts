import type { GameRound } from "@contextspeak/contracts";
import type { Challenge } from "./catalog";
import { speakingInstruction, speakingRequirements } from "./difficulty";

// Original fictional practice scenarios. Real news keeps its separate source credit.
const dictations = [
  [
    "The council postponed the transport consultation because residents requested more time to review the proposal.",
    "Please confirm your availability before we finalize the reservation, because changing the appointment may involve an additional charge.",
    "Students should summarize the main argument and explain how the supporting evidence relates to their research question.",
  ],
  [
    "A community centre will trial a bicycle scheme for three months before deciding whether the service is financially sustainable.",
    "The cancellation policy allows a partial refund, provided you submit the required documentation before the departure date.",
    "Before recruiting participants, researchers must explain the purpose of the study and obtain their informed consent.",
  ],
  [
    "Local shops will separate food waste from other rubbish, but the collection schedule will remain unchanged until the trial has been evaluated.",
    "Although the menu describes the meal as vegetarian, customers with allergies should ask whether the kitchen can prevent contamination during preparation.",
    "The initial findings suggest a relationship between attendance and achievement, but a larger sample is needed before drawing reliable conclusions.",
  ],
  [
    "An office introduced an automated scheduling tool to reduce administration. Although routine bookings became faster, staff still checked unusual requests because the system sometimes overlooked important constraints.",
    "Because the disruption affected several connecting services, the operator offered an alternative itinerary. Passengers who preferred to cancel could instead apply for reimbursement of the unused portion of their fare.",
    "The lecturer encouraged students to distinguish between a persuasive argument and a well supported one. Confident wording alone does not establish that a conclusion follows from the available evidence.",
  ],
  [
    "The transport committee supported extending the bus service, but rejected an immediate expansion because the funding estimate excluded maintenance. Members requested a revised budget before making a long term commitment.",
    "The manager acknowledged a discrepancy on the invoice and offered to investigate. I retained the original receipt and requested written confirmation of the adjustment before agreeing to close the complaint.",
    "Researchers repeated the experiment using a different sample. Similar results increased confidence in the finding, although the team emphasized that replication does not eliminate every possible source of bias.",
  ],
  [
    "A fictional town proposed converting an empty building into affordable housing. Supporters highlighted the existing transport links, while neighbours asked how additional demand for schools and medical services would be accommodated.",
    "Our team agreed to prioritize essential functions and postpone optional features. This reduced the immediate workload, but we documented the deferred work so that temporary compromises would not quietly become permanent decisions.",
    "The study compared outcomes across several institutions. However, differences in recruitment and reporting procedures limited direct comparisons, so the authors presented their conclusions cautiously and recommended more consistent measurement in future research.",
  ],
  [
    "The committee considered strengthening flood protection after repeated service interruptions. Rather than relying on one large barrier, it proposed several smaller measures, arguing that a diversified approach could improve resilience if any individual component failed.",
    "The supplier promised a faster delivery, but the revised contract made that estimate conditional on customs clearance. Before accepting, we asked which delays would qualify for compensation and how disagreements about responsibility would be resolved.",
    "The scholarship analysis found substantial variation between applicant groups. The researchers cautioned that incomplete records and differences in access to advice might influence the results, making it inappropriate to attribute every difference to individual academic ability.",
  ],
  [
    "An original workplace scenario considers a company introducing automation to improve productivity. The proposal anticipates fewer repetitive tasks, but employees question whether the promised training will arrive before their responsibilities change. A phased trial could generate useful evidence while preserving an opportunity to revise the implementation plan.",
    "The procurement panel preferred an inexpensive bid until an independent assessment revealed substantial operating costs. Members then reconsidered the comparison, distinguishing the initial purchase price from the total financial commitment and asking suppliers to clarify which performance guarantees could actually be enforced under the proposed contract.",
    "Although the intervention was associated with improved outcomes, participants had volunteered rather than being randomly assigned. That recruitment process may have introduced confounding differences. The authors therefore distinguished a promising association from a causal claim and recommended an independent replication using a more robust design.",
  ],
  [
    "A fictional regional budget promised efficiency through consolidation, but the forecast depended on uncertain demand. An independent review recommended publishing the underlying assumptions and testing less optimistic scenarios. Supporters considered the proposal feasible, provided that implementation remained reversible and that essential services were protected if the anticipated savings failed to materialize.",
    "After the operator disputed our eligibility for compensation, we requested the evidence supporting its decision. We separated the original disruption from the later communication failures and proposed an independent review. This approach preserved our claim without assuming that every inconvenience automatically created an entitlement to the same financial remedy.",
    "The analysis reported a correlation between two trends, yet neither the direction of influence nor the role of unobserved variables was established. Rather than presenting the pattern as proof, the researchers described competing explanations and identified the additional evidence that would be necessary to distinguish among them with greater confidence.",
  ],
  [
    "In this fictional policy scenario, an environmental proposal balances immediate affordability against longer term resilience. Its projected benefits depend on assumptions about maintenance, public participation and future demand. The review recommends a limited trial with explicit safeguards, measurable evaluation criteria and an independent audit, allowing decision makers to revise the intervention before committing resources that would be difficult to recover.",
    "During negotiations, the supplier offered a substantial discount in exchange for an inflexible delivery schedule. We acknowledged the potential saving but explained that an unforeseen delay could impose disproportionate costs on other teams. Our counterproposal retained a smaller discount while defining contingency arrangements, responsibilities and an escalation procedure, so that flexibility was treated as a contractual requirement rather than an informal assurance.",
    "The study acknowledges methodological limitations that constrain the generalizability of its conclusions. Several potentially confounding variables were measured inconsistently, and missing observations may not have occurred at random. Although the findings remain informative, the authors describe their recommendations as provisional and call for independent replication, transparent reporting and corroborating evidence before the proposed relationship is treated as a reliable basis for wider policy decisions.",
  ],
];

const comprehension = [
  [
    "Why was the consultation postponed?",
    "postpone",
    "Residents needed more time to review it",
    "Funding had already been approved",
    "The transport service was cancelled",
    "The delay gives residents more time to review the proposal.",
  ],
  [
    "What will the trial help the centre decide?",
    "sustainable",
    "Whether the scheme can be sustained financially",
    "Which bicycle colour residents prefer",
    "Whether every resident owns a bicycle",
    "The centre will evaluate financial sustainability after a three month trial.",
  ],
  [
    "What is waiting for evaluation before it changes?",
    "schedule",
    "The collection schedule",
    "The requirement to separate food waste",
    "The location of every participating shop",
    "The collection schedule remains unchanged until the trial is evaluated.",
  ],
  [
    "Why did staff continue checking unusual requests?",
    "constraints",
    "The tool sometimes missed important constraints",
    "All routine bookings became slower",
    "The office had abandoned automation",
    "Faster routine work did not remove the need for human checks on exceptional cases.",
  ],
  [
    "What prevented an immediate expansion?",
    "maintenance",
    "The budget estimate omitted maintenance costs",
    "The committee opposed all bus services",
    "A long term contract had already been signed",
    "Support was conditional on a complete revised budget, not opposition to the service.",
  ],
  [
    "Which issue remained unresolved for neighbours?",
    "services",
    "How local services would handle additional demand",
    "Whether the building was already occupied",
    "Whether the town had any transport links",
    "Neighbours asked about capacity in schools and medical services.",
  ],
  [
    "Why favour several smaller measures?",
    "resilience",
    "To retain protection if one component failed",
    "To guarantee that floods could never happen",
    "To remove the need for future maintenance",
    "Diversification reduces reliance on a single component; it does not guarantee zero risk.",
  ],
  [
    "What is the strongest justification for a phased trial?",
    "evidence",
    "It can produce evidence while keeping revision possible",
    "It proves all employees will benefit equally",
    "It guarantees that training will arrive first",
    "A reversible trial tests assumptions; the promised benefits remain uncertain.",
  ],
  [
    "Under what condition did supporters consider the budget feasible?",
    "safeguards",
    "Implementation stays reversible and essential services are protected",
    "Every forecast is guaranteed to be accurate",
    "The review suppresses less optimistic scenarios",
    "The support is conditional on reversibility and protection of essential services.",
  ],
  [
    "Which conclusion is best supported?",
    "conditional",
    "A limited trial could test assumptions before a difficult commitment",
    "An independent audit guarantees every projected benefit",
    "Affordability should replace all evaluation of resilience",
    "The proposal is conditional on assumptions and safeguards, so testing is justified rather than unconditional adoption.",
  ],
];

const pictures: [
  string,
  NonNullable<GameRound["choices"]>[number]["picture"],
][] = [
  [
    "The pedestrian crossing spans the waterway and allows residents to reach the opposite bank without a vehicle. Choose the structure described.",
    "bridge",
  ],
  [
    "Choose the portable reference with bound pages, an index and a protective cover, rather than a structure used for transport.",
    "book",
  ],
  [
    "Rather than paying for fuel, the commuter pedals a lightweight vehicle and secures its two wheels beside the station. Choose that vehicle.",
    "bicycle",
  ],
  [
    "Although it is not protective clothing, this collapsible object creates a temporary shelter above one person during an unexpected downpour.",
    "umbrella",
  ],
  [
    "The infrastructure provides a route across the river. It does not carry passengers itself, and its function depends on connecting otherwise separated banks.",
    "bridge",
  ],
  [
    "The archivist selects a bound publication whose information can be consulted without electricity. Choose the object, not the route described within it.",
    "book",
  ],
  [
    "The vehicle relies on human propulsion rather than an engine. Its accessibility depends partly on safe routes, but the object itself has two wheels and pedals.",
    "bicycle",
  ],
  [
    "The reading event was originally planned outdoors, and the organiser suggested equipment for brief showers. It has since moved inside, so that precaution is no longer required. Each participant must now bring a portable reference that can be consulted during a power cut; handwritten notes and electronic devices do not meet the requirement. Which pictured object should participants bring under the revised plan?",
    "book",
  ],
  [
    "The committee initially proposed a bicycle lending scheme for residents living across the river. However, many intended users cannot operate those vehicles, and the project must provide continuous access without requiring individual ownership. Printed information will explain the route but is only a supporting measure. Which pictured option best represents the main intervention that still satisfies the access requirements?",
    "bridge",
  ],
  [
    "A transport pilot initially considered a permanent crossing and an information campaign. The funding rules now exclude construction and printed publicity, while the objective remains to replace short car journeys. The chosen asset must be reusable, individually operated and able to move its user between destinations. Weather protection alone would not achieve that objective. Which pictured option satisfies all the remaining conditions?",
    "bicycle",
  ],
];

const riddles = [
  [
    "I secure a place before you arrive, although I cannot guarantee that you will enjoy the visit. You can confirm, change or cancel me. What am I? Explain two clues.",
    "reservation",
  ],
  [
    "I can approach without moving. Missing me can delay a project, and extending me gives people more time. What am I? Explain how the clues connect.",
    "deadline",
  ],
  [
    "I record what you paid, but I am not money. Keeping me may support a refund, and losing me may make a complaint harder to prove. What am I?",
    "receipt",
  ],
  [
    "I let an authorized person enter without opening a physical door. Sharing me can weaken protection, and changing me can restore control. What am I? Explain the clues.",
    "password",
  ],
  [
    "I describe something that has not happened, rely on available evidence and become less reliable as uncertainty increases. People revise their plans when I change. What am I?",
    "forecast",
  ],
  [
    "I can resolve a disagreement without giving either side everything it wanted. Reaching me requires concessions, and judging me depends on what each side values. What am I?",
    "compromise",
  ],
  [
    "I organize access without owning the service. My order can appear fair even while the wait feels frustrating, and joining me does not guarantee immediate progress. What am I?",
    "queue",
  ],
  [
    "I can reward a saver and increase a borrower's obligation. My meaning also describes curiosity, but a financial agreement determines how I accumulate. What am I? Explain why another answer fits less well.",
    "interest",
  ],
  [
    "I may support a claim without making it certain. People evaluate my relevance, reliability and sufficiency, and contradictory examples can reduce my persuasive force. What am I? Compare another possibility.",
    "evidence",
  ],
  [
    "I emerge from other people's judgments rather than a legal certificate. Consistent behaviour may strengthen me, yet one disputed event can damage me before every fact is known. What am I? Compare a plausible alternative.",
    "reputation",
  ],
];

const puzzles = [
  [
    "It is nine thirty-five and your appointment begins at ten. Walking takes twenty minutes; the bus journey takes thirty minutes including the wait. Choose a workable option and explain how much time remains when you arrive.",
    "Walking reaches the appointment at nine fifty-five, leaving five minutes. Other solutions must meet the stated time constraint.",
  ],
  [
    "You have twenty-five dollars. Two tickets cost eight dollars each, lunch costs seven dollars in total and the return fare is six dollars. Explain why the full plan exceeds the budget and propose an affordable change.",
    "The full plan costs twenty-nine dollars. Reduce spending by at least four dollars, change an activity or explain another feasible arrangement without inventing free purchases.",
  ],
  [
    "A workshop needs a room for two hours, and a discussion needs the same room for one hour. The room is available from one until four. Propose a schedule without overlap and explain who should receive the booking confirmation.",
    "Schedule the two-hour workshop and one-hour discussion consecutively within one to four; notify both organizers.",
  ],
  [
    "A team must deliver a report on Friday. Research takes two days and writing takes one day after research finishes. Review takes one day after writing. Starting Monday, propose a schedule and explain one risk or alternative.",
    "Research Monday and Tuesday, write Wednesday, review Thursday, leaving Friday as a buffer. A valid alternative must preserve dependencies and the deadline.",
  ],
  [
    "A café can fulfil forty orders per hour. A group wants sixty orders ready during a single hour, and the manager cannot hire extra staff. Propose a feasible arrangement and discuss the trade-off rather than promising impossible capacity.",
    "Stagger collection across at least ninety minutes or reduce the order; explain the timing or selection trade-off without exceeding capacity.",
  ],
  [
    "An outdoor event expects rain. An indoor room holds thirty people, but forty have registered. The budget cannot cover a larger room. Propose an accessible alternative and explain how you would communicate any limitation fairly.",
    "Use feasible staggered sessions or another supported arrangement; do not promise forty simultaneous places in a thirty-person room. Explain access, communication and trade-offs.",
  ],
  [
    "Equipment is needed within twenty days and the budget is thirteen hundred dollars. Supplier A costs twelve hundred and delivers in ten days. B costs eleven hundred but delivers in thirty days. C costs thirteen hundred and fifty and delivers in fifteen. Recommend a feasible option and discuss a trade-off.",
    "A meets the stated deadline and budget. Negotiating C is a conditional alternative, not a guaranteed discount; B misses the deadline unless the requirement changes.",
  ],
  [
    "Three colleagues need a handover, but one works only mornings and another only afternoons. The third can work either period, and no overtime is available. Propose a fair workflow, consider an alternative and qualify an assumption about how information will be transferred.",
    "Use the flexible colleague or an agreed written handover to bridge the periods. Explain fairness and qualify communication assumptions; do not require overlapping hours that are unavailable.",
  ],
  [
    "A fictional community has funds for either extending evening transport or improving a daytime clinic, but not both immediately. Evidence about demand is incomplete. Propose a decision process, compare the alternatives and explain what further evidence might change your recommendation.",
    "A transparent conditional recommendation with evidence gathering and consideration of affected users is valid. No particular policy preference is required for a high score.",
  ],
  [
    "A new system may reduce processing time, but the pilot excluded the most complex cases and has no independent comparison group. Recommend whether to expand, revise or pause it. Compare an alternative and explain what evidence would justify changing your decision under a limited budget.",
    "Any defensible conditional decision is valid if it addresses selection bias, the missing comparison, resource trade-offs and evidence that could change the recommendation.",
  ],
];

const responses = [
  [
    "Your usual appointment is unavailable. Ask for a different time, explain one scheduling constraint and respond politely if the first alternative does not work.",
    "A connected request with a practical scheduling constraint and a polite alternative.",
  ],
  [
    "A colleague suggests using an automated tool for routine administration. Explain one useful task it could support and one thing a person should still check.",
    "Identify an appropriate routine task and a relevant human check, supported by a reason.",
  ],
  [
    "Your town wants to reduce food waste. Suggest one practical change for households or shops and explain how people could tell whether it is working.",
    "A feasible suggestion and a relevant way of evaluating its effect.",
  ],
  [
    "A friend is considering a cheaper travel booking with strict cancellation rules. Explain the trade-off, suggest an alternative and ask what matters most to them.",
    "Compare cost and flexibility, offer an alternative and ask a relevant follow-up.",
  ],
  [
    "A workplace is considering a short trial of flexible hours. Explain a possible benefit, consider a drawback and suggest how the trial should be evaluated.",
    "A balanced proposal with a benefit, a trade-off and a practical evaluation method.",
  ],
  [
    "A study reports that students who attend more classes tend to achieve higher marks. Explain what the result suggests, what it does not prove and one alternative explanation.",
    "Distinguish association from causation and give a plausible alternative explanation without claiming certainty.",
  ],
  [
    "A local service is reliable but expensive. Discuss whether to keep it, replace it or run a limited trial of another option. Explain the trade-off and what information you would request.",
    "A justified recommendation with an alternative, a trade-off and a relevant information request.",
  ],
  [
    "A company promises that automation will improve productivity. Explain what evidence you would request, compare an alternative implementation plan and qualify one assumption about its effects on staff.",
    "Evaluate the claim, compare a viable alternative and qualify a relevant assumption; no fixed opinion is required.",
  ],
  [
    "A policy proposal promises substantial savings, but its forecast uses an optimistic demand estimate. Explain how you would assess it, consider an alternative and identify evidence that could change your recommendation.",
    "Discuss assumptions, uncertainty and alternatives with conditional reasoning and relevant evidence.",
  ],
  [
    "A promising research finding is being used to justify a major policy change before independent replication. Give a reasoned recommendation, compare a less irreversible option and explain a limitation that affects how confidently the evidence can be generalized.",
    "A qualified evidence-based recommendation recognizing replication, generalizability and the trade-off between action and uncertainty.",
  ],
];

const matches = [
  [
    "You are arranging a group reservation. Ask the organiser to confirm attendance, explain why the café needs an accurate number and suggest what to do if someone cancels.",
    "A clear confirmation request, reason and practical cancellation arrangement.",
  ],
  [
    "A customer asks for a refund without the original receipt. As the shop assistant, explain what other evidence could help and offer a practical next step without promising an unsupported refund.",
    "A polite request for alternative evidence and a realistic next step.",
  ],
  [
    "Your friend proposes an activity that exceeds your budget. Respond tactfully, explain your constraint and suggest an affordable alternative that still lets you spend time together.",
    "A tactful explanation and an affordable shared alternative.",
  ],
  [
    "A supplier offers faster delivery for a higher fee. Negotiate a workable arrangement, explain your priority and consider an alternative if the fee cannot be reduced.",
    "A practical negotiation with a stated priority and an alternative; no imaginary agreement is required.",
  ],
  [
    "An organiser wants to add a feature just before launch. Explain the risk, offer a limited alternative and ask which requirement should take priority if the deadline remains fixed.",
    "A clear risk, feasible alternative and relevant priority question.",
  ],
  [
    "A colleague disagrees with your interpretation of survey results. Respond constructively, explain your reasoning and suggest an alternative way to check the conclusion.",
    "A respectful evidence-based response and a workable checking approach.",
  ],
  [
    "A client requests a guarantee that depends on an external supplier. Explain the constraint, propose a conditional commitment and offer an alternative that reduces the client's exposure to delay.",
    "Avoid an unsupported guarantee; offer a clear conditional commitment and alternative.",
  ],
  [
    "A procurement panel challenges your recommendation because another bid is cheaper. Defend your reasoning, compare total costs rather than price alone and qualify one assumption that should be verified before signing.",
    "Compare relevant costs, respond to the challenge and qualify a material assumption.",
  ],
  [
    "A stakeholder insists that an uncertain forecast proves a proposal will succeed. Challenge the claim diplomatically, offer a less irreversible approach and explain what evidence would increase your confidence.",
    "A tactful qualified challenge, a feasible alternative and a clear evidence threshold.",
  ],
  [
    "You are negotiating a deadline that would require an irreversible financial commitment. Explain the dependencies, propose a contingency arrangement and compare an alternative while acknowledging one uncertainty that neither side can currently resolve.",
    "A coherent negotiation addressing dependencies, contingency, alternatives and unresolved uncertainty without inventing guarantees.",
  ],
];

const pictureChoices: NonNullable<GameRound["choices"]> = [
  { id: "umbrella", label: "Umbrella", picture: "umbrella" },
  { id: "book", label: "Book", picture: "book" },
  { id: "bridge", label: "Bridge", picture: "bridge" },
  { id: "bicycle", label: "Bicycle", picture: "bicycle" },
];
const themes = ["News analysis", "Everyday decisions", "Academic study"];
const spokenKinds = ["riddle", "puzzle", "response", "pk"] as const;
const spokenData = [riddles, puzzles, responses, matches];
const spokenTitles = [
  "Reason through a riddle",
  "Solve a practical problem",
  "Discuss a scenario",
  "AI sparring match",
];

export const currentChallenges: Challenge[] = dictations.flatMap(
  (texts, index) => {
    const level = index + 1;
    const [question, correct, label, second, third, explanation] =
      comprehension[index];
    const options = [
      { id: correct, label },
      { id: "other-1", label: second },
      { id: "other-2", label: third },
    ];
    // Rotate positions so careful listening, not a fixed option position, matters.
    const offset = index % options.length;
    return [
      ...texts.map((text, genre): Challenge => ({
        id: `game-dictation-${level}-${genre}-v3`,
        track: "listening",
        kind: "dictation",
        level,
        topic: themes[genre],
        title: "Original practice scenario · dictation",
        instruction:
          "This is a fictional practice scenario. Listen, then type exactly what you hear.",
        audioText: text,
        answer: text,
        explanation:
          "Compare the passage with your answer. Case and punctuation are ignored.",
      })),
      {
        id: `game-picture-${level}-0-v3`,
        track: "listening",
        kind: "picture",
        level,
        topic: "Paraphrase and inference",
        title: "Listen beyond the keyword",
        instruction:
          "Listen to the description and choose the matching picture.",
        audioText: pictures[index][0],
        choices: pictureChoices,
        answer: pictures[index][1]!,
        explanation: `The description refers to a ${pictures[index][1]}. Compare the function and clues, not just isolated words.`,
      },
      {
        id: `game-comprehension-${level}-0-v3`,
        track: "listening",
        kind: "comprehension",
        level,
        topic: "News analysis",
        title: "Evaluate what you hear",
        instruction: `Fictional practice scenario. ${question}`,
        audioText: texts[0],
        choices: [...options.slice(offset), ...options.slice(0, offset)],
        answer: correct,
        explanation,
      },
      ...spokenKinds.map((kind, position): Challenge => {
        const [prompt, answer] = spokenData[position][index];
        return {
          id: `game-${kind}-${level}-0-v3`,
          track: "speaking",
          kind,
          level,
          topic:
            kind === "response"
              ? "News and everyday reasoning"
              : kind === "riddle"
                ? "Clues and meanings"
                : "Decisions and negotiation",
          title: spokenTitles[position],
          instruction: `${kind === "riddle" ? "Original riddle." : "Fictional practice scenario."} ${speakingInstruction(level, kind)}`,
          audioText: prompt,
          answer,
          criteria: speakingRequirements(level, kind),
          explanation:
            "AI feedback evaluates your reasoning and communication. Relevant alternative answers are accepted; the reference is not a script to repeat.",
          ...(kind === "pk" ? { opponentScore: 55 + level * 3 } : {}),
        };
      }),
    ];
  },
);
