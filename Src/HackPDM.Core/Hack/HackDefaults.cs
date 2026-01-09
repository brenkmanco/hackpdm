using System.Collections;
using HackPDM.Abstractions;

namespace HackPDM.Infrastructure.Hack;

public class HackDefaults : HackDefaultBase
{
    public static bool GetFiles(string relativePath, out IEnumerable<string> files)
    {
        CurrentPath = Path.Combine(PwaPathAbsolute, relativePath);
        try
        {         
            // EnumerateFiles goes off a relative path from your project
            files = Directory.EnumerateFiles(CurrentPath, "*", SearchOption.AllDirectories);
                
            return true;
        }
        catch (DirectoryNotFoundException e)
        {
            Console.WriteLine(e.Message);
            files = null;
            return false;
        }
    }
    public static string DefaultPath(string? pathway, bool withAbsolute = false)
    {
        if (pathway is null || pathway == "") return withAbsolute ? PwaPathAbsolute : "root";
        string[] paths = pathway.Split('\\');
        paths = [.. paths.Skip(1)];

        string relativePath = string.Join(@"\", paths);

        if (withAbsolute) return Path.Combine(PwaPathAbsolute, relativePath);
            
        return relativePath;
    }
    public static T[] ArrayListToModelsArray<T>(ArrayList al) where T : IConvert<T>, new()
    {
        List<T> models = [];
        foreach (Hashtable ht in al)
        {
            T model = new();
            models.Add(model.ConvertFromHt(ht));
        }
        return [.. models];
    }
    private static void RecurseTravel(DirectoryDict directory, string directoryFullPath)
    {
        string pathway = directoryFullPath + "\\" + directory.Name;
        Directory.CreateDirectory(pathway);

        // recurse traverse children
        foreach (DirectoryDict hdr in directory.Directories)
        {
            RecurseTravel(hdr, pathway);
        }
    }
}
