using System;
using System.IO;
using System.Windows.Forms;
using System.Globalization;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace HackPDM
{
    public static class Utils
    {
        /// <summary>
        /// Returns an absolute or relative path for the parent of the passed argument
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetParentDirectory(string path)
        {
            // Check if path is a relative or absolute path:
            if (System.IO.Path.IsPathRooted(path))
            {
                // This is an absolute path:
                try
                {
                    System.IO.DirectoryInfo directoryInfo = System.IO.Directory.GetParent(path);
                    return (directoryInfo.FullName);
                }
                catch (ArgumentNullException)
                {
                    MessageBox.Show("Path is a null reference.  Could not find its parent.",
                            "Path Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    return ("");
                }
                catch (ArgumentException)
                {
                    MessageBox.Show("Path is an empty string.  Could not find its parent.",
                            "Path Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    return ("");
                }
                catch (System.IO.DirectoryNotFoundException)
                {
                    MessageBox.Show("The parent directory for path \"" + path + "\" could not be found.",
                            "Path Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    return ("");
                }
                catch
                {
                    MessageBox.Show("Could not find the parent directory for \"" + path + "\".",
                            "Path Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    return ("");
                }
            }
            else
            {
                // This is a relative path.  Check if there are any slashes:
                if (path.Contains("\\"))
                {
                    return (path.Substring(0, path.LastIndexOf("\\")));
                }
                else
                {
                    // This is the last parent directory
                    // TODO: Correct code to be more consisent (Some code may expect this method to return "pwa")
                    // Return the empty string:
                    return ("");
                }
            }
        }


        public static string GetBaseName(string path)
        {
            try
            {
                return (System.IO.Path.GetFileName(path));
            }
            catch
            {
                MessageBox.Show("Error getting Base Name from \"" + path + "\".",
                        "Path Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                return ("");
            }
        }


        public static string GetAbsolutePath(string strLocalFileRoot, string stringPath)
        {
            //Get Full path
 //           if (stringPath.Substring(0, 3) == "pwa")
            if (!System.IO.Path.IsPathRooted(stringPath))
            {
                //replace pwa with actual root path
                return (strLocalFileRoot + stringPath.Substring(3));
            }
            else
            {
                // NOTE 1:  This will occur if:
                //  a) stringPath starts with a drive identifier (e.g., "C:/"), or
                //  b) stringPath starts with a root folder slash (e.g., "\\").
                //
                // NOTE 2:  System.IO.Path.IsPathRooted() does not check if stringPath is an actual path.
                return(stringPath);
            }
        }


        public static string GetRelativePath(string strLocalFileRoot, string stringPath)
        {
            // get tree path
            string stringParse = "";
            // replace actual root path with pwa
            if (stringPath.IndexOf(strLocalFileRoot, 0, StringComparison.CurrentCultureIgnoreCase) != -1)
            {
                stringParse = "pwa" + stringPath.Substring(strLocalFileRoot.Length);
            }
            return stringParse;
        }


        public static string GetShortName(string FullName)
        {
            //    return FullName.Substring(FullName.LastIndexOf("\\") + 1);
            return Utils.GetBaseName(FullName);
        }


        //protected string GetFileExt(string strFileName) {
        //    //Get Name of folder
        //    string[] strSplit = strFileName.Split('.');
        //    int _maxIndex = strSplit.Length-1;
        //    return strSplit[_maxIndex];
        //}


        public static string FormatDate(DateTime dtDate)
        {

            // if file not in local current day light saving time, then add an hour?
            if (TimeZone.CurrentTimeZone.IsDaylightSavingTime(dtDate) == false)
            {
                dtDate = dtDate.AddHours(1);
            }

            // get date and time in short format and return it
            string stringDate = "";
            //stringDate = dtDate.ToShortDateString().ToString() + " " + dtDate.ToShortTimeString().ToString();
            stringDate = dtDate.ToString("yyyy-MM-dd HH:mm:ss");
            return stringDate;

        }


        public static string FormatSize(Int64 lSize)
        {
            //Format number to KB
            string stringSize = "";
            NumberFormatInfo myNfi = new NumberFormatInfo();

            Int64 lKBSize = 0;

            if (lSize < 1024)
            {
                if (lSize == 0)
                {
                    //zero byte
                    stringSize = "0";
                }
                else
                {
                    //less than 1K but not zero byte
                    stringSize = "1";
                }
            }
            else
            {
                //convert to KB
                lKBSize = lSize / 1024;
                //format number with default format
                stringSize = lKBSize.ToString("n", myNfi);
                //remove decimal
                stringSize = stringSize.Replace(".00", "");
            }

            return stringSize + " KB";

        }


        public static string StringMD5(string FileName)
        {
            // get local file checksum
            using (var md5 = MD5.Create())
            {
                //using (var stream = File.OpenRead(FileName))
                using (var stream = File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
                }
            }
        }


        public static void GetAllFilesInDir(string dirpath, ref List<string> filesfound)
        {
            try
            {
                foreach (string d in Directory.GetDirectories(dirpath))
                {
                    foreach (string f in Directory.GetFiles(d))
                    {
                        filesfound.Add(f);
                    }

                    GetAllFilesInDir(d, ref filesfound);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Error finding local files.",
                        "File Discovery Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
            }
        }
    }
}