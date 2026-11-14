public static class Program
{
    public static int Main()
    {
        try { ContextSpeak.Editor.LivingWorldChecks.Verify(); return 0; }
        catch (System.Exception error) { System.Console.Error.WriteLine(error); return 1; }
    }
}
