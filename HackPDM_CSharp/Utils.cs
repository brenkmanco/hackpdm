using System;
using System.Windows.Forms;

namespace HackPDM
{
    static class Utils
    {
        static string GetParentDirectory(string path)
        {
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
    }
}