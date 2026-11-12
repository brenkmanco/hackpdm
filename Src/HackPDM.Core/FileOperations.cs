using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HackPDM.Core.General;
using HackPDM.Core.Hack;

namespace HackPDM.Core;

/// <summary>Base64 conversion and listing the local files of a folder.</summary>
public static class FileOperations
{
    /// <summary>Encodes bytes as a base64 string.</summary>
    /// <param name="fileBytes">The bytes to encode.</param>
    /// <returns>The base64 text, or <see langword="null"/> when <paramref name="fileBytes"/> is <see langword="null"/>.</returns>
    public static string? ConvertBytesToBase64(byte[]? fileBytes)
        => fileBytes is null ? null : Convert.ToBase64String(fileBytes);
	/// <summary>Decodes a base64 string.</summary>
	/// <param name="base64String">The base64 text.</param>
	/// <returns>The decoded bytes, or <see langword="null"/> for a <see langword="null"/> or empty string.</returns>
	/// <exception cref="System.FormatException"><paramref name="base64String"/> is not valid base64.</exception>
	public static byte[]? ConvertFromBase64(string? base64String )
        => !string.IsNullOrEmpty(base64String) 
            ? Convert.FromBase64String( base64String )
            : null;
	

    /// <summary>
    /// Lists the files in a local folder as <see cref="HackPDM.Core.Hack.HackFile"/>s, leaving out the ones already
    /// known from the remote listing.
    /// </summary>
    /// <param name="path">The local folder to list.</param>
    /// <param name="hackFileMap">
    /// Already-built files keyed by server directory name (see <see cref="PwaPaths.ToServerDirectory"/>); a file whose
    /// key maps to a task with a non-null result is skipped. May be <see langword="null"/>.
    /// </param>
    /// <param name="localAndRemoteCount">Set to the number of files skipped because they were in <paramref name="hackFileMap"/>.</param>
    /// <param name="searchOption">Whether to include subfolders.</param>
    /// <param name="computeChecksums">Whether to hash each file (reads every file in full); when false, the checksum is computed on demand.</param>
    /// <returns>The remaining files, or <see langword="null"/> when <paramref name="path"/> does not exist.</returns>
    /// <remarks>Blocks on each matching task's <c>Result</c>, so the map's tasks should already be complete.</remarks>
    /// <exception cref="System.UnauthorizedAccessException">A folder cannot be listed.</exception>
    /// <exception cref="System.IO.IOException">The folder cannot be read.</exception>
    public static HackFile[]? FilesInDirectory(
        string path, 
        Dictionary<string, Task<HackFile>>? hackFileMap,
		out int localAndRemoteCount,

		SearchOption searchOption = SearchOption.TopDirectoryOnly,
		bool computeChecksums = true)
    {
		localAndRemoteCount = 0;
        if (!Directory.Exists(path))
        {
            //dividedEntries = null;
            return null;
        }
        string[] filePaths = [.. Directory.EnumerateFiles(path, "*", searchOption)];

		HackFile[]? hackFiles = [.. filePaths.SkipSelect(filePath => 
        {
            if (hackFileMap?.TryGetValue(PwaPaths.ToServerDirectory(filePath) ?? "", out var hackTask) is true)
            {
                if (hackTask.Result != default)
                {
                    return true;
                }
            }
            return false;
        }, filePath => computeChecksums
			? new HackFile(filePath)
			: HackFile.GetFromPath(filePath, computeChecksum: false)!)];

		localAndRemoteCount = filePaths.Length - hackFiles.Length;

		return hackFiles;
    }
}
