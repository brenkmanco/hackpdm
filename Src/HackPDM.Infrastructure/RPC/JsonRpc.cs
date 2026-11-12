using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HackPDM.Infrastructure.RPC;

/// <summary>A JSON-RPC 2.0 request body as Odoo's <c>/jsonrpc</c> endpoint expects it: method <c>"call"</c> with the service, method and arguments in <see cref="Params"/>.</summary>
/// <remarks>Sent by <see cref="HackPDM.Infrastructure.Odoo.OdooTransport.SendJsonAsync"/>; no <c>id</c> is set.</remarks>
public class JsonRpcRequest
{
	[JsonPropertyName( "jsonrpc" )]
	public string Version { get; set; } = "2.0";
	[JsonPropertyName( "method" )]
	public string Method { get; set; } = "call";

	[JsonPropertyName( "params" )]
	public JsonRpcParams Params { get; set; } = new();

}
/// <summary>The <c>params</c> object of an Odoo JSON-RPC call: which service method to run and its positional arguments.</summary>
public class JsonRpcParams
{
	[JsonPropertyName( "service" )]
	public string? Service { get; set; }

	[JsonPropertyName( "method" )]
	public string? Method { get; set; }

	[JsonPropertyName( "args" )]
	public List<object>? Args { get; set; }

	[JsonPropertyName( "kwargs" )]
	public Dictionary<object, object>? Kwargs { get; set; }
}

/// <summary>A JSON-RPC response: either <see cref="Result"/> or <see cref="Error"/> is set.</summary>
public class JsonRpcResponse
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
/// <summary>The <c>error</c> member of a failed JSON-RPC response; Odoo puts the exception type and message in <see cref="Data"/>.</summary>
public class JsonRpcError
{
	// Odoo sends an integer (e.g. 200); JsonElement accepts either form
	[JsonPropertyName( "code" )]
	public JsonElement? Code { get; set; }

	[JsonPropertyName( "message" )]
	public string? Message { get; set; }

	[JsonPropertyName( "data" )]
	public JsonRpcData? Data { get; set; }

	/// <summary>Builds the most specific error message available: Odoo puts the real one in data.message.</summary>
	/// <returns><c>"{data.name}: {data.message}"</c> (or just data.message when there is no name); otherwise the top-level message, or "unknown error".</returns>
	public string Describe()
		=> !string.IsNullOrEmpty( Data?.Message )
			? ( string.IsNullOrEmpty( Data?.Name ) ? Data!.Message! : $"{Data!.Name}: {Data.Message}" )
			: Message ?? "unknown error";
}
/// <summary>Odoo's error details: the Python exception name, its message, arguments, and the server traceback in <see cref="Debug"/>.</summary>
public class JsonRpcData
{
	[JsonPropertyName( "name" )]
	public string? Name { get; set; }

	[JsonPropertyName( "debug" )]
	public string? Debug { get; set; }

	[JsonPropertyName( "message" )]
	public string? Message { get; set; }

	[JsonPropertyName( "arguments" )]
	public List<object>? Args { get; set; }

	[JsonPropertyName( "context" )]
	public Dictionary<string, JsonElement>? Context { get; set; }
}