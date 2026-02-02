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

using Newtonsoft.Json;


namespace HackPDM.Infrastructure.Odoo;

public static class ExtensionOdoo
{
    // xmlrpc
    private static readonly Encoding Encoding = new ASCIIEncoding();
    private static readonly XmlRpcRequestSerializer Serializer = new();
    private static readonly XmlRpcResponseDeserializer Deserializer = new();
    
    public static ArrayList GetIDs(this IEnumerable<HpBaseModel> models)
        => models.Select(model => model.Id).ToArrayList();
    
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
	    public async Task<(bool, HpVersion?)> GetVersionFromLocal()
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
		    HpVersion? versionModel = (await HpVersion.GetRecordsBySearchAsync(arrList, ["file_contents", "preview_image"]))?[0];

		    return (versionModel != null, versionModel);
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
	    public async Task<(bool, HpVersion?)> HasLocalVersion()
	    {
			HpVersion? versionModel = null;
		    if (hackFile?.HasRemoteVersion == null || !(bool)hackFile.HasRemoteVersion ||
		        hackFile.HpVersionId == null) return (false, versionModel);
		    
		    versionModel = await HpVersion.GetRecordByIdAsync((int)hackFile.HpVersionId, HpVersion.UsualExcludedFields);
		    return (true, versionModel);
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
public static class OdooFieldExtension
{
	public static readonly Dictionary<OdooFieldType, Type[]> Schema = new()
	{
		{ OdooFieldType.Char,       new[] { typeof(string) } },
		{ OdooFieldType.Text,       new[] { typeof(string) } },
		{ OdooFieldType.Html,       new[] { typeof(string) } },
		{ OdooFieldType.Integer,    new[] { typeof(int), typeof(long) } },
		{ OdooFieldType.Float,      new[] { typeof(double), typeof(decimal) } },
		{ OdooFieldType.Monetary,   new[] { typeof(decimal) } },
		{ OdooFieldType.Boolean,    new[] { typeof(bool) } },
		{ OdooFieldType.Date,       new[] { typeof(DateTime) } },
		{ OdooFieldType.DateTime,   new[] { typeof(DateTime) } },
		{ OdooFieldType.Binary,     new[] { typeof(string), typeof(byte[]) } }, // Odoo sometimes base64 encodes
        { OdooFieldType.Many2One,   new[] { typeof(object[]), typeof(ValueTuple<int,string>) } },
		{ OdooFieldType.One2Many,   new[] { typeof(int[]), typeof(ArrayList) } },
		{ OdooFieldType.Many2Many,  new[] { typeof(int[]), typeof(ArrayList) } },
		{ OdooFieldType.Selection,  new[] { typeof(string), typeof(int) } },
		{ OdooFieldType.Reference,  new[] { typeof(string), typeof(object[]) } },
		{ OdooFieldType.Serialized, new[] { typeof(Dictionary<string,object>), typeof(string) } },
	};
	private static bool IsValidShape(this OdooFieldType type, object? value) =>
	type switch
	{
		OdooFieldType.Char
			or OdooFieldType.Text
			or OdooFieldType.Html
			=> value is string,

		OdooFieldType.Integer
			=> value is int or long or string,

		OdooFieldType.Float
			or OdooFieldType.Monetary
			=> value is double or decimal or string,

		OdooFieldType.Boolean
			=> value is bool or int or string,

		OdooFieldType.Date
			or OdooFieldType.DateTime
			=> value is string or DateTime,

		OdooFieldType.Binary
			=> value is string or byte[],

		OdooFieldType.Many2One
			=> value is object[],

		OdooFieldType.One2Many
			or OdooFieldType.Many2Many
			=> value is int[] or System.Collections.ArrayList,

		OdooFieldType.Selection
			=> value is string or int,

		OdooFieldType.Reference
			=> value is string or object[],

		OdooFieldType.Serialized
			=> value is string or System.Collections.Generic.Dictionary<string, object>,

		_ => true
	};

}
public static class OdooFieldHelpers
{

	public static bool TryCast<T>(object? value, out T? result)
	{
		if (value is T t)
		{
			result = t;
			return true;
		}

		result = default;
		return false;
	}
	public static bool TryCast<TIn, TOut>(
		object? value,
		in Func<TIn, TOut> func,
		out TOut? result)
	{
		if (value is TIn t)
		{
			result = func(t);
			return true;
		}
		result = default;
		return false;
	}
	public static bool TryCastAny<T1, T2, TOut>(
		object? value,
		in Func<T1, TOut> f1,
		in Func<T2, TOut> f2,
		out TOut result)
	{
		return value switch
		{
			T1 t1 => ReturnTrue(result = f1(t1)),
			T2 t2 => ReturnTrue(result = f2(t2)),
			_ => ReturnFalse(result = default!),
		};
	}
	public static bool TryCastAny<T1, T2, T3, TOut>(
		object? value,
		in Func<T1, TOut> f1,
		in Func<T2, TOut> f2,
		in Func<T3, TOut> f3,
		out TOut result)
	{
		return value switch
		{
			T1 t1 => ReturnTrue(result = f1(t1)),
			T2 t2 => ReturnTrue(result = f2(t2)),
			T3 t3 => ReturnTrue(result = f3(t3)),
			_ => ReturnFalse(result = default!),
		};
	}
	public static bool TryCastAny<T1, T2, T3, T4, TOut>(
		object? value,
		in Func<T1, TOut> f1,
		in Func<T2, TOut> f2,
		in Func<T3, TOut> f3,
		in Func<T4, TOut> f4,
		out TOut result)
	{
		return value switch
		{
			T1 t1 => ReturnTrue(result = f1(t1)),
			T2 t2 => ReturnTrue(result = f2(t2)),
			T3 t3 => ReturnTrue(result = f3(t3)),
			T4 t4 => ReturnTrue(result = f4(t4)),
			_ => ReturnFalse(result = default!),
		};
	}
	private static bool ReturnTrue<T>(in T _) => true;
	private static bool ReturnFalse<T>(in T _) => false;
	private static bool TryNull<TIn, TOut>(TIn input, Func<TIn, TOut> func, out TOut? result)
	{
		try
		{
			result = func(input);
		}
		finally
		{
			result = default!;
		}
		return false;
	}
	private static TOut? TryNull<TIn, TOut>(TIn input, Func<TIn, TOut> func)
	{
		try
		{
			return func(input);
		}
		catch
		{
			return default;
		}
	}
	public static object? Convert(OdooFieldType type, object? value)
	{
		value = Normalize(type, value);
		return value is null
			? null
			: type switch
			{
				// int
				OdooFieldType.Integer => ConvertInteger(value),
				// string
				OdooFieldType.Char
					or OdooFieldType.Text
					or OdooFieldType.Html => ConvertString(value),
				// bool
				OdooFieldType.Boolean => ConvertBoolean(value),
				// DateTime
				OdooFieldType.DateTime => ConvertDateTime(value),
				// Date
				OdooFieldType.Date => ConvertDate(value),
				// ValueTuple<int,string>?
				OdooFieldType.Many2One => ConvertMany2one(value),
				// int[]
				OdooFieldType.One2Many
					or OdooFieldType.Many2Many => ConvertOne2many(value),
				// byte[]
				OdooFieldType.Binary 
					or OdooFieldType.Image => ConvertBinary(value),
				OdooFieldType.Json => ConvertSerializedString(value),
				OdooFieldType.Float => ConvertFloat(value),
				OdooFieldType.Monetary => ConvertMonetary(value),
				OdooFieldType.Selection => ConvertSelectionString(value),
				OdooFieldType.Reference => ConvertReferenceString(value),
				OdooFieldType.Serialized => ConvertSerializedDict(value),
				_ => value
			};
	}

	// --------------------------------------------------------------------
	// Normalization + shape validation
	// --------------------------------------------------------------------

	private static object? Normalize(OdooFieldType type, object? value)
	{
		// Global Odoo rule: non-boolean fields use False for null
		if (value is bool b && b == false && type != OdooFieldType.Boolean)
			return null;

		return value;
	}

	private static bool IsValidShape(OdooFieldType type, object? value) =>
		type switch
		{
			OdooFieldType.Char
				or OdooFieldType.Text
				or OdooFieldType.Html
				=> value is null or string,

			OdooFieldType.Integer
				=> value is null or int or long or string,

			OdooFieldType.Float
				=> value is null or double or decimal or string,

			OdooFieldType.Monetary
				=> value is null or decimal or double or string,

			OdooFieldType.Boolean
				=> value is null or bool or int or string,

			OdooFieldType.Date
				or OdooFieldType.DateTime
				=> value is null or string or DateTime,

			OdooFieldType.Binary
				=> value is null or string or byte[],

			OdooFieldType.Many2One
				=> value is null or object[],

			OdooFieldType.One2Many
				or OdooFieldType.Many2Many
				=> value is null or int[] or ArrayList,

			OdooFieldType.Selection
				=> value is null or string or int,

			OdooFieldType.Reference
				=> value is null or string or object[],

			OdooFieldType.Serialized
				=> value is null
					or string
					or Dictionary<string, object>,

			_ => true
		};

	private static int FromInt(int i) => i;
	private static int FromLong(long l) => (int)l;
	private static float FromFloat(float f) => f;
	private static float FromDouble(double d) => (float)d;
	private static float FromDecimal(decimal m) => (float)m;
	private static decimal FromDecimalMonetary(decimal m) => m;
	private static decimal FromDoubleMonetary(double d) => (decimal)d;
	private static (int id, string name)? FromObjectArray(object value)
		=> value is IList list && list.Count >= 2
			? (list[0] as int? ?? 0, list[1] as string ?? "")
			: null;
	private static (int id, string name)? FromArrayList(object value)
		=> FromObjectArray(value);

	// Char/Text/Html -> string?
	public static string ConvertString(object value)
	{
		if (TryCast<string>(value, out var s))
			return s!;

		return value.ToString()!;
	}
	// Integer -> int
	public static int ConvertInteger(object value)
	{
		// Allowed shapes: int, long, string (validated by IsValidShape)
		if (TryCastAny<int, long, int>(
				value,
				FromInt,
				FromLong,
				out var result))
			return result;

		return default;
	}
	// Float -> double
	public static float ConvertFloat(object value)
	{
		// Allowed shapes: double, decimal, string
		if (TryCastAny<float, double, decimal, float>(
				value,
				FromFloat,
				FromDouble,
				FromDecimal,
				out var result))
			return result;

		return default;
	}
	// Monetary -> decimal
	public static decimal ConvertMonetary(object value)
	{
		// Allowed shapes: decimal, double, string
		if (TryCastAny<decimal, double, decimal>(
				value,
				FromDecimalMonetary,
				FromDoubleMonetary,
				out var result))
			return result;

		return default;
	}
	// Boolean -> bool
	public static bool ConvertBoolean(object value)
		=> TryCast<bool>(value, out var b) && b;
	// Date -> DateTime (date-only)
	public static DateTime ConvertDate(object value)
		=> ConvertDateTime(value).Date;
	// DateTime -> DateTime
	public static DateTime ConvertDateTime(object value)
	{
		if (value is DateTime dt)
			return dt;

		if (TryCast<string>(value, out var s) &&
			DateTime.TryParse(s, out var parsed))
			return parsed;

		return default;
	}
	// Binary -> byte[]?
	public static byte[] ConvertBinary(object value)
	{
		return TryCast<string>(value, out var s) && !string.IsNullOrEmpty(s)
			? TryNull(s, System.Convert.FromBase64String)!
			: TryCast<byte[]>(value, out var bytes) 
				? bytes! 
				: default!;
	}
	// Many2one -> (int id, string name)?
	// Odoo shape: [id, "Name"]
	public static Many2One ConvertMany2one(object value)
	{
		if (!TryCast<object, ValueTuple<int, string>?>(
				value,
				FromObjectArray,
				out var arr))
			return new Many2One { Id=0, name="" };

		var id = ConvertInteger(arr?.Item1 ?? 0);
		var name = ConvertString(arr?.Item2 ?? "") ?? string.Empty;

		return new Many2One { Id = id, name = name }; ;
	}
	// One2many/Many2many -> int[]
	public static One2Many ConvertOne2many(object value)
	{
		if (TryCast<int[]>(value, out var ids))
			return ids!;

		if (TryCast<IList>(value, out var al))
			return new One2Many { Ids = [.. al?.Cast<int>() ?? []] };

		return [];
	}
	public static Many2Many ConvertMany2many(object value)
	{
		if (TryCast<int[]>(value, out var ids))
			return ids!;

		if (TryCast<IList>(value, out var al))
			return [.. al?.Cast<int>() ?? []];

		return [];
	}
	// Selection -> preserve as string or int (you can refine as needed)
	public static string ConvertSelectionString(object value)
		=> value is string s ? s : value.ToString()!;
	public static int ConvertSelectionInt(object value)
		=> value is int id ? ConvertInteger(value) : 0;
	// Reference -> typically "model_name,ID" or [model, id]
	public static string ConvertReferenceString(object value)
		=> ConvertString(value);
	public static (string model, int id) ConvertReferenceToTuple(object value)
	{
		if (value is string s)
		{
			var parts = s.Split(',');
			if (parts.Length == 2 && int.TryParse(parts[1], out var id))
				return (parts[0], id);
			return ("", 0);
		}
		else if (value is object[] arr && arr.Length >= 2)
		{
			var model = arr[0]?.ToString() ?? string.Empty;
			var id = ConvertInteger(arr[1]!);
			return (model, id);
		}
		return ("", 0);
	}
	// Serialized -> leave as-is, or decode JSON
	public static string ConvertSerializedString(object value)
		=> value is string s ? s : string.Empty;
	public static Dictionary<string, object> ConvertSerializedDict(object value)
	{
		return JsonConvert.DeserializeObject<Dictionary<string, object>>(ConvertSerializedString(value)) ?? [];
	}
}