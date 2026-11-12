using System;
using System.IO;
using System.Threading.Tasks;

using HackPDM.Core.Hack;

namespace HackPDM.Core;

/// <summary>Writes downloaded file contents to the working folder.</summary>
public static class LocalFileWriter
{
    public static long? Write(HackFile file)
    {
        if (file.FileContents == null)
        {
            Console.WriteLine($"{file.Name} file contents empty");
            return null;
        }
        try
        {
            string combinedPath = file.DirectoryName;//Path.Combine(HackDefaults.Instance.InstancePWAPathAbsolute, file.FilePath);
            if (!Directory.Exists(combinedPath))
            {
                Directory.CreateDirectory(combinedPath);
            }
            combinedPath = Path.Combine(combinedPath, file.Name);

            File.WriteAllBytes(combinedPath, file.FileContents);

            Console.WriteLine($"{file.Name} created in {combinedPath}");
            file.ApplyModifiedDateToLocal();
            return file.FileSize ?? 0;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return null;
        }
    }
    public async static Task<bool> WriteAsync(HackFile file)
    {
        if (file.FileContents == null)
        {
            Console.WriteLine($"{file.Name} file contents empty");
            return false;
        }
        try
        {
            if (LocalTargetPath(file) is not { } targetPath) return false;

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            // a copy that is not checked out is read-only: an update has to lift that first (it is
            // set again below), or the write fails
            if (File.Exists(targetPath) && File.GetAttributes(targetPath).HasFlag(FileAttributes.ReadOnly))
                File.SetAttributes(targetPath, File.GetAttributes(targetPath) & ~FileAttributes.ReadOnly);
            if (!await WriteBytes(targetPath, file.FileContents))
                return false;

            // the date and read-only flag are applied through the file's own path
            file.FullPath = targetPath;
            file.Info = null;
            file.ApplyModifiedDateToLocal();
            file.ApplyNonOwnerReadOnly();
            return true;

        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return false;
        }
    }
    /// <summary>
    /// Where a downloaded file is written: under the working folder at its server path. The server
    /// path uses Windows separators and usually ends with the file name; both forms are accepted.
    /// </summary>
    internal static string? LocalTargetPath(HackFile file)
    {
        if (string.IsNullOrEmpty(file.DirectoryName)) return null;

        string relative = file.DirectoryName.Replace('\\', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        if (file.Name is { Length: > 0 } name
            && !string.Equals(Path.GetFileName(relative), name, StringComparison.OrdinalIgnoreCase))
        {
            relative = Path.Combine(relative, name);
        }
        string root = Path.GetFullPath(HackDefaults.Instance?.PwaPathAbsolute ?? "");
        string target = Path.GetFullPath(Path.Combine(root, relative));
        // never outside the working folder, whatever names the server sends ("..", a rooted path)
        string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return target.StartsWith(rootWithSeparator, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            ? target
            : null;
    }
    private async static Task<bool> WriteBytes(string path, byte[] bytes)
    {
        try
        {
            if (path == null) throw new ArgumentNullException("path");
            if (path.Length == 0) throw new ArgumentException("Invalid Argument: Empty path");
            if (bytes == null) throw new ArgumentNullException("bytes");

            using FileStream fileStream = new(
                path: path,
                mode: FileMode.Create,
                access: FileAccess.Write,
                share: FileShare.Read,
                bufferSize: 4096,
                useAsync: true);
            await fileStream.WriteAsync(bytes, 0, bytes.Length);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
