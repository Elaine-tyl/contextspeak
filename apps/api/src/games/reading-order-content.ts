import type { Challenge } from "./catalog";
import { shuffle } from "./new-modes";

// These original ordering texts are NOT published as comprehension passages.
// Each group is a level; within it are three independent, coherent sequences.
const levels: string[][][] = [
  [
    [
      "Ella found a recipe for vegetable soup and wrote down the ingredients.",
      "With that list in her bag, she went to the market and bought what she needed.",
      "Back at home, she followed the recipe and served the soup for dinner.",
    ],
    [
      "Ben opened the parcel and found a lamp inside.",
      "He placed the lamp on his desk and plugged it into the wall.",
      "When he pressed its switch, the room filled with warm light.",
    ],
    [
      "Lina checked the cinema times and chose an evening film.",
      "She bought a ticket for that showing and met her brother outside the cinema.",
      "Together, they found their seats just before the film began.",
    ],
  ],
  [
    [
      "A notice announced that the swimming pool would close for cleaning on Tuesday.",
      "Having read it, Tom changed his usual Tuesday lesson to Wednesday.",
      "He returned the following day to find clean water and the pool open again.",
    ],
    [
      "Priya realised that her library card was missing when she reached the borrowing desk.",
      "She explained the problem to a librarian, who checked her account using another form of identification.",
      "After the check, the librarian issued a replacement card so she could borrow the books.",
    ],
    [
      "Anita compared two bus routes before leaving for the exhibition.",
      "She chose the route with fewer stops, even though its stop was farther from her house.",
      "The extra walk was worthwhile: she reached the exhibition before the doors opened.",
    ],
  ],
  [
    [
      "A resident reported a broken light in the communal hallway.",
      "The building manager checked the report and arranged for an electrician to visit.",
      "During that visit, the electrician discovered a faulty connection and repaired it.",
      "The manager then confirmed with the resident that the hallway was properly lit.",
    ],
    [
      "A class decided to raise money by selling homemade cakes.",
      "Before advertising the sale, they agreed how to label ingredients and handle the money.",
      "With those arrangements in place, they invited families to attend on Friday.",
      "After the sale, the class counted the proceeds and sent the donation to the charity.",
    ],
    [
      "A garden club noticed that its newest plants were wilting despite regular watering.",
      "Members examined the soil and found that water was collecting around the roots.",
      "To improve drainage, they moved the plants into raised beds.",
      "The plants recovered, showing that adding more water had not been the solution.",
    ],
  ],
  [
    [
      "A museum planned an evening event to attract visitors unable to attend during working hours.",
      "Staff first consulted those potential visitors about transport and preferred opening times.",
      "Their responses led to an earlier finish that allowed people to catch the last bus home.",
      "The final schedule therefore reflected access needs rather than simply extending the normal timetable.",
    ],
    [
      "A workshop wanted to reduce the time spent searching for shared tools.",
      "Workers mapped where each tool was normally used and identified common sources of delay.",
      "Using that map, they created labelled storage areas beside the relevant workstations.",
      "A later review found shorter searches, although returning tools consistently remained important.",
    ],
    [
      "A neighbourhood newsletter received complaints that announcements were arriving too late to be useful.",
      "Its editors traced the delays to waiting for every contributor before preparing an issue.",
      "They introduced a fixed submission deadline and moved late material into the next edition.",
      "Publication became more predictable because the schedule no longer depended on the final contributor.",
    ],
  ],
  [
    [
      "A retailer considered replacing all reusable crates with a lighter disposable alternative.",
      "Before deciding, it compared not only purchase prices but also collection, cleaning and disposal costs.",
      "The comparison showed that reusable crates remained cheaper on routes with reliable returns.",
      "The retailer consequently retained them on those routes instead of applying one policy everywhere.",
    ],
    [
      "A tutoring service found that learners often abandoned online sessions after encountering technical problems.",
      "Follow-up interviews suggested that the difficulty was obtaining help quickly, not a lack of interest in the lessons.",
      "The service introduced a simple support channel available during every session.",
      "Attendance improved after the change, giving the team a reason to examine support provision alongside lesson content.",
    ],
    [
      "A village group wanted to restore a walking path but could not afford the full proposed route.",
      "It asked residents which sections connected the most important destinations.",
      "Those priorities shaped a phased plan, beginning with the link between the school and the bus stop.",
      "The limited budget thus delayed part of the project without preventing a useful first stage.",
    ],
  ],
  [
    [
      "A restaurant introduced larger portions after several customers described its meals as poor value.",
      "Subsequent monitoring showed more food waste but no clear improvement in customer satisfaction.",
      "Interviews then revealed that customers had been concerned about inconsistent quality rather than quantity.",
      "The restaurant redirected its effort towards preparation standards, recognising that its initial remedy had addressed the wrong interpretation of the complaint.",
    ],
    [
      "A coastal town proposed brighter lighting along a popular evening path.",
      "An ecological review warned that constant illumination could disrupt animals using the adjacent dunes.",
      "Designers responded with shielded lights activated only when walkers approached.",
      "The revised scheme sought to preserve safe access while limiting exposure, rather than assuming that either objective had to be abandoned.",
    ],
    [
      "A company offered a prize for the highest number of customer calls handled in an hour.",
      "Supervisors soon noticed shorter calls followed by repeated contacts about unresolved issues.",
      "They replaced the single target with a combination of resolution checks and response times.",
      "The change acknowledged that faster handling was useful only when it did not undermine the purpose of the conversation.",
    ],
  ],
  [
    [
      "A student survey appeared to show that a new timetable had reduced attendance.",
      "Analysts checked whether the comparison weeks were otherwise similar.",
      "They found that the later period included a transport strike, which affected some campuses far more than others.",
      "Their revised report therefore treated the timetable's effect as uncertain rather than attributing the entire decline to it.",
    ],
    [
      "A community fund received more eligible applications than its budget could support.",
      "The selection panel published criteria describing how urgency, expected benefit and alternative funding would be weighed.",
      "Applicants could then request an explanation of how those criteria had been applied to their case.",
      "This did not remove disappointment, but it made the basis of the decisions more transparent and open to challenge.",
    ],
    [
      "A hospital's visitor map was redesigned to reduce the number of people asking staff for directions.",
      "The design team initially tested it only with people already familiar with the building.",
      "A second test involving first-time visitors exposed confusing abbreviations that the first group had understood automatically.",
      "Revising those labels showed why familiarity with a setting could conceal problems faced by newcomers.",
    ],
  ],
  [
    [
      "A report credited a recycling campaign with a substantial fall in household waste.",
      "Reviewers noted that the measurement period also included a change in collection frequency.",
      "They requested separate estimates of the two changes rather than accepting a single explanatory story.",
      "The revised analysis preserved evidence of improvement while acknowledging that the campaign's contribution could not yet be isolated.",
    ],
    [
      "A city promoted a digital permit system as accessible because it was available at any hour.",
      "An access review distinguished availability from usability, documenting barriers faced by residents without suitable devices or digital skills.",
      "On that basis, the city retained assisted applications at staffed centres alongside the digital route.",
      "The additional route challenged the assumption that removing opening-hour restrictions alone made a service universally accessible.",
    ],
    [
      "A researcher proposed a demanding screening test intended to minimise the chance of admitting an unsuitable candidate.",
      "Colleagues asked how many suitable candidates the same threshold would exclude.",
      "Their question prompted a comparison of both types of error and the consequences attached to each.",
      "The eventual recommendation balanced those consequences explicitly instead of treating a lower rate of one error as an unqualified improvement.",
    ],
  ],
  [
    [
      "A conservation programme reported success after the number of protected nesting sites increased.",
      "An external review asked whether additional sites had produced more surviving young or merely shifted nesting from nearby unmonitored areas.",
      "Answering that question required a wider sampling boundary and several seasons of follow-up observations.",
      "The programme retained the original count as an operational measure while suspending its stronger claim about population recovery.",
    ],
    [
      "A public agency defended an algorithm because its recommendations were consistent across repeated runs.",
      "Auditors distinguished that reproducibility from validity: a system could apply the same inappropriate rule with perfect consistency.",
      "They therefore compared recommendations with independently assessed cases and examined errors across different groups.",
      "This evaluation addressed what the agency's original defence had left unresolved, namely whether dependable repetition also produced defensible decisions.",
    ],
    [
      "A review of local journalism found many reports using similar language to describe a proposed development.",
      "Tracing the wording revealed that most had reproduced material from the same promotional briefing.",
      "The reviewers consequently treated the coverage as evidence of message distribution rather than independent agreement about the project's merits.",
      "They sought separately gathered accounts before drawing any conclusion about the strength or breadth of public support.",
    ],
  ],
  [
    [
      "A policy review associated flexible working with improved staff retention across several organisations.",
      "Its authors recognised that organisations adopting such policies might also differ in management quality, pay or recruitment practices.",
      "Rather than claiming a universally transferable effect, they examined which contextual features made the association stronger or weaker.",
      "That analysis framed flexibility as a potentially valuable intervention whose likely contribution depended on conditions, not as an isolated explanation capable of accounting for every observed difference.",
      "The resulting recommendation combined cautious implementation with evaluation designed to distinguish the policy's influence from those accompanying characteristics.",
    ],
    [
      "A planning committee argued that identical compensation payments would demonstrate impartiality towards households displaced by a transport project.",
      "Consultation revealed that the same amount would not secure comparable housing for households with different access needs and local commitments.",
      "The committee accordingly distinguished procedural uniformity from substantive adequacy, considering adjustments linked to documented needs.",
      "Critics could still dispute the chosen criteria, but the revised proposal made clear which conception of fairness it was attempting to serve.",
      "Its justification thus depended on the defensibility of those criteria rather than on the apparent simplicity of paying everyone the same sum.",
    ],
    [
      "An experiment failed to reproduce a previously reported effect under a modified set of conditions.",
      "One interpretation treated the failure as sufficient grounds to dismiss the earlier result entirely.",
      "A more discriminating analysis asked whether the modification had removed an incidental feature or altered a condition essential to the proposed mechanism.",
      "The researchers designed a further comparison to separate these possibilities, declining to treat either successful repetition or its absence as self-interpreting.",
      "Their approach located confidence in the explanatory pattern across tests rather than in the outcome of one attempt considered in isolation.",
    ],
  ],
];
export const readingOrderChallenges: Challenge[] = levels.flatMap(
  (texts, index) =>
    texts.map((paragraphs, item) => {
      const level = index + 1;
      const id = `game-reading-order-${level}-${item}-v1`;
      let tiles = shuffle(paragraphs, id);
      if (tiles.every((text, position) => text === paragraphs[position]))
        tiles = [...tiles.slice(1), tiles[0]];
      return {
        id,
        kind: "reading-order",
        track: "reading",
        level,
        audioText: "",
        topic:
          level < 4
            ? "Everyday sequences"
            : level < 8
              ? "Causes and consequences"
              : "Arguments and evidence",
        title: "Put the ideas in order",
        instruction:
          "Arrange the paragraphs into a coherent text. Tap a placed paragraph to undo it.",
        tiles,
        answer: paragraphs.join(" "),
        explanation:
          "Follow the sequence of events, references such as 'those' and 'this', and the connections between a problem, evidence and a response.",
      };
    }),
);
