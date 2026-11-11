// Historical v1 content is kept only for saved attempts and replay.
export const legacyGapSentences = [
  ["I'd like [tea], please.", "Let's meet at [noon].", "The [bus] is here."],
  [
    "A small [coffee], please.",
    "Are you [free] tomorrow?",
    "Turn [left] at the park.",
  ],
  [
    "Could I get this to [go], please?",
    "Let's meet [outside] the library.",
    "The bus stop is [across] the street.",
  ],
  [
    "Could I have [oat] milk [instead]?",
    "Can we [meet] after [lunch]?",
    "Is this the [right] bus to the [station]?",
  ],
  [
    "Could you make it [decaf] and leave out the [syrup]?",
    "I'm [running] late, so please [start] without me.",
    "Does this [ticket] include the [return] trip?",
  ],
  [
    "I ordered an [iced] latte, but this one is [hot].",
    "Would you [mind] if we moved our meeting to [Thursday]?",
    "The next [train] leaves from the other [platform].",
  ],
  [
    "Could you [check] whether this [pastry] contains [peanuts]?",
    "If the [weather] gets worse, we could [watch] a movie [instead].",
    "I [missed] my connection because the [first] bus was [delayed].",
  ],
  [
    "I was [charged] for a large, but I [ordered] a [medium].",
    "I'm [happy] to go, as [long] as we're back before [dinner].",
    "Would you [recommend] walking, or is there a [quicker] way to get [downtown]?",
  ],
  [
    "Could you [confirm] whether the [discount] still [applies] if I use my own [cup]?",
    "I can [cover] the tickets [today], and you can [pay] me back [later].",
    "If the [service] is [canceled], can I use this [ticket] on another [route]?",
  ],
  [
    "I don't [mind] waiting, but could you [check] the order and give me a [rough] idea of how much [longer] it'll be?",
    "I'd [rather] keep our plans [flexible], so let's [decide] once we know what the [forecast] looks like.",
    "My [reservation] says the fare is [refundable], so could you [explain] why there's a [charge]?",
  ],
];

// v2 is immutable too: existing attempts retain the exact words they heard.
export const previousGapSentences = [
  [
    "Could I get a [coffee] with [oat] milk to go, please?",
    "Let's [meet] [outside] the library after lunch, if that works for you.",
    "Does this [bus] go directly to the [station], or should I change?",
    "The [registration] desk is on the first floor, opposite the [library].",
  ],
  [
    "I ordered a [small] coffee, but I think you've charged me for a [large].",
    "We could meet on [Thursday] instead, because I'm working late on [Tuesday].",
    "Please keep your [ticket] until you leave the [station], as staff may check it.",
    "The [seminar] begins at nine, and students should bring a [notebook] for the discussion.",
  ],
  [
    "Could you [check] whether the [sandwich] contains [nuts] before I place my order?",
    "If the [weather] improves, let's meet [outside] and walk to the [market] together.",
    "Your [platform] has changed, so follow the [signs] and keep your [ticket] ready.",
    "Students must [register] for the [workshop] before Friday to receive the [materials] in advance.",
  ],
  [
    "I'd [prefer] [oat] milk [instead] of regular milk, but only if there isn't an extra charge.",
    "Could we [postpone] the meeting until [Thursday], since the [report] will not be ready before then?",
    "The [service] is running behind [schedule], so allow extra [time] if you need to change trains.",
    "The [survey] should include [participants] from different age groups so that the [results] reflect a wider range of experiences.",
  ],
  [
    "Although the [menu] lists [almond] milk, the barista [confirmed] that it was [unavailable] when I ordered my drink.",
    "I've [booked] a table for [Saturday], but we can [cancel] it if the weather is good enough for a [picnic].",
    "Passengers should [check] the departure [board] before leaving the [waiting] area, because the platform may change without an [announcement].",
    "The [researchers] compared [samples] from two nearby [rivers] to identify differences in the level of [pollution].",
  ],
  [
    "Before I [pay], could you check whether the [discount] applies to the [whole] order or only to the [drinks]?",
    "I'm [happy] to help with the [presentation], provided we [agree] on the main points before we start designing the [slides].",
    "Because the [bridge] is closed for [repairs], drivers are advised to use an [alternative] route and allow an extra [hour].",
    "Each [participant] completed a [questionnaire] before the experiment, which allowed the team to compare their initial [expectations] with the final [outcomes].",
  ],
  [
    "The [receipt] includes a [service] charge that wasn't mentioned on the [menu], so I'd like an [explanation] before we [settle] the bill.",
    "We should [reserve] the smaller [venue] for now, since the [manager] said we could [upgrade] later if more people [confirm] they are coming.",
    "Although the [flight] was [delayed], the airline arranged [accommodation] for passengers who missed their [connections] and couldn't continue until the following [morning].",
    "The [findings] suggest a [correlation] between sleep and [concentration], although the [researchers] warned that the study did not establish a causal [relationship].",
  ],
  [
    "I [understand] that the kitchen is [busy], but we [ordered] nearly forty minutes ago and would [appreciate] a realistic [estimate] of the remaining wait.",
    "If the [funding] isn't [approved] by the end of the week, we may need to [revise] the [schedule] rather than [abandon] the project entirely.",
    "The [reservation] includes a [refundable] fare, but any [difference] in the ticket price must still be paid if you [reschedule] your journey at short [notice].",
    "While the [intervention] improved attendance in the short term, its [effectiveness] depended on [consistent] support from teachers and the [availability] of suitable [resources].",
  ],
  [
    "Could you [clarify] whether the [surcharge] is [optional], because the advertised [price] appeared to include [service], and I'd like to [understand] the total before I pay?",
    "Given the [uncertainty] around the [forecast], I'd [suggest] keeping both [options] open until Friday, when we should have enough [information] to make a [decision].",
    "Before [purchasing] the replacement ticket, I asked whether the original [booking] could be [transferred], since the [disruption] had been caused by a [technical] fault rather than a change in my [plans].",
    "The [analysis] revealed substantial [variation] across regions, but the [authors] emphasized that differences in [measurement] methods could have influenced the [estimates] and should be considered before drawing [conclusions].",
  ],
  [
    "While I [appreciate] the offer of a [replacement], I'd [prefer] a refund because we need to leave [shortly], and the meal we [received] was substantially different from what the menu [described].",
    "Unless we can [demonstrate] that the proposed [changes] will improve [accessibility] without increasing the overall [cost], the committee is unlikely to [approve] the revised plan before the current [deadline].",
    "Although the [operator] initially [refused] to reimburse the cost of an [alternative] journey, I was advised to [retain] every receipt and submit a [formal] claim once the cause of the [cancellation] had been confirmed.",
    "The [evaluation] acknowledges several [limitations], including the [absence] of a control group and the [possibility] of selection bias, which means that any [recommendations] should be treated as [provisional] until the findings are replicated.",
  ],
];
