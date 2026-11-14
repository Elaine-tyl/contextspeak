using System.IO;
namespace ContextSpeak.Learning
{
    public static class LearningSaveFile
    {
        public static void Write(string path, string json)
        {
            var temp = path + ".tmp";
            File.WriteAllText(temp, json);
            // The previous file remains intact until replacement. A failed write never deletes it.
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }
    }
}
