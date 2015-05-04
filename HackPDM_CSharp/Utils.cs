using System;
using System.Windows.Forms;

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
                    // This is the last parent directory.  Return the empty string:
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
    }
}