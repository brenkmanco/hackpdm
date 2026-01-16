using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Threading.Tasks;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Core;



public static class FileOperations
{
    public static string ConvertToBase64(string filePath)
        => Convert.ToBase64String(ReadFileInChunks(filePath));
    public static byte[] ConvertFromBase64(string base64String )
        => Convert.FromBase64String( base64String );
	
	public static bool InPWAFolder(string? fullFileName) => fullFileName?.StartsWith(HackDefaults.Instance.PwaPathAbsolute) ?? false;

    public static long? WriteAllBytes(HackFile file)
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
    public async static Task<bool> WriteAllBytesAsync(HackFile file)
    {
        if (file.FileContents == null)
        {
            Console.WriteLine($"{file.Name} file contents empty");
            return false;
        }
        try
        {
            string combinedPath = Path.Combine(HackDefaults.Instance.PwaPathAbsolute, file.DirectoryName);
            if (!Directory.Exists(combinedPath))
            {
				//CreateDirectory(combinedPath);
                Directory.CreateDirectory(combinedPath);
            }
            combinedPath = Path.Combine(combinedPath, file.Name);

            await WriteBytes(combinedPath, file.FileContents);
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
        
    public static bool SameChecksum(IHpVersionModel versionModel, ChecksumType cType=ChecksumType.Sha1)
        => SameChecksum(
            Path.Combine(
                HackDefaults.Instance.PwaPathAbsolute, 
                versionModel.windows_complete_name[5..]), 
            versionModel.checksum,
            GetHashAlgorithm(cType));
    public static bool SameChecksum( FileInfo file, string compareChecksum, ChecksumType cType = ChecksumType.Sha1 )
    {
        return file.Exists && SameChecksum( file.FullName, compareChecksum, GetHashAlgorithm( cType ) );
    }
	public static DirectoryInfo CreateDirectory(string path)
	{
		List<DirectoryInfo> directories = [];
		for (int i = 0; i < path.Length; i++)
		{
			if (path[i] == '/' || path[i] == '\\')
			{
				string subPath = path[..i];
				if (!Directory.Exists(subPath))
				{
					directories.Add(Directory.CreateDirectory(subPath));
				}
			}
		}

		return directories.Last();
	}
	public static bool SameChecksum( string directoryPath, string compareChecksum, HashAlgorithm alg )
    {
        string fileChecksum = FileChecksum(directoryPath, alg);
        if ( fileChecksum != null && fileChecksum != "" && fileChecksum == compareChecksum )
            return true;
        return false;
    }

    public static HashAlgorithm GetHashAlgorithm(ChecksumType cType) => cType switch
    {
        ChecksumType.Md5        => MD5.Create(),
        ChecksumType.Sha256     => SHA256.Create(),
        ChecksumType.Sha512     => SHA512.Create(),
        // SHA1 or default
        _                       => SHA1.Create(),
    };
    public static string? FileChecksum(string? filePath, HashAlgorithm alg)
    {
		if (string.IsNullOrEmpty(filePath)) return null;
        string fileChecksum = "";
        try
        {
            // opens the file and computes the checksum
            // and converts it to lowercase string
            // checks it against the version checksum
            using (FileStream stream = File.OpenRead(filePath))
            {
                if (stream.Length != 0) 
                    fileChecksum = string.Join("", alg.ComputeHash(stream)
                        .Select(b => b.ToString("X2"))).ToLower();
            }
            return fileChecksum;
        }
        catch (Exception e) when (e is DirectoryNotFoundException || e is FileNotFoundException)
        {
            Console.WriteLine($"file or directory not found: {e.Message}");
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        return null;
    }
    public static async Task<string> FileChecksumAsync(string directoryPath, HashAlgorithm alg)
        => await Task.Run(() => FileChecksum(directoryPath, alg));
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
    public static byte[] ReadFileInChunks(string filePath)
    {
		using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
		using BufferedStream bs = new(fs);
		using MemoryStream ms = new();
		byte[] buffer = new byte[4096]; // Adjust buffer size as needed
		int bytesRead;
		while ((bytesRead = bs.Read(buffer, 0, buffer.Length)) > 0)
		{
			ms.Write(buffer, 0, bytesRead);
		}
		return ms.ToArray();
	}
    public static HackFile[] FilesInDirectory(string path, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        IEnumerable<string> filePaths = Directory.EnumerateFiles(path, "*", searchOption);
        DirectoryInfo directory = new DirectoryInfo(path);
        IEnumerable<FileInfo> fileInfo = directory.EnumerateFiles("*", searchOption);

        return fileInfo.ToHackArray();
    }

    public static HackFile[]? FilesInDirectory(
        string path, 
        Dictionary<string, Task<HackFile>>? hackFileMap,
		//out Dictionary<string, Hashtable> dividedEntries, 
		out int localAndRemoteCount,

		SearchOption searchOption = SearchOption.TopDirectoryOnly)
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
            if (hackFileMap?.TryGetValue(WindowsToOdooPath(filePath, true), out var hackTask) is true)
            {
                if (hackTask.Result != default)
                {
                    return true;
                }
            }
            return false;
        }, filePath => new HackFile(filePath))];

		localAndRemoteCount = filePaths.Length - hackFiles.Length;

		return hackFiles;
    }
    
    
    public static string? GetRelativePath( string fullPath )
    {
        // Get the directory of the full path
        string? directoryPath = Path.GetDirectoryName(fullPath);
		return directoryPath?.StartsWith(HackDefaults.Instance.PwaPathAbsolute) is true
			? directoryPath[(HackDefaults.Instance.PwaPathAbsolute.Length - HackDefaults.Instance.PwaPathRelative.Length)..]
			: null;
    }
    public static string FileSizeReformat(int? bytesize)
        => FileSizeReformat((long?)bytesize);
    public static string FileSizeReformat(long? bytesize, bool standard = false)
        => bytesize switch
        {
            < 1024 => standard ? $"{bytesize} B" : $"{bytesize}     B",
            < 1048576 => standard ? $"{bytesize / 1024f:.##} KB" : $"{bytesize / 1024f:.##}   KB",
            < 1073741824 => standard ? $"{bytesize / 1048576f:.##} MB" : $"{bytesize / 1048576f:.##}   MB",
            < 1099511627776 => standard ? $"{bytesize / 1073741824f:.##} GB" : $"{bytesize / 1073741824f:.##}   GB",
            <= 1125899906842624 => standard ? $"{bytesize / 1099511627776f:.##} TB" : $"{bytesize / 1099511627776f:.##}   TB",
            _ => standard ? $"{bytesize} B": $"{bytesize}     B",
        };

    public static bool IsFileLocked(FileInfo file)
    {
        FileStream stream = null;

        if (file.Exists)
        {
            try
            {
                stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            finally
            {
                if (stream != null) stream.Close();
            }
        }

        //file is not locked
        return false;

    }
    public static bool OpenFile(string fullpath)
    {
        try
        {
            FileInfo fileInfo = new(fullpath);
            if (!fileInfo.Exists) throw new Exception();

            Process.Start(fileInfo.FullName);
            return true;
        }
        catch
        {
            return false;
        }
    }
    public static bool OpenFolder(string folderPath)
    {
        try
        {
            DirectoryInfo folderInfo = new DirectoryInfo(folderPath);
            if (!folderInfo.Exists) throw new Exception();

            Process.Start("explorer.exe", folderPath);
            return true;
        }
        catch
        {
            return false;
        }
    }
        
    
    public static string? ConvertToWindowsPath(string? pathway, bool withAbsolutePath)
    {
        if (pathway is null) return null;
        string[] pathwaySegmented = pathway.Split([" / "], StringSplitOptions.RemoveEmptyEntries);
        if (pathwaySegmented[0] == "root" || pathwaySegmented[0] == HackDefaults.Instance.PwaPathRelative)
        {
            pathwaySegmented = pathwaySegmented[1..];
        }
        string relativePath = string.Join(@"\", pathwaySegmented);

        return withAbsolutePath ? Path.Combine(HackDefaults.Instance.PwaPathAbsolute, relativePath) : relativePath;
    }
    public static string? NodePathToWindowsPath(string? pathway, bool withAbsolutePath = true)
    {
        if (pathway is null) return null;
        string[] pathwaySegmented = pathway.Split(["\\"], StringSplitOptions.RemoveEmptyEntries);
        if (pathwaySegmented[0] == "root" || pathwaySegmented[0] == HackDefaults.Instance.PwaPathRelative)
        {
            pathwaySegmented = pathwaySegmented[1..];
        }
        string relativePath = string.Join(@"\", pathwaySegmented);

        return withAbsolutePath ? Path.Combine(HackDefaults.Instance.PwaPathAbsolute, relativePath) : relativePath;
    }
    public static string WindowsToOdooPath(string pathway, bool fromFullPath = false)
    {
        if (fromFullPath)
        {
            pathway = pathway[(HackDefaults.Instance.PwaPathAbsolute.Length - HackDefaults.Instance.PwaPathRelative.Length)..];
        }
        string[] pathwaySegmented = pathway.Split('\\');
        if (pathwaySegmented[0] == HackDefaults.Instance.PwaPathRelative)
        {
            pathwaySegmented[0] = "root";
        }
        if (pathwaySegmented[0] != "root")
        {
            pathwaySegmented = [.. pathwaySegmented.Prepend("root")];
        }
        string relativePath = string.Join(@" / ", pathwaySegmented);
        return relativePath;
    }
}