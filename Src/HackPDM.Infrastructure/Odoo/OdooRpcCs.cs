using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using HackPDM.Core.General;
using HackPDM.Domain.OdooModels.Models;
using RestSharp;


//
using System.IO;
using HackPDM.Infrastructure.Odoo.Models;
//




using HackPDM.Shared.GlobalData;
using HackPDM.Infrastructure.RPC;

using RestSharp.Serializers;
using System.Text.Json;

namespace HackPDM.Infrastructure.Odoo;

public static class OdooClient
{
    public static readonly string XmlrpcEndpoint = "/xmlrpc";
    public const string JsonRPCEndpoint = "/jsonrpc";
    public static readonly string AuthenticationEndpoint = "/common";

    public static readonly string ObjectEndpoint = "/object";


    private static int _commonTimeout = 3000;
    public static int CommonTimeout
    {
        get
        {
            return _commonTimeout;
        }
        set
        {
            _commonTimeout = value;
        }
    }

    private static int _objectTimeout = 5000;
    public static int ObjectTimeout
    {
        get
        {
            return _objectTimeout;
        }
        set
        {
            _objectTimeout = value;
        }
    }

    private static string _latestException = "";
    public static string? LatestException
    {
        get => _latestException;
        set => _latestException = value ?? "";
    }
    public static async Task<int> CorrectUserId()
    {
        try
        {
			return await Task.Run(()=>OdooDefaults.Instance.OdooId is 0 ? 0 : 1);
        }
        catch 
        {
		    return -1;
        }
    }
        
    public static async Task<bool> CorrectOdooAddress()
    {
		using Ping pinger = new();
		return OdooDefaults.Instance.OdooAddress is not null 
			&& await pinger.SendPingAsync(OdooDefaults.Instance.OdooAddress) is PingReply reply 
			&& reply.Status == IPStatus.Success;
	}
	public static async Task<bool> CorrectOdooPort()
    {
        try
        {
            return await Task.Run(() =>
            {
			    if (OdooDefaults.Instance.OdooAddress is not null
				    && OdooDefaults.Instance.OdooPort is not null
				    && new TcpClient( OdooDefaults.Instance.OdooAddress, int.Parse( OdooDefaults.Instance.OdooPort ) ) is not null)
			    {
				    return true;
			    }
                return false;
            });
        }
        catch {}
		return false;
    }
    public async static Task<int?> LoginJson(int? timeout = null)
    {
		if( string.IsNullOrEmpty( OdooDefaults.Instance?.OdooDb )
			|| string.IsNullOrEmpty( OdooDefaults.Instance.OdooUser )
			|| string.IsNullOrEmpty( OdooDefaults.Instance.OdooPass ) )
			return 0;

		_latestException = "";
		int userTimeout = timeout == null ? _commonTimeout : (int)timeout;
        
        JsonRpcRequest request = new()
        {
            Params = new()
            {
                Service = "common",
                Method = "login",
                Args = 
                [
                    OdooDefaults.Instance.OdooDb,
					OdooDefaults.Instance.OdooUser,
					OdooDefaults.Instance.OdooPass,
				],
            }
        };

		int ui = 0;
		try
		{
			var response = await request.SendJsonAsync(OdooDefaults.Instance.OdooUrl + JsonRPCEndpoint, timeout: userTimeout);
            
			if( response is null )
			{
				_latestException = "web request exception";
				return 0;
			}
			else if( response?.Error is not null )
			{
				_latestException = response?.Error?.Describe() ?? "unknown exception";
				return 0;
			}
            switch( response?.Result?.ValueKind )
            {
                case JsonValueKind.Number:
                    ui = response?.Result?.GetInt32() ?? 0;
                    break;
                case JsonValueKind.String:
					ui = int.Parse(response?.Result?.GetString() ?? "0");
					break;
                case JsonValueKind.True:
				case JsonValueKind.False:
                case JsonValueKind.Array:
                case JsonValueKind.Null:
				case JsonValueKind.Undefined:
                default:
					_latestException = "login username or password failed";
                    return 0;
			}			
		}
		catch( Exception exc )
		{
			_latestException = exc.Message;
			return 0;
		}

		OdooDefaults.Instance.OdooId = ui;
		return OdooDefaults.Instance.OdooId;
	}
	public static async Task<object?> ExecuteJsonAsync( string model, string method, ArrayList parameters, int? timeout = null )
	{
		_latestException = "";
		int userTimeout = timeout == null ? _objectTimeout : (int)timeout;

		JsonRpcRequest request = new()
		{
			Params = new()
			{
				Service = "object",
				Method = "execute",
				Args =
				[
					OdooDefaults.Instance.OdooDb,
					OdooDefaults.Instance.OdooId,
					OdooDefaults.Instance.OdooPass,
					model,
					method,
					.. parameters
				],
			}
		};

		object? resVal;
		try
		{
			//XmlRpcResponse? objectResponse = request.Send(OdooDefaults.Instance.OdooUrl + XmlrpcEndpoint + ObjectEndpoint, userTimeout);
			var response = await request.SendJsonAsync(OdooDefaults.Instance.OdooUrl + JsonRPCEndpoint, timeout: userTimeout);
			if( response is null )
			{
				throw new Exception( "web exception" );
			}
			else if( response?.Error is not null )
			{
				// possible for faultCode to have a null value
				throw new Exception( response.Error.Describe() );
				//latestException = objectResponse.Value.ToString();
				//return null;
			}
			resVal = response?.Result?.ToNativeObject();
		}
		catch( Exception exc )
		{
			_latestException = exc.Message;
			return null;
		}

		return resVal;
	}

	public static async Task<T> CommandAsync<T>(string model, string method, ArrayList execParams, int? timeout = null) where T : new()
    {
        object? response = await ExecuteJsonAsync(model, method, execParams, timeout);

        if (typeof(T).IsValueType)
        {
            return (T)(response ?? default(T));
        }
        return (T)(response ?? new T());
    }



    // crud operations
    public static async Task<int> CreateCommitAsync(int? timeout = null)
        => await CommandAsync<int>(OdooDefaultsConstants.HP_PDM_COMMIT, "create", [new Hashtable { { "node_by", OdooDefaults.Instance?.MyNode?.id ?? 0} }], timeout);
    public static async Task<int> CreateAsync(string model, Hashtable values, int? timeout = null)
        => await CommandAsync<int>(model, "create", [values], timeout); //
    public static async Task<ArrayList> CreateAsync(string model, ArrayList arrayValues, int? timeout = null)
        => await CommandAsync<ArrayList>(model, "create", [arrayValues], timeout); //
	public static async Task<ArrayList> ReadAsync(string model, ArrayList ids, ArrayList fields, int? timeout = null)
        => await CommandAsync<ArrayList>(model, "read", [ids, fields], timeout); //
    public static async Task<bool> UpdateAsync(string model, int id, Hashtable values, int? timeout = null) 
        => await CommandAsync<bool>(model, "write", [id, values], timeout); //
    public static async Task<bool> DeleteAsync(string model, ArrayList execParams, int? timeout = null)
        => await CommandAsync<bool>(model, "unlink", execParams, timeout); //


    // search functions
    public static async Task<ArrayList> SearchAsync(string model, ArrayList domain, int? timeout = null)
        => await CommandAsync<ArrayList>(model, "search", domain, timeout); // read-only
    public static async Task<int> SearchCountAsync(string model, ArrayList execParams, int? timeout = null)
        => await CommandAsync<int>(model, "search_count", execParams, timeout); // read-only
    public static async Task<ArrayList> BrowseAsync(string model, ArrayList execParams, int? timeout = null)
        => await CommandAsync<ArrayList>(model, "search_read", execParams, timeout); // read-only
    public static async Task<ArrayList> SmartSearchAsync(string model, ArrayList? domain, ArrayList? ids, ArrayList? fields, int? timeout = null)
        => await CommandAsync<ArrayList>(model, "smart_read", [domain, ids ?? [], fields ?? []], timeout); // read-only
	public static async Task<ArrayList> RelatedBrowseAsync(string model, ArrayList execParams, int? timeout = null)
        => await CommandAsync<ArrayList>(model, "related_browse", execParams, timeout); // read-only
	public static async Task<ArrayList> RelatedSearchAsync(string model, ArrayList execParams, int? timeout = null)
		=> await CommandAsync<ArrayList>(model, "related_search_browse", execParams, timeout); // read-only
																					// field functions
    // "fields_view_get" is depreciated


    // common functions

}
public static class OdooClient<T> where T : HpBaseModelTransport<T>, new()
{
    private static readonly string _model = HpBaseModelTransport<T>.GetHpModel();

	public static Task<object?> ExecuteJsonAsync( string method, ArrayList parameters, int? timeout = null )
		=> OdooClient.ExecuteJsonAsync( _model, method, parameters, timeout );

	public static async Task<TReturn> CommandAsync<TReturn>(string method, ArrayList execParams, int? timeout = null) where TReturn : new()
    {
        object? response = await ExecuteJsonAsync(method, execParams, timeout);

        if (typeof(TReturn).IsValueType)
        {
            return (TReturn)(response ?? default(T));
        }
        return (TReturn)(response ?? new T());
    }

    public static async Task<ArrayList> ReadAsync(ArrayList ids, ArrayList fields, int? timeout = null)
        => await CommandAsync<ArrayList>("read", [ids, fields], timeout);
    public static async Task<bool> DeleteAsync(ArrayList execParams, int? timeout = null)
        => await CommandAsync<bool>("unlink", execParams, timeout);
}
