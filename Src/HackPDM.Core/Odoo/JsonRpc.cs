using System.Text.Json;
using System.Text.Json.Serialization;

namespace HackPDM.Core.Odoo;

public sealed class JsonRpcRequest
{
	[JsonPropertyName( "jsonrpc" )]
	public string Version { get; set; } = "2.0";

	[JsonPropertyName( "method" )]
	public string Method { get; set; } = "call";

	[JsonPropertyName( "params" )]
	public JsonRpcParams Params { get; set; } = new();
}

public sealed class JsonRpcParams
{
	[JsonPropertyName( "service" )]
	public string? Service { get; set; }

	[JsonPropertyName( "method" )]
	public string? Method { get; set; }

	[JsonPropertyName( "args" )]
	public List<object?>? Args { get; set; }

	[JsonPropertyName( "kwargs" )]
	public Dictionary<object, object>? Kwargs { get; set; }
}

public sealed class JsonRpcResponse
{
	[JsonPropertyName( "jsonrpc" )]
	public string? Version { get; set; }

	[JsonPropertyName( "id" )]
	public string? ID { get; set; }

	[JsonPropertyName( "result" )]
	public JsonElement? Result { get; set; }

	[JsonPropertyName( "error" )]
	public JsonRpcError? Error { get; set; }
}

public sealed class JsonRpcError
{
	[JsonPropertyName( "code" )]
	public JsonElement? Code { get; set; }

	[JsonPropertyName( "message" )]
	public string? Message { get; set; }

	[JsonPropertyName( "data" )]
	public JsonRpcData? Data { get; set; }

	public string Describe()
		=> !string.IsNullOrEmpty( Data?.Message )
			? ( string.IsNullOrEmpty( Data?.Name ) ? Data!.Message! : $"{Data!.Name}: {Data.Message}" )
			: Message ?? "unknown error";
}

public sealed class JsonRpcData
{
	[JsonPropertyName( "name" )]
	public string? Name { get; set; }

	[JsonPropertyName( "debug" )]
	public string? Debug { get; set; }

	[JsonPropertyName( "message" )]
	public string? Message { get; set; }

	[JsonPropertyName( "arguments" )]
	public List<object?>? Args { get; set; }

	[JsonPropertyName( "context" )]
	public Dictionary<string, JsonElement>? Context { get; set; }
}
