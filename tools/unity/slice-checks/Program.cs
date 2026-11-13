public static class Program
{
    public static int Main()
    {
        try
        {
            ContextSpeak.Editor.SliceChecks.Verify();
            System.Console.WriteLine("Forest Café slice checks passed: " + ContextSpeak.Editor.SliceChecks.Passed);
            return 0;
        }
        catch (System.Exception error)
        {
            System.Console.Error.WriteLine(error.Message);
            System.Console.Error.WriteLine("Checks passed before the failure: " + ContextSpeak.Editor.SliceChecks.Passed);
            return 1;
        }
    }
}
