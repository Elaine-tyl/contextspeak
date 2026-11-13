using System;
using System.Collections.Generic;
using System.Linq;

namespace ContextSpeak.Slice
{
    public enum FishingState { Idle, Casting, Waiting, Nibble, Bite, Reeling, Caught, Escaped }
    public enum Weather { Clear, Cloudy, Rain }

    [Serializable]
    public sealed class FishSpecies
    {
        public string Id, Name, Rarity, Fact;
        public float MinCm, MaxCm, Weight, Strength, BiteWindow;
        public bool RainOnly, NightOnly, DawnDusk;
        public int Xp;
    }

    [Serializable]
    public sealed class Catch
    {
        public string Species;
        public float Length;
        public int Day, Minute;
    }

    /// Timing-based fishing: cast, wait, hook on the bite, then keep line tension in the safe zone.
    /// Pure logic so it can be unit-tested; the Unity side only animates the rod, line and float.
    [Serializable]
    public sealed class FishingSession
    {
        public static readonly FishSpecies[] Species =
        {
            new FishSpecies { Id = "bluegill", Name = "Bluegill", Rarity = "Common", MinCm = 12, MaxCm = 22, Weight = 34, Strength = 0.35f, BiteWindow = 1.2f, Xp = 10, Fact = "Bluegills nest in shallow water near the reeds." },
            new FishSpecies { Id = "perch", Name = "Yellow perch", Rarity = "Common", MinCm = 15, MaxCm = 30, Weight = 28, Strength = 0.45f, BiteWindow = 1.0f, Xp = 14, Fact = "Perch swim in schools, so where there's one there are usually more." },
            new FishSpecies { Id = "trout", Name = "Rainbow trout", Rarity = "Uncommon", MinCm = 25, MaxCm = 55, Weight = 16, Strength = 0.6f, BiteWindow = 0.8f, DawnDusk = true, Xp = 28, Fact = "Trout feed most actively around dawn and dusk." },
            new FishSpecies { Id = "koi", Name = "Koi carp", Rarity = "Uncommon", MinCm = 30, MaxCm = 70, Weight = 12, Strength = 0.55f, BiteWindow = 0.9f, Xp = 30, Fact = "Koi can live for decades — some for over a century." },
            new FishSpecies { Id = "pike", Name = "Northern pike", Rarity = "Rare", MinCm = 50, MaxCm = 100, Weight = 7, Strength = 0.85f, BiteWindow = 0.6f, DawnDusk = true, Xp = 60, Fact = "Pike are ambush hunters that wait completely still among the weeds." },
            new FishSpecies { Id = "golden-carp", Name = "Golden forest carp", Rarity = "Legendary", MinCm = 60, MaxCm = 90, Weight = 3, Strength = 0.95f, BiteWindow = 0.5f, RainOnly = true, Xp = 150, Fact = "Locals say the golden carp only rises when rain hits the pond." },
        };

        public FishingState State = FishingState.Idle;
        public float Timer, Tension, Progress, SlackTime;
        public string Hooked = "";
        public string Message = "";
        public Catch Last;
        private Random random;
        private FishSpecies fish;
        private float pull, pullTimer;

        public FishingSession(int seed) { random = new Random(seed); }

        public static int Level(int xp) => 1 + (int)Math.Floor(Math.Sqrt(Math.Max(0, xp) / 40.0));
        public static int XpForLevel(int level) => (level - 1) * (level - 1) * 40;

        public bool Cast()
        {
            if (State != FishingState.Idle && State != FishingState.Caught && State != FishingState.Escaped) return false;
            State = FishingState.Casting;
            Timer = 0.9f;
            Tension = Progress = SlackTime = 0;
            Hooked = Message = "";
            fish = null;
            return true;
        }

        /// Called when the player presses the hook button.
        public void Strike()
        {
            if (State == FishingState.Nibble)
            {
                State = FishingState.Escaped;
                Message = "Too early — it was only a nibble. Wait for the float to go right under.";
            }
            else if (State == FishingState.Bite)
            {
                State = FishingState.Reeling;
                Tension = 0.35f;
                Progress = 0.1f;
                Hooked = fish.Id;
                Message = "Hooked! Reel in, but keep the tension out of the red.";
            }
            else if (State == FishingState.Waiting)
            {
                State = FishingState.Idle;
                Message = "Nothing biting yet. Cast again.";
            }
        }

        /// Advance the simulation. reeling = the reel button is held.
        public void Tick(float dt, bool reeling, Weather weather, int minuteOfDay, int fishingLevel)
        {
            switch (State)
            {
                case FishingState.Casting:
                    Timer -= dt;
                    if (Timer <= 0)
                    {
                        State = FishingState.Waiting;
                        fish = Choose(weather, minuteOfDay, fishingLevel);
                        var speed = weather == Weather.Rain ? 0.65f : weather == Weather.Cloudy ? 0.85f : 1f;
                        Timer = (float)(3 + random.NextDouble() * 7) * speed;
                        Message = "Watch the float…";
                    }
                    break;
                case FishingState.Waiting:
                    Timer -= dt;
                    if (Timer <= 0)
                    {
                        State = random.NextDouble() < 0.55 ? FishingState.Nibble : FishingState.Bite;
                        Timer = State == FishingState.Nibble ? 0.8f + (float)random.NextDouble() : fish.BiteWindow + fishingLevel * 0.05f;
                        Message = State == FishingState.Bite ? "Bite! Strike now!" : "Something's nibbling…";
                    }
                    break;
                case FishingState.Nibble:
                    Timer -= dt;
                    if (Timer <= 0) { State = FishingState.Bite; Timer = fish.BiteWindow + fishingLevel * 0.05f; Message = "Bite! Strike now!"; }
                    break;
                case FishingState.Bite:
                    Timer -= dt;
                    if (Timer <= 0) { State = FishingState.Escaped; Message = "Too slow — it stole the bait."; }
                    break;
                case FishingState.Reeling:
                    pullTimer -= dt;
                    if (pullTimer <= 0)
                    {
                        pull = (float)random.NextDouble() * fish.Strength;
                        pullTimer = 0.4f + (float)random.NextDouble() * 0.8f;
                    }
                    Tension += (reeling ? 0.55f + pull * 0.6f : -0.7f + pull * 0.35f) * dt;
                    Tension = Math.Max(0, Tension);
                    if (reeling && Tension < 0.85f) Progress += (0.22f - fish.Strength * 0.08f + fishingLevel * 0.01f) * dt;
                    SlackTime = Tension < 0.08f ? SlackTime + dt : 0;
                    if (Tension >= 1) { State = FishingState.Escaped; Message = "Snap! The line broke — ease off when the tension turns red."; }
                    else if (SlackTime > 1.6f) { State = FishingState.Escaped; Message = "The line went slack and it shook the hook."; }
                    else if (Progress >= 1)
                    {
                        State = FishingState.Caught;
                        var length = fish.MinCm + (float)random.NextDouble() * (fish.MaxCm - fish.MinCm);
                        Last = new Catch { Species = fish.Id, Length = (float)Math.Round(length, 1) };
                        Message = $"You caught a {fish.Name} ({Last.Length:0.0} cm)!";
                    }
                    break;
            }
        }

        public FishSpecies Choose(Weather weather, int minuteOfDay, int level)
        {
            var hour = minuteOfDay / 60;
            var dawnDusk = hour >= 5 && hour < 8 || hour >= 17 && hour < 20;
            var night = hour >= 20 || hour < 5;
            var pool = Species.Where(species => (!species.RainOnly || weather == Weather.Rain) && (!species.NightOnly || night)).Select(species =>
            {
                var weight = species.Weight;
                if (species.DawnDusk && dawnDusk) weight *= 2.2f;
                if (weather == Weather.Rain && species.Rarity != "Common") weight *= 1.4f;
                if (species.Rarity == "Rare" || species.Rarity == "Legendary") weight *= 1 + level * 0.1f;
                return (species, weight);
            }).ToArray();
            var roll = random.NextDouble() * pool.Sum(entry => entry.weight);
            foreach (var (species, weight) in pool)
            {
                roll -= weight;
                if (roll <= 0) return species;
            }
            return pool.Last().species;
        }

        public static FishSpecies Find(string id) => Species.FirstOrDefault(species => species.Id == id);
    }
}
