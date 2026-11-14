using System;
using System.Linq;
namespace ContextSpeak.Proactive
{
    public static class ProactiveEventContent
    {
        public static LifeEventDefinition[] All() => new[] {
            Event("collision", "A little accident", "Oh! I'm so sorry. I didn't see you there. Are you alright?", "Reply to the apology and check that everyone is alright. Natural alternatives are welcome.", "I'm fine, thanks. Are you okay?", new[] { "city", "cafe", "mall" }, "map", 100),
            Event("cafe-spill", "Spilled coffee", "Oh no, I've spilled my coffee! Are you alright? Did any get on you?", "Respond to the spilled coffee and say whether you are alright.", "I'm okay, thanks. Let me help you clean that up.", new[] { "cafe" }, "cup", 80),
            Event("tourist-help", "Tourist assistance", "Excuse me, do you know how to get to Forest Café from here?", "Give a plausible route to Forest Café, or honestly ask to check a map together.", "Let's check the map together. I think it's just past the fountain.", new[] { "city" }, "map"),
            Event("home-invitation", "Dinner together", "Hey, would you like to help me make dinner tonight?", "Accept or decline the invitation politely and explain your preference.", "I'd love to help. What shall we make?", new[] { "home" }),
            Event("school-help", "Homework catch-up", "Hey, did you catch what we're supposed to do for tomorrow?", "Explain what you understood or ask to check the homework instructions together.", "I'm not completely sure. Shall we check the instructions together?", new[] { "school" }),
            Event("shopping-mistake", "The wrong size", "I think I bought the wrong size. Could you help me ask about an exchange?", "Offer help with exchanging the wrong size or suggest checking the return policy.", "Sure. Let's ask whether you can exchange it with the receipt.", new[] { "mall" }),
            Event("reach-item", "A little help", "Excuse me, could you help me reach that cereal on the shelf?", "Respond safely to the request; offering to ask staff is also valid.", "Of course. If it's too high, we can ask a member of staff.", new[] { "supermarket" }),
            Event("flight-delay", "Plans have changed", "They've just delayed our flight. Do you know where we can get an update?", "Suggest checking a flight display or asking airline staff, without inventing a real flight status.", "Let's check the display and ask the airline staff what our options are.", new[] { "airport" }),
            Event("restaurant-seat", "A seat nearby", "Excuse me, is anyone sitting here?", "Answer whether the seat is free or explain politely that it is taken.", "It's free. You're welcome to sit here.", new[] { "restaurant" }),
            Event("hospital-directions", "Finding reception", "Excuse me, do you know where reception is?", "Offer help finding reception or a staff member. Do not give medical advice.", "I'm not sure, but let's ask that member of staff.", new[] { "hospital" }),
            Event("beach-belongings", "A lost bag", "I can't find my blue bag. Have you seen it around here?", "Respond honestly and suggest a safe way to find lost belongings.", "I haven't seen it. Shall we check with the lifeguard?", new[] { "beach" }),
            Event("zoo-meeting", "Meeting friends", "My friends are waiting near the entrance. Could you help me find it?", "Suggest checking signs or asking zoo staff for the entrance.", "Let's follow the signs, or ask a member of staff for directions.", new[] { "zoo" }),
        };
        private static LifeEventDefinition Event(string id, string title, string prompt, string goal, string model, string[] locations, string prop = "", int priority = 10)
            => new LifeEventDefinition { Id = id, Title = title, Prompt = prompt, Goal = goal, Model = model, Locations = locations, Prop = prop, Priority = priority };
        public static LifeEventDefinition Find(string id) => All().FirstOrDefault(e => e.Id == id);
    }
}
