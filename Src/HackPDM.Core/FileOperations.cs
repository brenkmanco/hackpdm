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
    public static string? ConvertBytesToBase64(byte[]? fileBytes)
        => fileBytes is null ? null : Convert.ToBase64String(fileBytes);
	public static byte[]? ConvertFromBase64(string? base64String )
        => !string.IsNullOrEmpty(base64String) 
            ? Convert.FromBase64String( base64String )
            : null;
	

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
