using System;
using System.IO;
using System.Threading.Tasks;

using HackPDM.Core.Hack;
using HackPDM.Core.Logging;

namespace HackPDM.Core;

/// <summary>Writes downloaded file contents to the working folder.</summary>
public static class LocalFileWriter
{
    /// <summary>Writes a file's downloaded contents to its <see cref="HackPDM.Core.Hack.HackBaseFile.DirectoryName"/> synchronously.</summary>
    /// <param name="file">The file; its <see cref="HackPDM.Core.Hack.HackBaseFile.FileContents"/> are written to <c>DirectoryName/Name</c>.</param>
    /// <returns>
    /// The file's <see cref="HackPDM.Core.Hack.HackFile.FileSize"/> (0 when unknown) after a successful write, or
    /// <see langword="null"/> when there were no contents or the write failed (the error is printed to the console).
    /// </returns>
    /// <remarks>
    /// Creates the folder if needed and applies <see cref="HackPDM.Core.Hack.HackFile.OverwriteDate"/> as the modified
    /// date. Unlike <see cref="WriteAsync"/> it does not keep the path inside the working folder, does not clear or set
    /// the read-only flag, and fails on an existing read-only file.
    /// </remarks>
    public static long? Write(HackFile file)
    {
        if (file.FileContents == null)
        {
            HackLogger.Warn("LocalFileWriter", $"{file.Name} file contents empty");
            return null;
        }
        if (file.DirectoryName is not { } combinedPath || file.Name is not { } name)
        {
            HackLogger.Warn("LocalFileWriter", $"{file.Name} has no folder or name to write to");
            return null;
        }
        try
        {
            if (!Directory.Exists(combinedPath))
            {
                Directory.CreateDirectory(combinedPath);
            }
            combinedPath = Path.Combine(combinedPath, name);

            string tempPath = $"{combinedPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllBytes(tempPath, file.FileContents);
                File.Move(tempPath, combinedPath, overwrite: true);
            }
            catch
            {
                TryDeleteTemp(tempPath);
                throw;
            }

            HackLogger.Debug("LocalFileWriter", $"{file.Name} created in {combinedPath}");
            file.ApplyModifiedDateToLocal();
            return file.FileSize ?? 0;
        }
        catch (UnauthorizedAccessException e)
        {
            HackLogger.Error("LocalFileWriter", $"Access denied writing '{file.Name}': {e.Message}", e);
            return null;
        }
        catch (IOException e)
        {
            HackLogger.Error("LocalFileWriter", $"I/O error writing '{file.Name}' (file may be locked or disk full): {e.Message}", e);
            return null;
        }
        catch (Exception e)
        {
            HackLogger.Error("LocalFileWriter", $"Unexpected error writing '{file.Name}': {e.Message}", e);
            return null;
        }
    }
    /// <summary>Writes a downloaded file's contents into the working folder at its server path.</summary>
    /// <param name="file">
    /// The file to write: <see cref="HackPDM.Core.Hack.HackBaseFile.FileContents"/> holds the bytes and
    /// <see cref="HackPDM.Core.Hack.HackBaseFile.DirectoryName"/> the server path (backslashes allowed, with or without
    /// the file name).
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the file was written; <see langword="false"/> when there were no contents, the target
    /// would fall outside the working folder, or an IO error occurred (errors are printed to the console, not thrown).
    /// </returns>
    /// <remarks>
    /// Creates missing folders and lifts the read-only flag of an existing copy before overwriting it. Afterwards it
    /// sets <see cref="HackPDM.Core.Hack.HackBaseFile.FullPath"/> to the target, clears
    /// <see cref="HackPDM.Core.Hack.HackBaseFile.Info"/>, applies <see cref="HackPDM.Core.Hack.HackFile.OverwriteDate"/>
    /// as the modified date, and makes the file read-only unless <see cref="HackPDM.Core.Hack.HackFile.Owner"/> is true
    /// (a copy that is not checked out to this user stays read-only).
    /// </remarks>
    public async static Task<bool> WriteAsync(HackFile file)
    {
        if (file.FileContents == null)
        {
            HackLogger.Warn("LocalFileWriter", $"{file.Name} file contents empty");
            return false;
        }
        byte[] contents = file.FileContents;
        return await PlaceAsync(file, target => WriteBytesAtomicAsync(target, contents));
    }

    /// <summary>
    /// Moves a downloaded file from <paramref name="sourcePath"/> into the working folder at its server path, the way
    /// <see cref="WriteAsync"/> writes one from memory (same target, read-only and date handling).
    /// </summary>
    /// <param name="file">The file: <see cref="HackPDM.Core.Hack.HackBaseFile.DirectoryName"/> holds the server path; its contents are not used.</param>
    /// <param name="sourcePath">The downloaded file, for example a temporary file; it is moved, not copied.</param>
    /// <returns><see langword="true"/> when the file is in place; <see langword="false"/> when the target would fall outside the working folder or an IO error occurred.</returns>
    public static Task<bool> WriteFromFileAsync(HackFile file, string sourcePath)
        => PlaceAsync(file, target =>
        {
            File.Move(sourcePath, target, overwrite: true);
            return Task.FromResult(true);
        });

    private static async Task<bool> PlaceAsync(HackFile file, Func<string, Task<bool>> write)
    {
        try
        {
            if (LocalTargetPath(file) is not { } targetPath) return false;

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            // a copy that is not checked out is read-only: an update has to lift that first (it is
            // set again below), or the write fails
            if (File.Exists(targetPath) && File.GetAttributes(targetPath).HasFlag(FileAttributes.ReadOnly))
                File.SetAttributes(targetPath, File.GetAttributes(targetPath) & ~FileAttributes.ReadOnly);
            if (!await write(targetPath))
                return false;

            // the date and read-only flag are applied through the file's own path
            file.FullPath = targetPath;
            file.Info = null;
            file.ApplyModifiedDateToLocal();
            file.ApplyNonOwnerReadOnly();
            return true;

        }
        catch (UnauthorizedAccessException e)
        {
            HackLogger.Error("LocalFileWriter", $"Access denied placing '{file.Name}': {e.Message}", e);
            return false;
        }
        catch (IOException e)
        {
            HackLogger.Error("LocalFileWriter", $"I/O error placing '{file.Name}' (file may be locked by another application or disk full): {e.Message}", e);
            return false;
        }
        catch (Exception e)
        {
            HackLogger.Error("LocalFileWriter", $"Unexpected error placing '{file.Name}': {e.Message}", e);
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
    private async static Task<bool> WriteBytesAtomicAsync(string path, byte[] bytes)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("Invalid Argument: Empty path", nameof(path));
        ArgumentNullException.ThrowIfNull(bytes);

        string tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (FileStream fileStream = new(
                path: tempPath,
                mode: FileMode.CreateNew,
                access: FileAccess.Write,
                share: FileShare.None,
                bufferSize: 65536,
                useAsync: true))
            {
                await fileStream.WriteAsync(bytes);
                await fileStream.FlushAsync();
            }

            File.Move(tempPath, path, overwrite: true);
            return true;
        }
        catch (UnauthorizedAccessException e)
        {
            TryDeleteTemp(tempPath);
            HackLogger.Error("LocalFileWriter", $"Access denied writing '{path}': {e.Message}", e);
            return false;
        }
        catch (IOException e)
        {
            TryDeleteTemp(tempPath);
            HackLogger.Error("LocalFileWriter", $"I/O error writing '{path}' (file may be locked or disk full): {e.Message}", e);
            return false;
        }
        catch (Exception e)
        {
            TryDeleteTemp(tempPath);
            HackLogger.Error("LocalFileWriter", $"Failed writing '{path}': {e.Message}", e);
            return false;
        }
    }

    private static void TryDeleteTemp(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            HackLogger.Debug("LocalFileWriter", $"Could not delete temporary file '{tempPath}': {ex.Message}");
        }
    }
}
