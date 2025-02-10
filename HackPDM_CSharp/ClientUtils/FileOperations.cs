using HackPDM.ClientUtils;
using SolidWorks.Interop.sldworks;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using OClient = OdooRpcCs.OdooClient;

namespace HackPDM
{
    public static class FileOperations
    {
        public static string ConvertToBase64(string filePath)
            => Convert.ToBase64String(ReadFileInChunks(filePath));
        public static string[] ConvertToBase64(params string[] filePaths)
        {
            string[] base64Array = new string[filePaths.Length];
            for (int i = 0; i < filePaths.Length; i++)
            {
                base64Array[i] = ConvertToBase64(filePaths[i]);
            }
            return base64Array;
        }
        public static byte[] ConvertFromBase64(string base64String )
			=> Convert.FromBase64String( base64String );
		public static bool WriteAllBytes(FileData file)
        {
            if (file.FileContents == null)
            {
                Console.WriteLine($"{file.Name} file contents empty");
                return false;
            }
            try
            {
                string combinedPath = Path.Combine(HackDefaults.PWAPathAbsolute, file.FilePath);
                if (!Directory.Exists(combinedPath))
                {
                    Directory.CreateDirectory(combinedPath);
                }
                combinedPath = Path.Combine(combinedPath, file.Name);

                File.WriteAllBytes(combinedPath, file.FileContents);

                Console.WriteLine($"{file.Name} create in {combinedPath}");
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }
        public async static Task<bool> WriteAllBytesAsync(FileData file)
        {
            if (file.FileContents == null)
            {
                Console.WriteLine($"{file.Name} file contents empty");
                return false;
            }
            try
            {
                string combinedPath = Path.Combine(HackDefaults.PWAPathAbsolute, file.FilePath);
                if (!Directory.Exists(combinedPath))
                {
                    Directory.CreateDirectory(combinedPath);
                }
                combinedPath = Path.Combine(combinedPath, file.Name);

                await WriteBytes(combinedPath, file.FileContents);
                return true;

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }
        public static bool SameChecksum(HpVersion version, ChecksumType cType)
        {
            HashAlgorithm alg;

            switch (cType)
            {
                case ChecksumType.MD5:
                    {
                        alg = MD5.Create();
                        break;
                    }
                case ChecksumType.SHA1:
                default:
                    {
                        alg = SHA1.Create();
                        break;
                    }
            }
            string fileChecksum = FileChecksum(Path.Combine(HackDefaults.PWAPathAbsolute, version.winPathway, version.name), alg);

            if (fileChecksum != null && fileChecksum != "" && version.checksum == fileChecksum)
                return true;
            return false;
        }
        public static bool SameChecksum(string directoryPath, string compareChecksum, HashAlgorithm alg)
        {
            string fileChecksum = FileChecksum(directoryPath, alg);
            if (fileChecksum != null && fileChecksum != "" && fileChecksum == compareChecksum)
                return true;
            return false;
        }
        public static bool ContainsSameChecksum(string directoryPath, HashAlgorithm alg, params string[] checksums)
        {
            string fileChecksum = FileChecksum(directoryPath, alg);
            return checksums.Contains(fileChecksum);
        }

        public static bool ContainsSameChecksum(string directoryPath, HashAlgorithm alg, IEnumerable<string> checksums)
        {
            string fileChecksum = FileChecksum(directoryPath, alg);
            return checksums.Contains(fileChecksum);
        }
        public static string FileChecksum(string filePath, HashAlgorithm alg)
        {
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

                using (FileStream fileStream = new(
                    path: path,
                    mode: FileMode.Create,
                    access: FileAccess.Write,
                    share: FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true))
                {
                    await fileStream.WriteAsync(bytes, 0, bytes.Length);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        public static byte[] ReadFileInChunks(string filePath)
        {
            using (FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BufferedStream bs = new(fs))
            using (MemoryStream ms = new())
            {
                byte[] buffer = new byte[4096]; // Adjust buffer size as needed
                int bytesRead;
                while ((bytesRead = bs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, bytesRead);
                }
                return ms.ToArray();
            }
        }
        public static HackFile[] FilesInDirectory(string path, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            IEnumerable<string> filePaths = Directory.EnumerateFiles(path, "*", searchOption);
            DirectoryInfo directory = new DirectoryInfo(path);
            IEnumerable<FileInfo> fileInfo = directory.EnumerateFiles("*", searchOption);

            return fileInfo.ToHackArray();
        }
       // public static FileInfo[] FilesInDirectory(
       //     string path, 
       //     Hashtable entries, 
       //     //out Dictionary<string, Hashtable> dividedEntries, 
       //     SearchOption searchOption = SearchOption.TopDirectoryOnly)
       // {
       //     if (!Directory.Exists(path))
       //     {
       //         //dividedEntries = null;
       //         return null;
       //     }
       //     string[] filePaths = Directory.EnumerateFiles(path, "*", searchOption).ToArray();

       //     IEnumerable<string> checksums = entries.Select<DictionaryEntry, string>((entry) =>
       //     {
       //         Hashtable ht = (Hashtable)entry.Value;
			    //if (ht["latest_checksum"] is bool) return "";
       //         return (string)ht["latest_checksum"];
       //     });

       //     IEnumerable<string> paths = filePaths.Where(filePath => !ContainsSameChecksum(filePath, SHA1.Create(), checksums));
            

       //     List<FileInfo> fileInfo = new(paths.Count());

       //     foreach (string filePath in paths)
       //     {
       //         FileInfo file = new(filePath);

       //         if (file.Exists)
       //         {
       //             fileInfo.Add(file);
       //         }
       //     }

       //     return fileInfo.ToArray();
       // }
       public static HackFile[] FilesInDirectory(
            string path, 
            Dictionary<string, Task<HackFile>> hackFileMap, 
            //out Dictionary<string, Hashtable> dividedEntries, 
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (!Directory.Exists(path))
            {
                //dividedEntries = null;
                return null;
            }
            string[] filePaths = Directory.EnumerateFiles(path, "*", searchOption).ToArray();

            return filePaths.SkipSelect(filePath => 
            {
                if (hackFileMap.TryGetValue(HpDirectory.WindowsToOdooPath(filePath, true), out Task<HackFile> hackTask))
                {
                    if (hackTask.Result != default)
                    {
                        return true;
                    }
                }
                return false;
            }, filePath => new HackFile(filePath)).ToArray();
            //IEnumerable<string> paths = filePaths.Where(filePath => !ContainsSameChecksum(filePath, SHA1.Create(), checksums));
            

            //List<FileInfo> fileInfo = new(paths2.Count());

            //foreach (string filePath in paths2)
            //{
            //    FileInfo file = new(filePath);

            //    if (file.Exists)
            //    {
            //        fileInfo.Add(file);
            //    }
            //}

            //return fileInfo.ToArray();
        }
        public static ArrayList FilesNotInOdoo(string[] filePaths)
        {
            // key: checksum, value: filepath
            Dictionary<string, string> checkFiles = new(filePaths.Length);
            foreach (string filePath in filePaths)
            {
                checkFiles.Add(FileChecksum(filePath, SHA1.Create()), filePath);
            }

            ArrayList domain = ["checksum", "in", checkFiles.Keys.ToArray()];
            ArrayList fields = ["checksum"];
            ArrayList result = OClient.Browse(HpVersion.GetHpModel(), [domain, fields], 10000);

            // Hashtable of all results
            // might have array or value
            ArrayList values = Utils.GetResults(result, "checksum", true);
            return values;
        }
        public async static Task<HackFile[]> FilesNotInOdoo(IEnumerable<HackFile> hackFiles)
        {
            // key: checksum, value: filepath
            Dictionary<string, string> checkFiles = new(hackFiles.Count());
            foreach (HackFile filePath in hackFiles)
            {
                if (OdooDefaults.ExtToType.ContainsKey(filePath.TypeExt))
                {
                    checkFiles.Add(filePath.FullPath, FileChecksum(filePath.FullPath, SHA1.Create()));
                }
            }
         
            HackFile[] hackArr = hackFiles.ToArray();
            List<HackFile> hacks = [];


            ArrayList[] arrayList = new ArrayList[hackArr.Length];
            ArrayList fields = ["name", "checksum", "dir_id"];

            for (int i = 0; i < hackArr.Length; i++)
            {
                if (!OdooDefaults.ExtToType.ContainsKey(hackArr[i].TypeExt.ToLower())) continue;

                string filePath = HpDirectory.WindowsToOdooPath(hackArr[i].RelativePath);
                ArrayList arrList = new ArrayList
                {

                    new ArrayList() { "name", "=", hackArr[i].Name },
                    // new ArrayList() { "checksum", "=", hackArr[i].SHA1Checksum },
                    new ArrayList() { "directory_complete_name", "=", filePath },
                
                };

                //ArrayList execParam = [arrList, fields];
                //int resultTest = await OClient.CommandAsync<int>(HpVersion.GetHpModel(), "search_count", arrList, 10000);
                ArrayList result = await OClient.BrowseAsync( HpVersion.GetHpModel(), [arrList, fields], 10000 );

                bool isFound = false;
                foreach ( Hashtable item in result )
                {
                    if (item["checksum"] is string checksum)
                    {
				        // this means that this hackFile is in the database so it can be skipped
                        if (checksum == hackArr[i].SHA1Checksum)
				        {
					        HackFileManager.Dialog.AddStatusLine( "INFO", $"checksum found remotely ({hackArr [ i ].SHA1Checksum}) for: {filePath}" );
                            isFound = true;
                            break;
				        }
                    }
			    }
                if ( !isFound )
                {
				    HackFileManager.Dialog.AddStatusLine( "INFO", $"unable to find checksum remotely ({hackArr [ i ].SHA1Checksum}) for: {filePath}" );
				    hacks.Add( hackArr [ i ] );
                }

		    }
		    return [.. hacks];
        }
	    public static string GetRelativePath( string fullPath )
	    {
		    // Get the directory of the full path
		    string directoryPath = Path.GetDirectoryName(fullPath);
		    return directoryPath.Substring(HackDefaults.PWAPathAbsolute.Length - HackDefaults.PWAPathRelative.Length);
	    }
		public async static IAsyncEnumerable<string> ChecksumsInDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                IEnumerable<string> filePaths = Directory.EnumerateFiles(path);
                foreach (string filePath in filePaths)
                {
                    Task<string> checksumTask = FileChecksumAsync(filePath, SHA1.Create());
                    await checksumTask;
                    yield return checksumTask.Result;
                }
            }
            yield return null;
        }

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
        public static string GetRelativePath(string basePath, string absolutePath)
        {
            Uri baseUri = new(basePath); 
            Uri absoluteUri = new(absolutePath); 
            Uri relativeUri = baseUri.MakeRelativeUri(absoluteUri);
            return Uri.UnescapeDataString(relativeUri.ToString().Replace('/', '\\'));
        }
        public static void OpenFile(string path)
        {
            try
            {
                FileInfo fileInfo = new(path);
                if (!fileInfo.Exists) throw new Exception();

                Process.Start(fileInfo.FullName);
            }
            catch
            {
                MessageBox.Show($"unable to open {path}");
            }
        }
		public static byte [] ImageToByteArray(Image imageIn)
		{
            using ( MemoryStream ms = new() )
            {
                imageIn.Save( ms, ImageFormat.Png );
			    return ms.ToArray();
            }
		}
	}
}