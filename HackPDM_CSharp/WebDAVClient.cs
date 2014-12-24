/*
 * 
 * (C) 2013 Matt Taylor
 * Date: 2/18/2013
 * 
 * This file is part of Foobar.
 * 
 * Foobar is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * Foobar is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with Foobar.  If not, see <http://www.gnu.org/licenses/>.
 * 
 * 
 * Inspired by Kees van den Broek: kvdb@kvdb.net
 * Latest version and examples on: http://kvdb.net/projects/webdav
 * 
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;

// If you want to disable SSL certificate validation
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace HackPDM
{

    public class WebDAVClient
    {

        HttpWebRequest httpWebRequest;

        private int statusCode;
        /// <summary>
        /// Return statusCode of lastest operation
        /// </summary>
        public int StatusCode
        {
            get { return statusCode; }
        }


        #region WebDAV connection parameters

        private String server;
        /// <summary>
        /// Specify the WebDAV hostname (required).
        /// </summary>
        public String Server
        {
            get { return server; }
            set
            {
                value = value.TrimEnd('/');
                server = value;
            }
        }

        private String basePath = "/";
        /// <summary>
        /// Specify the path of a WebDAV directory to use as 'root' (default: /)
        /// </summary>
        public String BasePath
        {
            get { return basePath; }
            set
            {
                value = value.Trim('/');
                basePath = "/" + value + "/";
            }
        }

        private int port = 443;
        /// <summary>
        /// Specify an port
        /// (could implement a default: null = auto-detect)
        /// </summary>
        public int Port
        {
            get { return port; }
            set { port = value; }
        }

        private String user;
        /// <summary>
        /// Specify a username (optional)
        /// </summary>
        public String User
        {
            get { return user; }
            set { user = value; }
        }
        
        private String pass;
        /// <summary>
        /// Specify a password (optional)
        /// </summary>
        public String Pass
        {
            get { return pass; }
            set { pass = value; }
        }

        Uri getServerUrl(String path, Boolean appendTrailingSlash)
        {
            String completePath = basePath;
            if (path != null)
            {
                completePath += path.Trim('/');
            }

            if (appendTrailingSlash && completePath.EndsWith("/") == false) { completePath += '/'; }

            return new Uri(server + ":" + port + completePath);

        }
        
        #endregion


        #region WebDAV operations

        /// <summary>
        /// List files in the root directory
        /// </summary>
        public List<String> List()
        {
            // Set default depth to 1. This would prevent recursion (default is infinity).
            return List("/", 1);
        }

        /// <summary>
        /// List files in the given directory
        /// </summary>
        /// <param name="path"></param>
        public List<String> List(String path)
        {
            // Set default depth to 1. This would prevent recursion.
            return List(path, 1);
        }

        /// <summary>
        /// List all files present on the server.
        /// </summary>
        /// <param name="remoteFilePath">List only files in this path</param>
        /// <param name="depth">Recursion depth</param>
        /// <returns>A list of files (entries without a trailing slash) and directories (entries with a trailing slash)</returns>
        public List<String> List(String remoteFilePath, int? depth)
        {
            // Uri should end with a trailing slash
            Uri listUri = getServerUrl(remoteFilePath, true);

            // http://webdav.org/specs/rfc4918.html#METHOD_PROPFIND
            StringBuilder propfind = new StringBuilder();
            propfind.Append("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
            propfind.Append("<propfind xmlns=\"DAV:\">");
            propfind.Append("  <propname/>");
            propfind.Append("</propfind>");

            // Depth header: http://webdav.org/specs/rfc4918.html#rfc.section.9.1.4
            IDictionary<string, string> headers = new Dictionary<string, string>();
            if (depth != null)
            {
                headers.Add("Depth", depth.ToString());
            }

            HTTPRequest(listUri, "PROPFIND", headers, Encoding.UTF8.GetBytes(propfind.ToString()), remoteFilePath);

            // return result
            List<String> files = new List<string>();
            using (HttpWebResponse response = (HttpWebResponse)httpWebRequest.GetResponse())
            {
                statusCode = (int)response.StatusCode;
                using (Stream stream = response.GetResponseStream())
                {
                    XmlDocument xml = new XmlDocument();
                    xml.Load(stream);
                    XmlNamespaceManager xmlNsManager = new XmlNamespaceManager(xml.NameTable);
                    xmlNsManager.AddNamespace("d", "DAV:");

                    foreach (XmlNode node in xml.DocumentElement.ChildNodes)
                    {
                        XmlNode xmlNode = node.SelectSingleNode("d:href", xmlNsManager);
                        string filepath = Uri.UnescapeDataString(xmlNode.InnerXml);
                        string[] file = filepath.Split(new string[1] { basePath }, 2, StringSplitOptions.RemoveEmptyEntries);
                        if (file.Length > 0)
                        {
                            // Want to see directory contents, not the directory itself.
                            if (file[file.Length - 1] == remoteFilePath || file[file.Length - 1] == server) { continue; }
                            files.Add(file[file.Length - 1]);
                        }
                    }
                }
            }

            return files;
        }


        /// <summary>
        /// Upload a byte array to a file on the server
        /// </summary>
        /// <param name="content">Content of the file to upload as a byte array</param>
        /// <param name="remoteFilePath">Destination path and filename of the file on the server</param>
        public void Upload(byte[] content, String remoteFilePath)
        {
            Uri uploadUri = getServerUrl(remoteFilePath, false);
            string method = WebRequestMethods.Http.Put.ToString();

            HTTPRequest(uploadUri, method, null, content, null);
            PushContentStream(content, null);

            // process response
            int statusCode = 0;
            using (HttpWebResponse response = (HttpWebResponse)httpWebRequest.GetResponse())
            {
                statusCode = (int)response.StatusCode;
            }

        }


        /// <summary>
        /// Upload a file to the server
        /// </summary>
        /// <param name="localFilePath">Local path and filename of the file to upload</param>
        /// <param name="remoteFilePath">Destination path and filename of the file on the server</param>
        public void Upload(String localFilePath, String remoteFilePath)
        {
            FileInfo fileInfo = new FileInfo(localFilePath);
            long fileSize = fileInfo.Length;

            Uri uploadUri = getServerUrl(remoteFilePath, false);
            string method = WebRequestMethods.Http.Put.ToString();

            HTTPRequest(uploadUri, method, null, null, localFilePath);
            PushContentStream(null, localFilePath);

            // process response
            int statusCode = 0;
            using (HttpWebResponse response = (HttpWebResponse)httpWebRequest.GetResponse())
            {
                statusCode = (int)response.StatusCode;
            }

        }


        /// <summary>
        /// Download a file from the server as a byte array
        /// </summary>
        /// <param name="remoteFilePath">Source path and filename of the file on the server</param>
        /// <param name="localFilePath">Destination path and filename of the file to download on the local filesystem</param>
        public byte[] Download(String remoteFilePath)
        {
            // Should not have a trailing slash.
            Uri downloadUri = getServerUrl(remoteFilePath, false);
            string method = WebRequestMethods.Http.Get.ToString();

            HTTPRequest(downloadUri, method, null, null, null);

            int statusCode = 0;
            byte[] content = new byte[4096];
            using (HttpWebResponse response = (HttpWebResponse)httpWebRequest.GetResponse())
            {
                statusCode = (int)response.StatusCode;
                int contentLength = int.Parse(response.GetResponseHeader("Content-Length"));
                using (Stream s = response.GetResponseStream())
                {
                    using (MemoryStream fs = new MemoryStream(contentLength))
                    {
                        int bytesRead = 0;
                        do
                        {
                            bytesRead = s.Read(content, 0, content.Length);
                            fs.Write(content, 0, bytesRead);
                        } while (bytesRead > 0);
                    }
                }
            }

            return content;
        }

        /// <summary>
        /// Download a file from the server and write to filesystem
        /// </summary>
        /// <param name="remoteFilePath">Source path and filename of the file on the server</param>
        /// <param name="localFilePath">Destination path and filename of the file to download on the local filesystem</param>
        public void Download(String remoteFilePath, String localFilePath)
        {
            // Should not have a trailing slash.
            Uri downloadUri = getServerUrl(remoteFilePath, false);
            string method = WebRequestMethods.Http.Get.ToString();

            HTTPRequest(downloadUri, method, null, null, localFilePath);

            int statusCode = 0;
            using (HttpWebResponse response = (HttpWebResponse)httpWebRequest.GetResponse())
            {
                statusCode = (int)response.StatusCode;
                int contentLength = int.Parse(response.GetResponseHeader("Content-Length"));
                using (Stream s = response.GetResponseStream())
                {
                    using (FileStream fs = new FileStream(localFilePath, FileMode.Create, FileAccess.Write))
                    {
                        byte[] content = new byte[4096];
                        int bytesRead = 0;
                        do
                        {
                            bytesRead = s.Read(content, 0, content.Length);
                            fs.Write(content, 0, bytesRead);
                        } while (bytesRead > 0);
                    }
                }
            }
        }


        /// <summary>
        /// Create a directory on the server
        /// </summary>
        /// <param name="remotePath">Destination path of the directory on the server</param>
        public void CreateDir(string remotePath)
        {
            // Should not have a trailing slash.
            Uri dirUri = getServerUrl(remotePath, false);

            string method = WebRequestMethods.Http.MkCol.ToString();

            HTTPRequest(dirUri, method, null, null, null);

            int statusCode = 0;
            using (HttpWebResponse response = (HttpWebResponse)httpWebRequest.GetResponse())
            {
                statusCode = (int)response.StatusCode;
            }

        }


        /// <summary>
        /// Delete a file on the server
        /// </summary>
        /// <param name="remoteFilePath"></param>
        public void Delete(string remoteFilePath)
        {
            Uri delUri = getServerUrl(remoteFilePath, remoteFilePath.EndsWith("/"));

            HTTPRequest(delUri, "DELETE", null, null, null);

            int statusCode = 0;
            using (HttpWebResponse response = (HttpWebResponse)httpWebRequest.GetResponse())
            {
                statusCode = (int)response.StatusCode;
            }

        }

        #endregion


        #region Server communication

        /// <summary>
        /// Perform the WebDAV call and return the result
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="requestMethod"></param>
        /// <param name="headers"></param>
        /// <param name="content"></param>
        /// <param name="uploadFilePath"></param>

        //   HTTPRequest(uploadUri,             method,                                null,           null,         localFilePath,               callback,        state);
        void HTTPRequest(Uri uri, string requestMethod, IDictionary<string, string> headers, byte[] content, string uploadFilePath)
        {
            httpWebRequest = (HttpWebRequest)HttpWebRequest.Create(uri);

            /*
             * The following line fixes an authentication problem explained here:
             * http://www.devnewsgroups.net/dotnetframework/t9525-http-protocol-violation-long.aspx
             */
            System.Net.ServicePointManager.Expect100Continue = false;

            // If you want to disable SSL certificate validation
            System.Net.ServicePointManager.ServerCertificateValidationCallback +=
            delegate(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors sslError)
            {
                    bool validationResult = true;
                    return validationResult;
            };

            // The server may use authentication
            if (user != null && pass != null)
            {
                NetworkCredential networkCredential;
                networkCredential = new NetworkCredential(user, pass);
                httpWebRequest.Credentials = networkCredential;
                // Send authentication along with first request.
                httpWebRequest.PreAuthenticate = true;
            }
            httpWebRequest.Method = requestMethod;

            // Need to send along headers?
            if (headers != null)
            {
                foreach (string key in headers.Keys)
                {
                    httpWebRequest.Headers.Set(key, headers[key]);
                }
            }

            // Need to send along content?
            if (content != null || uploadFilePath != null)
            {

                if (content != null)
                {
                    // The request either contains actual content...
                    httpWebRequest.ContentLength = content.Length;
                    httpWebRequest.ContentType = "text/xml";
                }
                else
                {
                    // ...or a reference to the file to be added as content.
                    httpWebRequest.ContentLength = new FileInfo(uploadFilePath).Length;
                }

            }

            //httpWebRequest.GetRequestStream();

        }

        /// <summary>
        /// Stream content to the server
        /// </summary>
        /// <param name="result"></param>
        private void PushContentStream(byte[] content, string uploadFilePath)
        {
            using (Stream streamResponse = httpWebRequest.GetRequestStream()) {
                // Submit content
                if (content != null) {
                    streamResponse.Write(content, 0, content.Length);
                } else {
                    using (FileStream fs = new FileStream(uploadFilePath, FileMode.Open, FileAccess.Read)) {
                        content = new byte[4096];
                        int bytesRead = 0;
                        do {
                            bytesRead = fs.Read(content, 0, content.Length);
                            streamResponse.Write(content, 0, bytesRead);
                        } while (bytesRead > 0);
                    }
                }
            }
        }

        #endregion

    }
}
