using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Infrastructure.SldWrks;
using HackPDM.Infrastructure.XmlRpc;
using HackPDM.Shared.GlobalData;


namespace HackPDM.Infrastructure.Odoo;

public static class ExtensionOdoo
{
    // xmlrpc
    private static readonly Encoding Encoding = new ASCIIEncoding();
    private static readonly XmlRpcRequestSerializer Serializer = new();
    private static readonly XmlRpcResponseDeserializer Deserializer = new();
    
    public static ArrayList GetIDs(this IEnumerable<HpBaseModel> models)
        => models.Select(model => model.id).ToArrayList();
    
    public async static Task<XmlRpcResponse> SendAsync(this XmlRpcRequest request, string url, int timeout = 0, IWebProxy proxy = null)
    {
	    //HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
	    var handler = new HttpClientHandler();
	    if (proxy is not null)
	    {
		    handler.Proxy = proxy;
		    handler.UseProxy = true;
	    }
	    using var httpClient = new HttpClient(handler);

	    if (timeout > 0)
	    {
		    httpClient.Timeout = TimeSpan.FromMilliseconds(timeout);
	    }

	    // Serialize into a MemoryStream with UTF-8
	    using var ms = new MemoryStream();
	    var xmlWriter = new XmlTextWriter(ms, Encoding.UTF8);
	    Serializer.Serialize(xmlWriter, request);
	    xmlWriter.Flush();
	    ms.Position = 0;

	    var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
	    {
		    Content = new StreamContent(ms)
	    };
	    httpRequest.Content.Headers.ContentType =
		    new System.Net.Http.Headers.MediaTypeHeaderValue("text/xml")
		    {
			    CharSet = "utf-8"
		    };

	    using var response = await httpClient.SendAsync(httpRequest);
	    response.EnsureSuccessStatusCode();

	    using var responseStream = await response.Content.ReadAsStreamAsync();
	    using var reader = new StreamReader(responseStream);

	    XmlRpcResponse result = Deserializer.DeserializeResponse(reader);
	    return result;
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

    extension(HackFile hackFile)
    {
	    public static bool GetHackFileWithDependencies(HackFile file, out List<HackFile> hackFiles)
	    {
		    List<HackResultTree.ResultNode> hackResults = [];

		    HackResultTree hackTree = new(new(file));
		    HackResultTree.ResultNode result = hackTree.Root;
		    ResultHackFile rHack;

		    var hackResultCode = HackResult.Clean;
		    //= TryFilePathToHackWithDependencies(out var list, rHack);
		    var queue = new Queue<HackResultTree.ResultNode>();
		    //list.Select(r => new HackResultTree.ResultNode(r))

		    do
		    {
			    rHack = result.Value;
			    hackResultCode = TryFilePathToHackWithDependencies(out var list, rHack);

			    if (hackResultCode is not HackResult.Clean)
			    {
				    hackFiles = [];
				    return false;
			    }

			    hackResults.Add(result);
			    foreach (ResultHackFile resultHack in list)
			    {
				    queue.Enqueue(new(resultHack));
			    }
		    } while (queue.TryDequeue(out result));

		    hackFiles = [.. hackResults.SkipSelect<HackResultTree.ResultNode, HackFile>(r => (r.Value?.Hack is null, r.Value?.Hack!))];
		    return true;
	    }
	    public bool GetVersionFromLocal(out HpVersion? versionModel)
	    {
		    string filePath = FileOperations.WindowsToOdooPath(hackFile.RelativePath);
		    ArrayList arrList =
		    [
			    new ArrayList()
			    {
				    new ArrayList() { "name", "=", hackFile.Name },
				    new ArrayList() { "directory_complete_name", "=", filePath },
			    }
		    ];
		    versionModel = HpVersion.GetRecordsBySearch(arrList, ["file_contents", "preview_image"])?[0];

		    return versionModel != null;
	    }
	    public static List<HackFile> FilePathsToHackWithDependencies(params string[] filePaths)
	    {
		    HashSet<string> newFiles = [.. filePaths];
		    List<HackFile> hackFiles = [];
		    // find all dependencies
		    foreach (string file in filePaths)
		    {
			    try
			    {
				    var fInfo = new FileInfo(file);
				    if (OdooDefaultsConstants.DependentExt.Contains(fInfo.Extension))
				    {
					    var dependencies = SolidWorksUtil.DocMgr?.GetDependencies(file);
					    if (dependencies is null || dependencies.Count <= 0) continue;
					    foreach (string[] deps in dependencies)
					    {
						    string path = deps[1];
						    var splitPath = path.Split([$"\\{HackDefaults.Instance?.PwaPathRelative}\\"], StringSplitOptions.RemoveEmptyEntries);
						    if (splitPath.Length == 2)
						    {
							    newFiles.Add(Path.Combine([HackDefaults.Instance.PwaPathAbsolute, splitPath[1]]));
						    }
					    }
				    }
			    }
			    catch (Exception e)
			    {
				    Console.WriteLine(e);
				    throw;
			    }
		    }
		    foreach (string item in newFiles)
		    {
			    HackFile? hack = HackFile.GetFromPath(item, FileOperations.GetRelativePath(item));
			    if (hack != null)
				    hackFiles.Add(hack);
		    }
		    return hackFiles;
	    }
	    public bool HasLocalVersion(out HpVersion versionModel)
	    {
		    versionModel = null;
		    if (hackFile?.HasRemoteVersion == null || !(bool)hackFile.HasRemoteVersion ||
		        hackFile.HpVersionId == null) return false;
		    
		    versionModel = HpVersion.GetRecordById((int)hackFile.HpVersionId, HpVersion.UsualExcludedFields);
		    return true;
	    }
	    public static List<HackFile> FolderPathToHackWithDependencies(string pathway, SearchOption options = SearchOption.AllDirectories)
	    {
		    // get all files in folder path to commit.
		    string[] files = [.. Directory.EnumerateFiles(pathway, "*", options)];
		    return FilePathsToHackWithDependencies(files);
	    }
	    public static bool GetHackFolderWithDependencies(string folderPath, bool listOutputDialog, out List<HackFile>? hackFiles)
	    {
		    bool hasErrors = false;
		    Regex rxSearch = new Regex(OdooDefaultsConstants.DependentExtRegex, RegexOptions.IgnoreCase);
		    var filesInDir = Directory.EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly);
		    var matchedFiles = filesInDir.SegmentWhere(file => rxSearch.IsMatch(file ?? ""));
		    (List<HackFile>, List<HackFile>) hackSegment = (
			    [.. matchedFiles.Item1.Select(f => new HackFile(new FileInfo(f)))],
			    [.. matchedFiles.Item2.Select(f => new HackFile(new FileInfo(f)))]
		    );
        
		    IEnumerable<HackFile> allHackDependencyFiles = hackSegment.Item1.SelectMany<HackFile, HackFile>(m =>
		    {
			    bool failed = !GetHackFileWithDependencies(m, out List<HackFile> hf);
			    hasErrors |= failed;
			    return failed ? hf : Enumerable.Empty<HackFile>();
		    });
		    List<HackFile> allcombinedFiles = [.. allHackDependencyFiles, .. hackSegment.Item2.SkipWhile(hf => hf.Exists is null or false)];
		    hackFiles = allcombinedFiles;
		    return true;
	    }
	    public static bool GetHackFileWithDependencies(EntryRow? entry, bool listOutputDialog, out List<HackFile>? hackFiles)
		    => (entry is not null & (hackFiles = null) is null) && GetHackFileWithDependencies(new HackFile(entry), out hackFiles);
	    public static bool GetHackFileWithDependencies(string? filePath, bool listOutputDialog, out List<HackFile>? hackFiles)
		    => (filePath is not null & (hackFiles = null) is null) && GetHackFileWithDependencies(new HackFile(new FileInfo(filePath)), out hackFiles);

	    public static HackResult TryFilePathToHackWithDependencies(out List<ResultHackFile> depAllInPWAorBrokenList, ResultHackFile parentFile)
	    {
		    List<string> newFiles = [];
		    depAllInPWAorBrokenList = [];
		    HackResult hackResultCode = parentFile.Result;

		    // if parent file is not clean, return its result
		    if (parentFile is not { Result: HackResult.Clean }) 
		    {
			    return hackResultCode;
		    }

		    // if parent file is not a dependent type, return clean
		    if (!(parentFile.Hack?.TypeExt is { } ext && OdooDefaultsConstants.DependentExt.Contains($"{ext}")))
		    {
			    return hackResultCode = HackResult.Clean;
		    }
		
		    // find all dependencies
		    List<string[]>? dependencies;
		    try
		    {
				dependencies = SolidWorksUtil.DocMgr?.GetDependencies(parentFile.Hack.FullPath!);
		    }
		    catch
		    {
			    return hackResultCode;
		    }

		    // if no dependencies, return clean
		    if (dependencies is not { Count: > 0 })
		    {
			    return hackResultCode;
		    }
		
		    foreach (var path in dependencies.Select(deps => deps[1]))
		    {
			    if (!FileOperations.InPWAFolder(path))
			    {
				    ResultHackFile resHack = new(HackFile.GetFromPath(path, FileOperations.GetRelativePath(path)), HackTestDepth.FileExistsTest);
				    depAllInPWAorBrokenList = [
					    resHack
				    ];
				    hackResultCode = resHack.Result;
				    return hackResultCode;
			    }
			    newFiles.Add(path);
		    }
		
		    foreach (var result in newFiles.Select(Help.ValidateDependency))
		    {
			    if (result.Result is not HackResult.Clean) 
			    {
				    depAllInPWAorBrokenList = [result];
				    return hackResultCode;
			    }
			    depAllInPWAorBrokenList.Add(result);
		    }
		    return hackResultCode;
	    }
	    public static bool TryFilePathsToHackWithDependencies(out (List<ResultHackFile> depAllInPWAorBrokenList, ResultHackFile parentFile)[] hackFiles, params string[] filePaths)
	    {
		    hackFiles = new (List<ResultHackFile>, ResultHackFile)[filePaths.Length];
		    bool hasErrors = false;
		    for (int i = 0; i < filePaths.Length; i++)
		    {
			    // once true, always true
			    hasErrors |= TryFilePathToHackWithDependencies(out var hd, out var pf, filePaths[i]) is not HackResult.Clean;
			    hackFiles[i] = (hd, pf);
		    }
		    return hasErrors;
	    }
	    public static HackResult TryFilePathToHackWithDependencies(out List<ResultHackFile> depAllInPWAorBrokenList, out ResultHackFile parentFile, string filePath)
	    {
		    var fInfo = new HackFile(new FileInfo(filePath));
		    parentFile = new(fInfo);
		    return TryFilePathToHackWithDependencies(out depAllInPWAorBrokenList, parentFile);
	    }
    }
    extension(IEnumerable<HpEntry> entries)
    {
	    public IEnumerable<HpEntry> TakeOutLatest(out IEnumerable<HpEntry> latestEntries)
	    {
		    latestEntries = entries.TakeWhile(entry => entry.IsLatest);
		    entries = entries.Except(latestEntries);

		    return entries;
	    }
    }
}