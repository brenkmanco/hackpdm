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

using HackPDM.Extensions.General;
using HackPDM.Extensions.Odoo;

using OClient = OdooRpcCs.OdooClient;
using StatDialog = HackPDM.Forms.Settings.StatusDialog;

namespace HackPDM
{
    public static class FileOperations
    {
        public static string ConvertToBase64(string filePath)
            => Convert.ToBase64String(ReadFileInChunks(filePath));
        public static byte[] ConvertFromBase64(string base64String )
			=> Convert.FromBase64String( base64String );
	
        public static bool WriteAllBytes(HackFile file)
        {
            if (file.FileContents == null)
            {
                Console.WriteLine($"{file.Name} file contents empty");
                return false;
            }
            try
            {
                string combinedPath = file.BasePath;//Path.Combine(HackDefaults.PWAPathAbsolute, file.FilePath);
                if (!Directory.Exists(combinedPath))
                {
                    Directory.CreateDirectory(combinedPath);
                }
                combinedPath = Path.Combine(combinedPath, file.Name);

                File.WriteAllBytes(combinedPath, file.FileContents);
                
                Console.WriteLine($"{file.Name} created in {combinedPath}");
                file.ApplyModifiedDateToLocal();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
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
                string combinedPath = Path.Combine(HackDefaults.PWAPathAbsolute, file.BasePath);
                if (!Directory.Exists(combinedPath))
                {
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
        
        public static bool SameChecksum(HpVersion version, ChecksumType cType=ChecksumType.SHA1)
            => SameChecksum(
                Path.Combine(
                    HackDefaults.PWAPathAbsolute, 
                    version.winPathway, 
                    version.name), 
                version.checksum,
                GetHashAlgorithm(cType));
		public static bool SameChecksum( FileInfo file, string compareChecksum, ChecksumType cType = ChecksumType.SHA1 )
        {
            return file.Exists && SameChecksum( file.FullName, compareChecksum, GetHashAlgorithm( cType ) );
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
            ChecksumType.MD5        => MD5.Create(),
            ChecksumType.SHA256     => SHA256.Create(),
            ChecksumType.SHA512     => SHA512.Create(),
            // SHA1 or default
            _                       => SHA1.Create(),
		};
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
            string[] filePaths = [.. Directory.EnumerateFiles(path, "*", searchOption)];

            return [.. filePaths.SkipSelect(filePath => 
            {
                if (hackFileMap.TryGetValue(HpDirectory.WindowsToOdooPath(filePath, true), out Task<HackFile> hackTask))
                {
                    if (hackTask.Result != default)
                    {
                        return true;
                    }
                }
                return false;
            }, filePath => new HackFile(filePath))];
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
            //Dictionary<string, string> checkFiles = new(hackFiles.Count());
            //foreach (HackFile filePath in hackFiles)
            //{
            //    //if (OdooDefaults.ExtToType.ContainsKey(filePath.TypeExt))
            //    if (!OdooDefaults.ExtToFilter.ContainsKey(filePath.TypeExt))
            //    {
            //        checkFiles.Add(filePath.FullPath, FileChecksum(filePath.FullPath, SHA1.Create()));
            //    }
            //}
         
            HackFile[] hackArr = [.. hackFiles];
            List<HackFile> hacks = [];


            ArrayList[] arrayList = new ArrayList[hackArr.Length];
            ArrayList fields = ["name", "checksum", "dir_id"];

            for (int i = 0; i < hackArr.Length; i++)
            {
                string filepath = hackArr[i].TypeExt.ToLower();
                if (OdooDefaults.RestrictTypes && !OdooDefaults.ExtToType.ContainsKey(filepath)) continue;

                string filePath = HpDirectory.WindowsToOdooPath(hackArr[i].RelativePath);
                ArrayList arrList =
                [

                    new ArrayList() { "name", "=", hackArr[i].Name },
                    new ArrayList() { "directory_complete_name", "=", filePath },
                
                ];

                //ArrayList execParam = [arrList, fields];
                //int resultTest = await OClient.CommandAsync<int>(HpVersion.GetHpModel(), "search_count", arrList, 10000);
                ArrayList result = await OClient.BrowseAsync( HpVersion.GetHpModel(), [arrList, fields], 10000 );

                bool isFound = false;
                foreach ( Hashtable item in result )
                {
                    if (item["checksum"] is string checksum)
                    {
				        // this means that this hackFile is in the database so it can be skipped
                        if (checksum == hackArr[i].Checksum)
				        {
					        StatDialog.Dialog.AddStatusLine(StatusMessage.FOUND, $"checksum found remotely ({hackArr [ i ].Checksum}) for: {filePath}" );
                            isFound = true;
                            break;
				        }
                    }
			    }
                if ( !isFound )
                {
                    StatDialog.Dialog.AddStatusLine(StatusMessage.INFO, $"Queued commit for {hackArr[i].Name} (Checksum: {hackArr [ i ].Checksum}) for: {filePath}" );
				    hacks.Add( hackArr [ i ] );
                }

		    }
		    return [.. hacks];
        }
	    public static string GetRelativePath( string fullPath )
	    {
		    // Get the directory of the full path
		    string directoryPath = Path.GetDirectoryName(fullPath);
		    return directoryPath[(HackDefaults.PWAPathAbsolute.Length - HackDefaults.PWAPathRelative.Length)..];
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
        public static void OpenFile(string fullpath)
        {
            try
            {
                FileInfo fileInfo = new(fullpath);
                if (!fileInfo.Exists) throw new Exception();

                Process.Start(fileInfo.FullName);
            }
            catch
            {
                MessageBox.Show($"unable to open {fullpath}");
            }
        }
        public static void OpenFolder(string folderPath)
        {
            try
            {
                DirectoryInfo folderInfo = new DirectoryInfo(folderPath);
                if (!folderInfo.Exists) throw new Exception();

                Process.Start("explorer.exe", folderPath);
            }
            catch
            {
                MessageBox.Show($"unable to open {folderPath}");
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