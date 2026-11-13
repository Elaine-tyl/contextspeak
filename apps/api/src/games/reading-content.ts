import type { Challenge } from "./catalog";
import { shuffle } from "./new-modes";

type Reading = {
  title: string;
  topic: string;
  paragraphs: string[];
  question: string;
  answers: string[];
  evidence: string;
};
// Independently written fiction and explanatory texts. The first answer is the
// editorial solution; public choices and paragraph tiles are shuffled separately.
const levels: Reading[][] = [
  [
    {
      title: "A seat by the window",
      topic: "Everyday life",
      paragraphs: [
        "Maya reached the café before her friend and chose a table beside the window.",
        "A few minutes later, the server asked whether she was ready to order.",
        "Maya explained that she was waiting for someone, so the server brought her some water instead.",
        "When her friend finally arrived, they looked at the menu together.",
      ],
      question: "Why did Maya wait to order?",
      answers: [
        "She wanted to order with her friend.",
        "The café had run out of food.",
        "She could not find a seat.",
        "The server had forgotten her.",
      ],
      evidence:
        "Maya said she was waiting for someone; they read the menu together after the friend arrived.",
    },
    {
      title: "The blue notebook",
      topic: "Everyday life",
      paragraphs: [
        "Leo left his blue notebook on a library table when he went to find a book.",
        "On returning, he saw that the table was empty and asked at the information desk.",
        "The librarian checked a drawer and found the notebook someone had handed in.",
        "Relieved, Leo thanked her and put it safely in his bag.",
      ],
      question: "Where did Leo recover his notebook?",
      answers: [
        "At the information desk.",
        "Inside a borrowed book.",
        "At the bus stop.",
        "From a café server.",
      ],
      evidence:
        "The librarian found the notebook in a drawer after Leo asked at the desk.",
    },
    {
      title: "Rainy-day plans",
      topic: "Everyday life",
      paragraphs: [
        "Amir and Jo planned to walk in the park on Saturday.",
        "That morning, they heard heavy rain against the window.",
        "Rather than cancel their afternoon together, they decided to visit a small museum nearby.",
        "They spent two hours exploring its rooms and agreed to try the park another weekend.",
      ],
      question: "What did the friends do when it rained?",
      answers: [
        "They visited a museum instead.",
        "They walked in the park anyway.",
        "They stayed in separate homes.",
        "They cancelled all future plans.",
      ],
      evidence:
        "They chose the museum as an alternative and postponed the park visit.",
    },
  ],
  [
    {
      title: "Changing a booking",
      topic: "Travel",
      paragraphs: [
        "Nora booked a morning train for an interview in the next city.",
        "The employer then moved the interview to the afternoon and emailed her the new time.",
        "After reading the email, Nora changed her ticket to a later train without cancelling the trip.",
        "The revised booking let her arrive with time to spare and avoid an unnecessarily early start.",
      ],
      question: "Why did Nora change her ticket?",
      answers: [
        "The interview time had changed.",
        "The train had been cancelled.",
        "She no longer wanted the job.",
        "The employer changed the city.",
      ],
      evidence:
        "The employer moved the interview to the afternoon before Nora revised her train booking.",
    },
    {
      title: "The borrowed bicycle",
      topic: "Travel",
      paragraphs: [
        "Sam offered to lend his bicycle to a visiting cousin for the weekend.",
        "Before handing it over, he noticed that one tyre needed air.",
        "He pumped up the tyre and showed his cousin where the lights and brakes were.",
        "Only after those checks did the cousin leave for a short ride along the river.",
      ],
      question: "What did Sam do before the ride?",
      answers: [
        "He checked and prepared the bicycle.",
        "He sold the bicycle to his cousin.",
        "He replaced both wheels.",
        "He arranged a taxi.",
      ],
      evidence:
        "Sam inflated the tyre and explained the controls before the cousin set off.",
    },
    {
      title: "A different entrance",
      topic: "Travel",
      paragraphs: [
        "A sign at the front of the gallery said that the main entrance was temporarily closed.",
        "Visitors following its arrow found a side door beside the garden.",
        "Staff there checked tickets and explained that building repairs were taking place at the front.",
        "With admission still available, the visitors continued into the exhibition.",
      ],
      question: "What was temporarily unavailable?",
      answers: [
        "The main entrance.",
        "The whole exhibition.",
        "All ticket checks.",
        "The garden itself.",
      ],
      evidence:
        "Only the front entrance was closed; visitors entered through a side door.",
    },
  ],
  [
    {
      title: "The repair estimate",
      topic: "Work",
      paragraphs: [
        "A community centre asked a technician to inspect its unreliable heating system.",
        "Following the inspection, she provided an estimate rather than beginning the work immediately.",
        "The manager compared that estimate with the maintenance budget and approved a smaller repair.",
        "Once approval was recorded, the technician returned with the necessary parts.",
      ],
      question: "Why did the technician wait before repairing the system?",
      answers: [
        "The manager needed to approve the proposed work.",
        "The building had permanently closed.",
        "The parts had already been installed.",
        "The heating was working perfectly.",
      ],
      evidence:
        "Inspection and an estimate came first; the technician returned after approval.",
    },
    {
      title: "Volunteer shifts",
      topic: "Community",
      paragraphs: [
        "The food bank initially asked every volunteer to attend the same Saturday session.",
        "Several people explained that they worked on Saturdays but were free on weekday evenings.",
        "In response, the coordinator introduced an evening packing session as well as the original shift.",
        "The change allowed more people to help without removing the weekend option.",
      ],
      question: "What was the purpose of the new session?",
      answers: [
        "To include volunteers with different schedules.",
        "To replace all weekend volunteers.",
        "To reduce the number of donations.",
        "To require everyone to work every evening.",
      ],
      evidence:
        "An additional evening session accommodated people unable to attend on Saturdays.",
    },
    {
      title: "An alternative route",
      topic: "Travel",
      paragraphs: [
        "Roadworks added twenty minutes to the usual bus journey to the hospital.",
        "After missing an appointment, Eli checked the transport map for another route.",
        "He found a train service with a short walk at the end and tested it the next morning.",
        "Because that journey was more predictable, he used it for his next appointment.",
      ],
      question: "What mattered most to Eli when choosing the new route?",
      answers: [
        "A more dependable arrival time.",
        "Avoiding every form of walking.",
        "Using the same bus stop.",
        "Travelling without a map.",
      ],
      evidence:
        "The train journey was more predictable after the roadworks had caused a missed appointment.",
    },
  ],
  [
    {
      title: "A quieter workspace",
      topic: "Work",
      paragraphs: [
        "Staff at a shared office reported difficulty concentrating during informal meetings near their desks.",
        "Rather than banning discussion, the manager asked which activities actually required quiet.",
        "That distinction informed a trial layout with a silent room and a separate collaborative area.",
        "At the end of the trial, staff reported fewer interruptions while still being able to exchange ideas.",
      ],
      question: "What principle guided the redesign?",
      answers: [
        "Different activities needed different environments.",
        "All conversation should be prohibited.",
        "Everyone preferred to work at home.",
        "Informal meetings had no value.",
      ],
      evidence:
        "The manager separated quiet tasks from collaboration rather than eliminating conversation.",
    },
    {
      title: "The refill scheme",
      topic: "Environment",
      paragraphs: [
        "A grocery shop introduced refill containers to reduce disposable packaging.",
        "During the first week, customers said that the weighing process was confusing.",
        "Using their feedback, staff added illustrated instructions and offered a brief demonstration at the entrance.",
        "Refill use subsequently increased, although the shop continued to offer packaged goods for customers who needed them.",
      ],
      question: "What initially limited participation?",
      answers: [
        "Uncertainty about how to use the system.",
        "A ban on all packaged products.",
        "The absence of grocery items.",
        "A requirement to buy new scales.",
      ],
      evidence:
        "Customers described the weighing process as confusing; clearer instructions followed.",
    },
    {
      title: "Shared equipment",
      topic: "Community",
      paragraphs: [
        "A neighbourhood group had enough funding for either a small tool collection or a larger one with no repair budget.",
        "Members compared likely demand with the cost of maintaining frequently borrowed items.",
        "On that basis, they selected the smaller collection and reserved funds for repairs.",
        "The lending scheme opened with fewer tools, but damaged equipment could be restored instead of discarded.",
      ],
      question: "Why was the smaller collection chosen?",
      answers: [
        "It left resources to keep the tools usable.",
        "Members had no interest in borrowing tools.",
        "All repairs were guaranteed to be free.",
        "The larger collection included a repair budget.",
      ],
      evidence:
        "Members prioritised maintenance capacity over the initial number of tools.",
    },
  ],
  [
    {
      title: "Trial before expansion",
      topic: "Work",
      paragraphs: [
        "A delivery company considered extending its evening service across the entire region.",
        "Because demand outside the city was uncertain, the operations team proposed a limited trial in two towns.",
        "The trial showed strong weekend demand but little weekday use, allowing the timetable to be adjusted before expansion.",
        "Management therefore approved a targeted service rather than treating the initial proposal as an all-or-nothing decision.",
      ],
      question: "What did the trial make possible?",
      answers: [
        "An expansion shaped by observed demand.",
        "Proof that every town had identical needs.",
        "A guarantee of profit in every season.",
        "The elimination of all evening deliveries.",
      ],
      evidence:
        "Observed weekend and weekday differences informed a more targeted timetable.",
    },
    {
      title: "Beyond attendance",
      topic: "Education",
      paragraphs: [
        "An adult learning centre initially evaluated its courses by counting registrations.",
        "Tutors pointed out that registration alone revealed little about whether learners could use the new skills.",
        "Consequently, the centre added practical tasks and follow-up conversations to its evaluation.",
        "Attendance remained useful, but it was now interpreted alongside evidence of learning rather than as a substitute for it.",
      ],
      question: "How did the meaning of attendance data change?",
      answers: [
        "It became one indicator among several.",
        "It became conclusive proof of competence.",
        "It was treated as always inaccurate.",
        "It replaced all practical assessment.",
      ],
      evidence:
        "Attendance was retained but combined with tasks and follow-up evidence.",
    },
    {
      title: "Keeping the service running",
      topic: "Community",
      paragraphs: [
        "A volunteer-run transport service received a generous one-off donation after a local appeal.",
        "Although the donation covered immediate repairs, the committee recognised that fuel and insurance were recurring expenses.",
        "It therefore sought a modest monthly contribution from several partner organisations.",
        "That arrangement reduced reliance on occasional appeals and made future operating costs easier to plan.",
      ],
      question: "What problem did the partnership address?",
      answers: [
        "The need for continuing, predictable support.",
        "A complete absence of immediate funding.",
        "A decision to stop transporting passengers.",
        "The requirement to return the donation.",
      ],
      evidence:
        "Recurring expenses required reliable funding beyond the one-off donation.",
    },
  ],
  [
    {
      title: "Cooling the high street",
      topic: "Environment",
      paragraphs: [
        "A town proposed planting trees along its busiest shopping street to reduce summer heat.",
        "An engineering review supported the aim but warned that roots and underground cables could conflict in some locations.",
        "The council responded with a mixed plan: trees where space permitted and shade structures elsewhere.",
        "By adapting the method rather than abandoning the objective, the revised proposal addressed a physical constraint while preserving most of the expected benefit.",
      ],
      question: "Which best describes the revision?",
      answers: [
        "A flexible means of pursuing the original objective.",
        "A rejection of the need for shade.",
        "Proof that underground cables can be ignored.",
        "A promise that every tree would survive.",
      ],
      evidence:
        "The council combined methods to retain shade despite location-specific constraints.",
    },
    {
      title: "The convenience trade-off",
      topic: "Work",
      paragraphs: [
        "A workplace introduced an online-only system for booking equipment, expecting it to save administrative time.",
        "Early feedback showed that staff working away from computers were having difficulty making bookings.",
        "In response, the team kept the online system but added a telephone option during staffed hours.",
        "The additional channel reduced some of the efficiency savings, yet prevented convenience for one group from becoming exclusion for another.",
      ],
      question: "What trade-off was accepted?",
      answers: [
        "Some efficiency was sacrificed to improve access.",
        "All digital booking was removed to cut costs.",
        "Telephone access was blocked to save time.",
        "Administrative effort disappeared entirely.",
      ],
      evidence:
        "The telephone channel improved access while reducing some anticipated savings.",
    },
    {
      title: "A measure that changed behaviour",
      topic: "Education",
      paragraphs: [
        "A reading programme rewarded classes solely for the number of books completed.",
        "Teachers noticed that some pupils began selecting only very short books, even when longer texts matched their interests.",
        "The programme then introduced reading discussions and personal goals alongside completion counts.",
        "This broader approach aimed to discourage gaming the measure without suggesting that finishing books was itself undesirable.",
      ],
      question: "Why were additional measures introduced?",
      answers: [
        "The original incentive encouraged a narrow strategy.",
        "Short books were banned from the school.",
        "Teachers wanted pupils to stop finishing books.",
        "Every pupil had lost interest in reading.",
      ],
      evidence:
        "Counting completions alone encouraged selection of short books to maximise rewards.",
    },
  ],
  [
    {
      title: "The consultation gap",
      topic: "Community",
      paragraphs: [
        "A council survey found considerable support for replacing a car park with a public square.",
        "Before interpreting the result as a community-wide consensus, analysts examined who had responded.",
        "They discovered that most responses came from people already using the council's online services, so they organised additional in-person sessions.",
        "The supplementary consultation did not invalidate the original responses; it addressed the possibility that an important group had been underrepresented.",
      ],
      question: "What was the main concern about the survey?",
      answers: [
        "Its respondents might not represent the whole community.",
        "Every original response was deliberately false.",
        "A public square was technically impossible.",
        "Only car owners were allowed to respond.",
      ],
      evidence:
        "The council broadened participation after finding that online-service users dominated responses.",
    },
    {
      title: "A contingency, not a prediction",
      topic: "Work",
      paragraphs: [
        "An event organiser reserved a second venue after learning that repairs at the main hall might overrun.",
        "Some sponsors interpreted the reservation as confirmation that the hall would remain closed.",
        "The organiser clarified that the extra booking was a precaution against uncertainty, not evidence about the builders' progress.",
        "With that distinction understood, preparations continued at both locations until an inspection established which could safely be used.",
      ],
      question: "What did the second reservation signify?",
      answers: [
        "Preparation for a possible disruption.",
        "Proof that the repairs had failed.",
        "A confirmed decision to use both venues.",
        "Evidence that no inspection was needed.",
      ],
      evidence:
        "The organiser explicitly distinguished a precaution from a prediction.",
    },
    {
      title: "Interpreting a discrepancy",
      topic: "Research",
      paragraphs: [
        "Two teams reported different rates of bicycle use in the same district.",
        "Rather than assuming that one team had made an error, a reviewer compared the sampling periods and definitions.",
        "One study counted weekday commuting while the other included weekend leisure trips, accounting for much of the discrepancy.",
        "Once these methodological differences were documented, the findings could be compared more meaningfully without forcing them into artificial agreement.",
      ],
      question: "What helped explain the apparent conflict?",
      answers: [
        "The studies measured different patterns of use.",
        "Both teams counted only identical journeys.",
        "All transport research is unreliable.",
        "One report was withdrawn before review.",
      ],
      evidence:
        "Different periods and definitions explained much of the difference.",
    },
  ],
  [
    {
      title: "What the evidence can support",
      topic: "Research",
      paragraphs: [
        "A pilot study found that participants using a new study tool improved their test scores over six weeks.",
        "Because participants had volunteered and no comparison group was included, the researchers could not isolate the tool's contribution from motivation or extra practice.",
        "They reported the improvement while explicitly limiting the causal claim and proposing a controlled follow-up study.",
        "This caution preserved the observed finding without treating an encouraging association as conclusive evidence of effectiveness.",
      ],
      question: "Which conclusion is best supported?",
      answers: [
        "Scores improved, but the cause remains uncertain.",
        "The tool alone certainly caused the improvement.",
        "Volunteering prevents any useful observation.",
        "A comparison group had already disproved the tool.",
      ],
      evidence:
        "Improvement was observed, but the design could not isolate its cause.",
    },
    {
      title: "Who bears the burden?",
      topic: "Community",
      paragraphs: [
        "A flat fee for collecting bulky household waste appeared to distribute costs equally because every household would pay the same amount.",
        "A distributional review, however, showed that the fee represented a substantially larger share of income for poorer households.",
        "The council consequently considered reduced charges for eligible residents while retaining a general contribution towards service costs.",
        "The debate therefore concerned proportional impact rather than a dispute over whether the service incurred any costs at all.",
      ],
      question: "What distinction shaped the debate?",
      answers: [
        "Equal payments need not create equal burdens.",
        "All households had identical disposable incomes.",
        "The waste service could operate without costs.",
        "Reduced charges would eliminate every contribution.",
      ],
      evidence:
        "The same fee consumed a different proportion of household income.",
    },
    {
      title: "A qualified endorsement",
      topic: "Work",
      paragraphs: [
        "A review panel endorsed a proposed transport interchange in principle, citing its potential to improve connections between routes.",
        "Its endorsement was conditional on an accessible pedestrian design and publication of a credible maintenance plan.",
        "When publicity described the scheme as having received unconditional approval, the panel requested a correction.",
        "The correction did not reverse the panel's support; it restored the qualifications that had been essential to that support from the outset.",
      ],
      question: "Why did the panel request a correction?",
      answers: [
        "Publicity omitted the conditions attached to its support.",
        "The panel had rejected the entire proposal.",
        "The maintenance plan was guaranteed to be unnecessary.",
        "Accessible design had been forbidden.",
      ],
      evidence:
        "The panel supported the scheme subject to conditions, rather than unconditionally.",
    },
  ],
  [
    {
      title: "Independent corroboration",
      topic: "Research",
      paragraphs: [
        "Several articles appeared to corroborate an early report about a company's environmental performance.",
        "A subsequent source audit revealed that all of them relied on the same unpublished statement supplied by the company itself.",
        "The apparent multiplicity of confirmations therefore added little independent evidential weight, despite giving readers an impression of widespread verification.",
        "Reviewers sought separately collected measurements before deciding whether the original claim was substantiated, distinguishing repetition of a source from genuine corroboration.",
      ],
      question: "Why were the additional articles insufficient corroboration?",
      answers: [
        "They repeated one underlying source rather than adding independent evidence.",
        "Published articles can never contain reliable information.",
        "The original claim had already been conclusively disproved.",
        "Independent measurements had confirmed every detail.",
      ],
      evidence: "Multiple articles depended on a single company statement.",
    },
    {
      title: "A useful but incomplete measure",
      topic: "Education",
      paragraphs: [
        "A university found that graduates reporting high employment rates were not necessarily those expressing the greatest satisfaction with their courses.",
        "The divergence prompted administrators to examine how job quality, further study and students' original aspirations interacted with the headline employment measure.",
        "Rather than discarding the measure, they retained it within a wider evaluation that made its limitations explicit.",
        "The resulting account resisted both the claim that employment data were irrelevant and the assumption that a single outcome could capture the entire educational experience.",
      ],
      question: "What position did the university adopt?",
      answers: [
        "Retain a useful indicator while rejecting its sufficiency on its own.",
        "Treat employment rates as the only meaningful outcome.",
        "Conclude that graduate experiences cannot be examined.",
        "Remove every outcome measure because they differed.",
      ],
      evidence:
        "Employment data remained part of a wider evaluation rather than a complete account.",
    },
    {
      title: "The risk of a premature remedy",
      topic: "Environment",
      paragraphs: [
        "A district experiencing water shortages considered an immediate restriction on all public garden use.",
        "An assessment suggested that poorly maintained supply pipes accounted for a substantial share of losses, while garden demand varied considerably by season.",
        "Officials prioritised repairs and introduced targeted restrictions during peak demand, monitoring whether either intervention displaced rather than reduced consumption.",
        "This staged response acknowledged urgency while avoiding the assumption that the most visible form of water use was necessarily the principal source of the problem.",
      ],
      question: "What assumption did the response challenge?",
      answers: [
        "The most visible use must be the main cause of the shortage.",
        "Urgent problems should never prompt intervention.",
        "Seasonal variation makes monitoring pointless.",
        "Pipe repairs cannot affect water supply.",
      ],
      evidence:
        "Officials considered pipe losses and seasonal demand rather than focusing only on visible gardens.",
    },
  ],
  [
    {
      title: "Reconciling competing objectives",
      topic: "Community",
      paragraphs: [
        "A proposal to convert a disused warehouse into housing was welcomed for addressing a shortage but challenged for threatening affordable workspace used by local artists.",
        "Treating the dispute as a choice between development and preservation obscured the possibility that the building could accommodate both, albeit with fewer units and a less straightforward financing arrangement.",
        "A revised proposal reserved part of the ground floor for workspaces and subjected the resulting financial projections to independent review.",
        "Its merit lay not in eliminating every tension, but in making the remaining trade-offs explicit enough for residents to judge whether the compromise was proportionate to the benefits claimed.",
      ],
      question: "What is the central argument of the passage?",
      answers: [
        "An explicit, scrutinised compromise can be more informative than a false binary choice.",
        "Every competing objective can be satisfied without sacrifice.",
        "Independent review guarantees that a development will succeed.",
        "Preserving workspace necessarily rules out any additional housing.",
      ],
      evidence:
        "The revision combined uses while exposing, rather than denying, its trade-offs.",
    },
    {
      title: "The limits of an apparent consensus",
      topic: "Research",
      paragraphs: [
        "A review described a scientific debate as settled because a large majority of the included papers reached broadly similar conclusions.",
        "Closer examination showed that the papers shared a narrow set of assumptions and frequently reused a single dataset, limiting the independence and scope of that agreement.",
        "The reviewer revised the synthesis to distinguish consistency within that framework from robustness across alternative frameworks and observations.",
        "Such a distinction neither refuted the prevailing account nor placed every competing explanation on an equal footing; it specified the boundaries within which the evidence currently justified confidence.",
      ],
      question: "Which interpretation best reflects the revised synthesis?",
      answers: [
        "Agreement supports confidence within limits that still require examination.",
        "Any shared assumption makes all findings false.",
        "All competing explanations have equal evidential support.",
        "Counting papers alone establishes universal validity.",
      ],
      evidence:
        "The synthesis distinguished agreement within a shared framework from broader robustness.",
    },
    {
      title: "An exception and its implications",
      topic: "Work",
      paragraphs: [
        "An organisation defended a rigid procurement rule on the grounds that uniform application prevented favouritism and made decisions easier to audit.",
        "When an unusual equipment failure threatened a time-sensitive public service, staff argued that waiting for the standard process would impose costs far exceeding the safeguards it offered in that particular case.",
        "An independently documented exception allowed an urgent purchase, while the subsequent review considered whether the case exposed a defect in the rule or merely demonstrated why exceptional discretion sometimes remained necessary.",
        "The review resisted drawing a general conclusion from the outcome alone, asking instead whether the departure had been justified by information available when the decision was made.",
      ],
      question: "What criterion did the review emphasise?",
      answers: [
        "Whether the exception was justified by evidence available at the time.",
        "Whether a favourable outcome automatically justified any departure.",
        "Whether one exception proved the rule should always be abandoned.",
        "Whether documentation could replace all scrutiny of the decision.",
      ],
      evidence:
        "The review evaluated the decision using contemporaneous information rather than outcome alone.",
    },
  ],
];
export const readingChallenges: Challenge[] = levels.flatMap(
  (readings, index) =>
    readings.map((reading, item) => {
      const level = index + 1;
      const id = `game-reading-question-${level}-${item}-v1`;
      const choices = shuffle(reading.answers, id).map((label, position) => ({
        id: `option-${position}`,
        label,
      }));
      return {
        id,
        kind: "reading-question",
        track: "reading",
        level,
        audioText: "",
        topic: reading.topic,
        title: reading.title,
        instruction: reading.question,
        passage: reading.paragraphs.join("\n\n"),
        choices,
        answer: choices.find((choice) => choice.label === reading.answers[0])!
          .id,
        explanation: reading.evidence,
      };
    }),
);
