using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ContextSpeak.Life
{
    public enum AnimalState
    {
        Idle, Walking, Running, Eating, Drinking, Resting, Sleeping, LookingAround, Socializing, Playing, Swimming, Grooming, ReturningToHabitat,
    }

    /// One species: how it moves, what it needs, what it is allowed to do, and what visitors learn about it.
    /// The need rates are simplified game values (per real minute), not animal-welfare science.
    public sealed class AnimalSpecies
    {
        public string Id, Name, Plural, Model;
        public float WalkSpeed = 1, RunSpeed = 3, TurnSpeed = 60, Radius = 1, Height = 1, Stride = 1;
        public float Hunger = 0.06f, Thirst = 0.05f, Tiredness = 0.03f, Social = 0.04f, Activity = 0.5f;
        public bool Herd, Browser, RigidLegs, Placeholder;
        public float LegSwing = 22, HeadDrop = 40, NeckLift;          // degrees for the procedural animation
        public AnimalState[] Behaviours = { AnimalState.Idle, AnimalState.Walking, AnimalState.Eating, AnimalState.Drinking, AnimalState.Resting, AnimalState.LookingAround };
        public string Diet = "", Home = "", Status = "", Fact = "", Describe = "";
        public string[] Vocabulary = new string[0];
        public bool Can(AnimalState state) => state == AnimalState.ReturningToHabitat || Array.IndexOf(Behaviours, state) >= 0;
    }

    public static class AnimalCatalog
    {
        private static readonly AnimalState[] Grazer =
        {
            AnimalState.Idle, AnimalState.Walking, AnimalState.Running, AnimalState.Eating, AnimalState.Drinking, AnimalState.Resting,
            AnimalState.Sleeping, AnimalState.LookingAround, AnimalState.Socializing, AnimalState.Grooming,
        };
        private static readonly AnimalState[] Heavy =
        {
            AnimalState.Idle, AnimalState.Walking, AnimalState.Eating, AnimalState.Drinking, AnimalState.Resting, AnimalState.Sleeping,
            AnimalState.LookingAround, AnimalState.Socializing, AnimalState.Playing, AnimalState.Grooming,
        };
        private static readonly AnimalState[] Farm =
        {
            AnimalState.Idle, AnimalState.Walking, AnimalState.Eating, AnimalState.Drinking, AnimalState.Resting, AnimalState.Sleeping,
            AnimalState.LookingAround, AnimalState.Socializing,
        };

        public static readonly AnimalSpecies[] All =
        {
            new AnimalSpecies
            {
                Id = "elephant", Name = "African elephant", Plural = "elephants", Model = "Elephant", WalkSpeed = 1.1f, RunSpeed = 2.4f, TurnSpeed = 28, Radius = 2.4f, Height = 3.1f, Stride = 2.2f,
                Hunger = 0.07f, Thirst = 0.07f, Tiredness = 0.02f, Social = 0.06f, Activity = 0.45f, Herd = true, LegSwing = 20, HeadDrop = 22, Behaviours = Heavy,
                Diet = "Grass, leaves, bark and fruit — up to 150 kg a day", Home = "Savannahs and forests of Africa", Status = "Endangered",
                Fact = "An elephant's trunk has about 40,000 muscles. It is used to breathe, smell, drink, grab food and greet friends.",
                Describe = "Elephants are huge grey animals with a long trunk, big ears and two white tusks.",
                Vocabulary = new[] { "trunk: the long nose of an elephant", "tusk: a long, curved tooth", "herd: a group of elephants", "calf: a baby elephant", "endangered: at risk of disappearing" },
            },
            new AnimalSpecies
            {
                Id = "giraffe", Name = "Giraffe", Plural = "giraffes", Model = "Giraffe", WalkSpeed = 1.3f, RunSpeed = 4.5f, TurnSpeed = 35, Radius = 1.8f, Height = 4.8f, Stride = 2.4f,
                Hunger = 0.08f, Thirst = 0.03f, Tiredness = 0.02f, Social = 0.04f, Activity = 0.5f, Herd = true, Browser = true, LegSwing = 20, HeadDrop = 95, NeckLift = 12, Behaviours = Grazer,
                Diet = "Leaves and shoots from tall trees, especially acacia", Home = "Savannahs of Africa", Status = "Vulnerable",
                Fact = "A giraffe's tongue is about 45 cm long and dark blue, which helps protect it from the sun while it eats.",
                Describe = "Giraffes are very tall animals with a long neck, long legs and brown patches.",
                Vocabulary = new[] { "neck: the part between head and body", "browse: to eat leaves from trees", "patch: a spot of colour", "tallest: more tall than all the others" },
            },
            new AnimalSpecies
            {
                Id = "zebra", Name = "Plains zebra", Plural = "zebras", Model = "Zebra", WalkSpeed = 1.4f, RunSpeed = 6, TurnSpeed = 70, Radius = 1.1f, Height = 1.4f, Stride = 1.4f,
                Hunger = 0.09f, Thirst = 0.06f, Tiredness = 0.03f, Social = 0.07f, Activity = 0.6f, Herd = true, LegSwing = 24, HeadDrop = 55, Behaviours = Grazer,
                Diet = "Mostly grass", Home = "Grasslands of eastern and southern Africa", Status = "Near threatened",
                Fact = "Every zebra has a different pattern of stripes, like a human fingerprint.",
                Describe = "Zebras look like small horses with black and white stripes.",
                Vocabulary = new[] { "stripe: a long line of colour", "graze: to eat grass", "herd: a group of animals", "pattern: a repeated design" },
            },
            new AnimalSpecies
            {
                Id = "antelope", Name = "Antelope", Plural = "antelopes", Model = "Deer", WalkSpeed = 1.3f, RunSpeed = 7, TurnSpeed = 90, Radius = 0.9f, Height = 1.95f, Stride = 1.2f,
                Hunger = 0.08f, Thirst = 0.05f, Tiredness = 0.03f, Social = 0.06f, Activity = 0.7f, Herd = true, LegSwing = 24, HeadDrop = 60, Behaviours = Grazer,
                Diet = "Grass, leaves and herbs", Home = "Open plains and woodland", Status = "Varies by species",
                Fact = "Antelopes stay alert while they eat: one animal often keeps watch while the others graze.",
                Describe = "Antelopes are slim, fast animals with long legs and horns.",
                Vocabulary = new[] { "alert: watching carefully for danger", "horn: a hard point on an animal's head", "slim: thin in an attractive way", "predator: an animal that hunts others" },
            },
            new AnimalSpecies
            {
                Id = "rhino", Name = "White rhinoceros", Plural = "rhinos", Model = "Rhino", WalkSpeed = 0.9f, RunSpeed = 3, TurnSpeed = 30, Radius = 2, Height = 1.75f, Stride = 1.6f,
                Hunger = 0.07f, Thirst = 0.05f, Tiredness = 0.03f, Social = 0.01f, Activity = 0.3f, RigidLegs = true, HeadDrop = 14, Behaviours = Heavy,
                Diet = "Grass", Home = "Grasslands of southern Africa", Status = "Near threatened",
                Fact = "A rhino's horn is made of keratin — the same material as your hair and fingernails.",
                Describe = "Rhinos are heavy grey animals with thick skin and one or two horns on their nose.",
                Vocabulary = new[] { "horn: a hard point on the nose", "thick: not thin", "poaching: illegal hunting", "keratin: what hair and nails are made of" },
            },
            new AnimalSpecies
            {
                Id = "sheep", Name = "Sheep", Plural = "sheep", Model = "Sheep", WalkSpeed = 0.7f, RunSpeed = 3, TurnSpeed = 90, Radius = 0.6f, Height = 0.95f, Stride = 0.6f,
                Hunger = 0.1f, Thirst = 0.05f, Tiredness = 0.03f, Social = 0.08f, Activity = 0.4f, Herd = true, LegSwing = 22, HeadDrop = 50, Behaviours = Farm,
                Diet = "Grass and hay", Home = "Farms all over the world", Status = "Domestic", Fact = "Sheep can remember the faces of other sheep — and of people — for years.",
                Describe = "Sheep are farm animals covered in thick, soft wool.", Vocabulary = new[] { "wool: the soft hair of a sheep", "flock: a group of sheep", "lamb: a baby sheep" },
            },
            new AnimalSpecies
            {
                Id = "pony", Name = "Pony", Plural = "ponies", Model = "Pony", WalkSpeed = 1, RunSpeed = 5, TurnSpeed = 70, Radius = 1, Height = 1.55f, Stride = 1.2f,
                Hunger = 0.08f, Thirst = 0.05f, Tiredness = 0.03f, Social = 0.04f, Activity = 0.5f, LegSwing = 22, HeadDrop = 60, Behaviours = Farm,
                Diet = "Grass, hay and a few carrots", Home = "Farms and stables", Status = "Domestic", Fact = "Ponies can sleep standing up by locking their legs.",
                Describe = "A pony is a small horse with a long mane and tail.", Vocabulary = new[] { "mane: the long hair on a horse's neck", "saddle: the seat on a horse's back", "stable: a building for horses" },
            },
            new AnimalSpecies
            {
                Id = "pig", Name = "Pig", Plural = "pigs", Model = "Pig", WalkSpeed = 0.6f, RunSpeed = 2.5f, TurnSpeed = 70, Radius = 0.7f, Height = 0.9f, Stride = 0.5f,
                Hunger = 0.1f, Thirst = 0.06f, Tiredness = 0.04f, Social = 0.03f, Activity = 0.35f, LegSwing = 18, HeadDrop = 25, Behaviours = Farm,
                Diet = "Vegetables, grain and roots", Home = "Farms", Status = "Domestic", Fact = "Pigs are among the most intelligent farm animals and can learn their own names.",
                Describe = "Pigs are pink farm animals with a flat nose and a curly tail.", Vocabulary = new[] { "snout: a pig's nose", "curly: in the shape of a curl", "mud: wet earth" },
            },
            new AnimalSpecies
            {
                Id = "chicken", Name = "Chicken", Plural = "chickens", Model = "Chicken", WalkSpeed = 0.5f, RunSpeed = 2, TurnSpeed = 200, Radius = 0.25f, Height = 0.45f, Stride = 0.25f,
                Hunger = 0.12f, Thirst = 0.05f, Tiredness = 0.03f, Social = 0.05f, Activity = 0.7f, Herd = true, RigidLegs = true, HeadDrop = 0, Behaviours = Farm,
                Diet = "Seeds, grain and insects", Home = "Farms", Status = "Domestic", Fact = "There are more chickens on Earth than any other bird.",
                Describe = "Chickens are farm birds with feathers, a beak and a red comb.", Vocabulary = new[] { "beak: a bird's mouth", "feather: what covers a bird", "peck: to hit or pick up with the beak" },
            },
        };

        public static AnimalSpecies Find(string id) => All.FirstOrDefault(species => species.Id == id);
    }

    /// Simplified needs, each 0 (satisfied) … 1 (urgent). Pure logic so it can be tested without a scene.
    [Serializable]
    public sealed class AnimalNeeds
    {
        [Range(0, 1)] public float Hunger, Thirst, Tiredness, Stress, Social;

        public void Tick(float minutes, AnimalSpecies species, AnimalState state, bool nearHerd, bool disturbed)
        {
            var moving = state == AnimalState.Walking || state == AnimalState.Running || state == AnimalState.Playing || state == AnimalState.ReturningToHabitat;
            Hunger = Mathf.Clamp01(Hunger + (state == AnimalState.Eating ? -0.5f : species.Hunger) * minutes);
            Thirst = Mathf.Clamp01(Thirst + (state == AnimalState.Drinking ? -0.9f : species.Thirst * (moving ? 1.3f : 1)) * minutes);
            var rest = state == AnimalState.Sleeping ? -0.35f : state == AnimalState.Resting ? -0.2f : species.Tiredness * (state == AnimalState.Running ? 4 : moving ? 1.5f : 1);
            Tiredness = Mathf.Clamp01(Tiredness + rest * minutes);
            Social = Mathf.Clamp01(Social + (!species.Herd ? 0 : nearHerd || state == AnimalState.Socializing ? -0.3f : species.Social) * minutes);
            Stress = Mathf.Clamp01(Stress + (disturbed ? 0.6f : state == AnimalState.Resting || state == AnimalState.Sleeping ? -0.25f : -0.1f) * minutes);
        }
    }

    /// Chooses what an animal does next from its needs, the hour and its herd — never by pure chance,
    /// and with long calm periods (resting, looking around) like real zoo animals.
    public static class AnimalBehaviorStateMachine
    {
        public static bool Night(float hour) => hour < 5.5f || hour > 20.5f;
        public static bool MiddayHeat(float hour) => hour > 11.5f && hour < 15f;

        /// Returns the next state and how long to stay in it (seconds) once the animal has arrived.
        public static (AnimalState state, float seconds) Choose(AnimalSpecies species, AnimalNeeds needs, float hour, bool insideHabitat, bool herdFar, float roll)
        {
            (AnimalState, float) Pick(AnimalState wanted, float seconds, AnimalState fallback = AnimalState.Idle) => (species.Can(wanted) ? wanted : fallback, seconds);
            if (!insideHabitat) return (AnimalState.ReturningToHabitat, 1);
            if (needs.Stress > 0.75f) return Pick(AnimalState.Running, 4, AnimalState.Walking);
            if (Night(hour) && needs.Thirst < 0.9f && needs.Hunger < 0.9f) return Pick(AnimalState.Sleeping, 60, AnimalState.Resting);
            if (needs.Thirst > 0.65f) return Pick(AnimalState.Drinking, 14);
            if (needs.Hunger > 0.6f) return Pick(AnimalState.Eating, 22);
            if (needs.Tiredness > 0.7f) return Pick(AnimalState.Resting, 35);
            if (species.Herd && (herdFar || needs.Social > 0.7f)) return Pick(AnimalState.Socializing, 12, AnimalState.Walking);
            // Nothing urgent: mostly calm, sometimes a stroll; quieter in the midday heat.
            var calm = Mathf.Clamp01(0.75f - species.Activity * 0.5f + (MiddayHeat(hour) ? 0.2f : 0));
            if (roll < calm * 0.45f) return Pick(AnimalState.LookingAround, 8);
            if (roll < calm * 0.75f) return Pick(AnimalState.Idle, 10);
            if (roll < calm) return Pick(MiddayHeat(hour) ? AnimalState.Resting : AnimalState.Grooming, 14, AnimalState.Idle);
            if (roll > 0.94f && needs.Tiredness < 0.4f) return Pick(AnimalState.Playing, 8, AnimalState.Walking);
            if (needs.Hunger > 0.35f && roll > 0.8f) return Pick(AnimalState.Eating, 16);
            return (AnimalState.Walking, 2);
        }
    }
}
