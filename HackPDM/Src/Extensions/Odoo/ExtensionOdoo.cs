using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using HackPDM.Extensions.General;
using HackPDM.Hack;
using HackPDM.Odoo.OdooModels;
using HackPDM.Odoo.OdooModels.Models;
using HackPDM.Odoo.XmlRpc;

using MessageBox = System.Windows.Forms.MessageBox;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace HackPDM.Extensions.Odoo;

public static class ExtensionOdoo
{
    // xmlrpc
    private static readonly Encoding Encoding = new ASCIIEncoding();
    private static readonly XmlRpcRequestSerializer Serializer = new();
    private static readonly XmlRpcResponseDeserializer Deserializer = new();
    public static ArrayList GetIDs(this IEnumerable<HpBaseModel> models)
        => models.Select(model => model.Id).ToArrayList();
    public static IEnumerable<HpEntry> TakeOutLatest(this IEnumerable<HpEntry> entries, out IEnumerable<HpEntry> latestEntries)
    {
        latestEntries = entries.TakeWhile(entry => entry.IsLatest);
        entries = entries.Except(latestEntries);

        return entries;
    }
    public static bool MessageToRecommit(this IEnumerable<HpEntry> entries)
    {
        if (entries.Count() > 0)
        {
            string lst = string.Join("\n", entries.Where(entry => entry.IsLatest).Take(10).Select(entry => $"{entry.name}"));
            string message = $"{lst}{(entries.Count() > 10 ? $"...\nincluding {entries.Count() - 10} other files\n" : "\n")}";
            if (DialogResult.Yes == MessageBox.Show($"{message}would you like to recommit the latest versions?", "recommit latest?", MessageBoxButtons.YesNoCancel))
            {
                return true;
            }
        }
        return false;
    }
    public static bool DownloadAll(this HpVersion[] versions, out List<HpVersion> failedDownloads)
    {
        failedDownloads = [];
        bool isSuccess = true;
        foreach (var version in versions)
        {
            if (!version.DownloadFile())
            {
                isSuccess = false;
                failedDownloads.Add(version);
            }
        }
        return isSuccess;
    }
    public static HackFile[] ToHackArray(this IEnumerable<FileInfo> fileInfos)
        => [.. fileInfos.Select(file => new HackFile(file))];
    public static ArrayList ToArrayListIDs<T>(this IEnumerable<T> source) where T : HpBaseModel<T>, new()
    {
        ArrayList ids = [];
        foreach (T model in source)
        {
            ids.Add(model.Id);
        }
        return ids;
    }
    public async static Task<XmlRpcResponse> SendAsync(this XmlRpcRequest request, string url, int timeout = 0, IWebProxy proxy = null)
    {
        //HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
        HttpWebRequest httpWebRequest = WebRequest.CreateHttp(url);
        if (httpWebRequest == null)
        {
            throw new XmlRpcException(-32300, "Transport Layer Error: Could not create request with " + url);
        }

        httpWebRequest.Proxy = proxy;
        httpWebRequest.Method = "POST";
        httpWebRequest.ContentType = "text/xml";
        httpWebRequest.AllowWriteStreamBuffering = true;
        if (timeout > 0)
        {
            httpWebRequest.Timeout = timeout;
        }

        XmlTextWriter xmlTextWriter = new(httpWebRequest.GetRequestStream(), Encoding);
        Serializer.Serialize(xmlTextWriter, request);
        xmlTextWriter.Flush();
        xmlTextWriter.Close();

        //HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
        HttpWebResponse httpWebResponse = (HttpWebResponse)await httpWebRequest.GetResponseAsync();

        StreamReader streamReader = new(httpWebResponse.GetResponseStream());

        XmlRpcResponse result = Deserializer.DeserializeResponse(streamReader);
        streamReader.Close();
        httpWebResponse.Close();
        return result;
    }
}