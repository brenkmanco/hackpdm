using HackPDM.ClientUtils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace HackPDM
{
    public abstract class HackBaseFile
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string BasePath { get; set; }
        public string RelativePath { get; set; }
        private byte[] FileContents { get; set; }

        
        public Hashtable ComputeHashtable(bool includeEmpty = true, in string[] excludedFieldNames = null)
        {
            Hashtable ht;
            ht = HashConverter.ConvertToHashtable(this, MethodType.PropertyOnly, includeEmpty, excludedFieldNames);
            
            return ht;
        }
        public async static Task<HackFile> GetHackFileAsync<T>(string fullFilePath) where T : HackFile, new()
        {
            HackFile hackFile = DefaultType<HackFile>();

            try
            {
                // if the directory doesn't exist then return its default type
                FileInfo fileInfo = new(fullFilePath);
                if (!fileInfo.Exists) return hackFile;
                
                hackFile = await FileInfoToHackFile(fileInfo);
            }
            catch (Exception ex) 
            {
                Console.WriteLine(ex);
            }
            return hackFile;
        }
        private static async Task<HackFile> FileInfoToHackFile(FileInfo fileInfo) => await HackFile.GetFromFileInfo(fileInfo);
        
        
        private static T DefaultType<T>() where T : new()
        {
            if (typeof(T).IsValueType)
            {
                return default;
            }
            return new T();
        }
    }
}
