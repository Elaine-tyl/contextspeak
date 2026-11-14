using System;
using ContextSpeak.Life;
using ContextSpeak.Slice;

namespace UnityEngine
{
    public class ScriptableObject { }
    public class TextAreaAttribute : Attribute { }
    public class CreateAssetMenuAttribute : Attribute { public string menuName, fileName; }
}

internal static class Program
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Main()
    {
        CheckModalOwnership();
        var scenario = new ScenarioDefinition();
        scenario.Steps.Add(new ScenarioStep { meaning = "coffee", model = "I would like a coffee, please.", standard = "What would you like?" });
        foreach (var learning in new[] { false, true })
        {
            var machine = new DialogueStateMachine();
            machine.Start(scenario, IeltsBand.Band5, learning);
            machine.Answer("I would like a coffee, please.", false);
            Check(machine.Index == 0 && machine.Phase == DialoguePhase.AwaitingAnswer, "Typed rehearsal cannot advance or consume the answer gate.");
        }
        var spoken = new DialogueStateMachine();
        spoken.Start(scenario, IeltsBand.Band5, true);
        spoken.Answer("Can I has a coffee, please?", true);
        Check(spoken.Phase == DialoguePhase.AwaitingRepeat, "Spoken answer presents feedback and corrected repeat.");
        // Reflection keeps this test executable before the speech-aware overload exists.
        var repeat = typeof(DialogueStateMachine).GetMethod("Repeat", new[] { typeof(string), typeof(bool) });
        Check(repeat != null, "Repeat must preserve speech provenance.");
        Check(!(bool)repeat.Invoke(spoken, new object[] { spoken.Target, false }) && spoken.Index == 0, "Typing the exact corrected sentence cannot pass.");
        Check(!(bool)repeat.Invoke(spoken, new object[] { "Can I have two coffees, please?", true }) && spoken.Index == 0, "A changed order quantity cannot pass through word overlap.");
        Check(!(bool)repeat.Invoke(spoken, new object[] { "Can I has a coffee, please?", true }) && spoken.Index == 0, "Repeating the known grammatical mistake cannot bypass correction through word overlap.");
        Check(!(bool)repeat.Invoke(spoken, new object[] { "purple elephants", true }) && spoken.Index == 0, "A wrong spoken repeat stays blocked.");
        Check((bool)repeat.Invoke(spoken, new object[] { spoken.Target, true }) && spoken.Phase == DialoguePhase.Complete, "The correct spoken repeat advances.");
        Console.WriteLine("Legacy spoken answer / feedback / corrected repeat checks passed.");
    }
    private static void CheckModalOwnership()
    {
        var stack = new LegacyModalStack();
        var menu = new object(); var phone = new object(); var required = new object();
        var menuClosed = 0; var phoneClosed = 0;
        stack.Set(menu, true, () => { menuClosed++; stack.Set(menu, false); return true; });
        stack.Set(phone, true, () => { phoneClosed++; stack.Set(phone, false); return true; }, 100);
        stack.Back(1); stack.Back(1);
        Check(phoneClosed == 1 && menuClosed == 0 && stack.Open && ReferenceEquals(stack.TopOwner, menu), "One press closes only the upper modal and retains the lower owner.");
        stack.Back(2);
        Check(menuClosed == 1 && !stack.Open, "A separate press closes the lower owner.");
        stack.Set(required, true);
        stack.Back(3); stack.Back(3);
        Check(stack.Open && ReferenceEquals(stack.TopOwner, required), "A required form consumes Back without being discarded.");
        stack.Set(required, false);
        Check(!stack.Open, "A required form still closes through its gameplay completion.");
        Console.WriteLine("Legacy modal ownership and one-press Back checks passed.");
    }
}
