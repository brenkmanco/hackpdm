using System;
using System.Collections;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using HackPDM.Extensions.Odoo;
using HackPDM.Odoo.OdooModels;
using HackPDM.Odoo.XmlRpc;

namespace HackPDM.Odoo;

public static class OdooClient
{
    public static readonly string XmlrpcEndpoint = "/xmlrpc";
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
        get;
        set;
    }
    public static async Task<int> CorrectUserId()
    {
        try
        {
			return await Task.Run(()=>OdooDefaults.OdooId is null or 0 ? 0 : 1);
        }
        catch {}
		return -1;
    }
        
    public static async Task<bool> CorrectOdooAddress()
    {
		using Ping pinger = new();
		return OdooDefaults.OdooAddress is not null 
			&& await pinger.SendPingAsync(OdooDefaults.OdooAddress) is PingReply reply 
			&& reply.Status == IPStatus.Success;
	}
	public static bool CorrectOdooPort()
    {
        try
        {
			if (OdooDefaults.OdooAddress is not null
				&& OdooDefaults.OdooPort is not null
				&& new TcpClient( OdooDefaults.OdooAddress, int.Parse( OdooDefaults.OdooPort ) ) is not null)
			{
				return true;
			}
        }
        catch {}
		return false;
    }
    public static int? Login(int? timeout = null)
    {
		if (string.IsNullOrEmpty(OdooDefaults.OdooDb)
			|| string.IsNullOrEmpty(OdooDefaults.OdooUser)
			|| string.IsNullOrEmpty(OdooDefaults.OdooPass)) return 0;
		
		_latestException = "";
        int userTimeout = timeout == null ? _commonTimeout : (int)timeout;

        XmlRpcRequest client = new()
        {
            MethodName = "login",
        };
        client.Params.Clear();
        client.Params.Add(OdooDefaults.OdooDb);
        client.Params.Add(OdooDefaults.OdooUser);
        client.Params.Add(OdooDefaults.OdooPass);
        int ui;
        try
        {
            XmlRpcResponse response = client.Send(OdooDefaults.OdooUrl + XmlrpcEndpoint + AuthenticationEndpoint, timeout: userTimeout);
            if (response is null)
            {
                _latestException = "web request exception";
                return 0;
            }
            else if (response?.IsFault == true)
            {
                _latestException = response.Value.ToString();
                return 0;
            }
            else if (response?.Value is bool)
            {
                _latestException = "login username or password failed";
                return 0;
            }
            ui = (int)response.Value;
        }
        catch (Exception exc)
        {
            _latestException = exc.Message;
            return 0;
        }

        OdooDefaults.OdooId = ui;
        return OdooDefaults.OdooId;
    }

    //  [("res_model", "=", "hp.version"), ("res_id", "=", 1)]
    public static object Execute(string model, string method, ArrayList parameters, int? timeout = null)
    {
        _latestException = "";
        int userTimeout = timeout == null ? _objectTimeout : (int)timeout;

        XmlRpcRequest objectClient = new()
        {
            MethodName = "execute"
        };
        objectClient.Params.Clear();
        objectClient.Params.Add(OdooDefaults.OdooDb);
        objectClient.Params.Add(OdooDefaults.OdooId);
        objectClient.Params.Add(OdooDefaults.OdooPass);
        objectClient.Params.Add(model);
        objectClient.Params.Add(method);

        foreach (object obj in parameters)
            objectClient.Params.Add(obj);

        object resVal;
        try
        {
            XmlRpcResponse? objectResponse = objectClient.Send(OdooDefaults.OdooUrl + XmlrpcEndpoint + ObjectEndpoint, userTimeout);
            if (objectResponse is null)
            {
                throw new Exception("web exception");
            }
            else if (objectResponse?.IsFault == true)
            {
                // possible for faultCode to have a null value
                string faultCode = (string)((Hashtable)objectResponse.Value)["faultCode"];
                throw new Exception(faultCode);
                //latestException = objectResponse.Value.ToString();
                //return null;
            }
            resVal = objectResponse?.Value ?? "no response";
        }
        catch (Exception exc)
        {
            _latestException = exc.Message;
            return null;
        }

        return resVal;
    }

    // generic execute command that'll return the response or the default/empty type
    public static TReturn Command<TReturn>(string model, string method, ArrayList execParams, int? timeout = null) where TReturn : new()
    {
        object response = Execute(model, method, execParams, timeout);

        if (typeof(TReturn).IsValueType)
        {
            return (TReturn)(response ?? default(TReturn));
        }
        return (TReturn)(response ?? new TReturn());
    }


    // CRUD operations
    public static int Create(string model, Hashtable values, int? timeout = null) 
        => Command<int>(model, "create", [values], timeout); //
    public static ArrayList Read(string model, ArrayList ids, ArrayList fields, int? timeout = null) 
        => Command<ArrayList>(model, "read", [ids, fields], timeout); //
    public static ArrayList FastRead(string model, ArrayList ids, ArrayList fields, int? timeout = null) 
        => Command<ArrayList>(model, "fast_read", [ids, fields], timeout);
    public static bool Update(string model, int id, Hashtable values, int? timeout = null) 
        => Command<bool>(model, "write", [id, values], timeout); //
    public static bool Delete(string model, ArrayList execParams, int? timeout = null) 
        => Command<bool>(model, "unlink", execParams, timeout); //
        
    // search functions
    public static ArrayList Search(string model, ArrayList domain, int? timeout = null) 
        => Command<ArrayList>(model, "search", domain, timeout); // read-only
    public static int SearchCount(string model, ArrayList execParams, int? timeout = null) 
        => Command<int>(model, "search_count", execParams, timeout); // read-only
    public static ArrayList Browse(string model, ArrayList execParams, int? timeout = null) 
        => Command<ArrayList>(model, "search_read", execParams, timeout); // read-only
    // takes ids, field_name, fields
    public static ArrayList RelatedBrowse(string model, ArrayList execParams, int? timeout = null)
        => Command<ArrayList>(model, "related_browse", execParams, timeout); // read-only

    // takes domain search, related field name, and return fields
    public static ArrayList RelatedSearch(string model, ArrayList execParams, int? timeout = null)
        => Command<ArrayList>(model, "related_search_browse", execParams, timeout); // read-only

    // field functions
    public static Hashtable GetFields(string model, ArrayList execParams, int? timeout = null) 
        => Command<Hashtable>(model, "fields_get", execParams, timeout); //
    // "fields_view_get" is depreciated
    public static Hashtable GetFieldViews(string model, ArrayList execParams, int? timeout = null) 
        => Command<Hashtable>(model, "get_view", execParams, timeout); // ?
        
    // common functions
    public static ArrayList NameSearch(string model, ArrayList execParams, int? timeout = null) 
        => Command<ArrayList>(model, "display_name", execParams, timeout); // read-only
    public static Hashtable GetDefault(string model, ArrayList execParams, int? timeout = null) 
        => Command<Hashtable>(model, "default_get", execParams, timeout); //
    public static ArrayList Duplicate(string model, ArrayList execParams, int? timeout = null) 
        => Command<ArrayList>(model, "copy_data", execParams, timeout); //


    // asynchronous commands
    public static async Task<object> ExecuteAsync(string model, string method, ArrayList parameters, int? timeout = null)
    {
        _latestException = "";
        int userTimeout = timeout == null ? _objectTimeout : (int)timeout;

        XmlRpcRequest objectClient = new()
        {
            MethodName = "execute"
        };
        objectClient.Params.Clear();
        objectClient.Params.Add(OdooDefaults.OdooDb);
        objectClient.Params.Add(OdooDefaults.OdooId);
        objectClient.Params.Add(OdooDefaults.OdooPass);
        objectClient.Params.Add(model);
        objectClient.Params.Add(method);

        foreach (object obj in parameters)
            objectClient.Params.Add(obj);

        object resVal;
        try
        {
            XmlRpcResponse objectResponseAsync = await objectClient.SendAsync(OdooDefaults.OdooUrl + XmlrpcEndpoint + ObjectEndpoint, userTimeout);

            if (objectResponseAsync.IsFault)
            {
                // possible for faultCode to have a null value
                string faultCode = (string)((Hashtable)objectResponseAsync.Value)["faultCode"];
                throw new Exception(faultCode);
                //latestException = objectResponse.Value.ToString();
            }
            resVal = objectResponseAsync.Value;
        }
        catch (Exception exc)
        {
            _latestException = exc.Message;
            return null;
        }

        return resVal;
    }

    public static async Task<T> CommandAsync<T>(string model, string method, ArrayList execParams, int? timeout = null) where T : new()
    {
        object response = await ExecuteAsync(model, method, execParams, timeout);

        if (typeof(T).IsValueType)
        {
            return (T)(response ?? default(T));
        }
        return (T)(response ?? new T());
    }



    // crud operations
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
    public static async Task<ArrayList> RelatedBrowseAsync(string model, ArrayList execParams, int? timeout = null)
        => await CommandAsync<ArrayList>(model, "related_browse", execParams, timeout); // read-only
	public static async Task<ArrayList> RelatedSearchAsync(string model, ArrayList execParams, int? timeout = null)
		=> await CommandAsync<ArrayList>(model, "related_search_browse", execParams, timeout); // read-only
																					// field functions
	public static async Task<Hashtable> GetFieldsAsync(string model, ArrayList execParams, int? timeout = null)
        => await CommandAsync<Hashtable>(model, "fields_get", execParams, timeout); //
    // "fields_view_get" is depreciated
    public static async Task<Hashtable> GetFieldViewsAsync(string model, ArrayList execParams, int? timeout = null)
        => await CommandAsync<Hashtable>(model, "get_view", execParams, timeout); // ?


    // common functions
    public static async Task<ArrayList> NameSearchAsync(string model, ArrayList execParams, int? timeout = null)
        => await CommandAsync<ArrayList>(model, "display_name", execParams, timeout); // read-only
    public static async Task<Hashtable> GetDefaultAsync(string model, ArrayList execParams, int? timeout = null)
        => await CommandAsync<Hashtable>(model, "default_get", execParams, timeout); //
    public static async Task<ArrayList> DuplicateAsync(string model, ArrayList execParams, int? timeout = null)
        => await CommandAsync<ArrayList>(model, "copy_data", execParams, timeout); //

    //
    // possible functions
    // 
    // name_create
    // search_fetch // read-only
    // read_group // read-only
    // check_field_access_rights
    // check_access_rights
    // new
    // load
    // name_search
    // display_name // ?
    // name_get // ?
    // get_field_string // ?
    // get_field_help // ?
    // get_field_selection // ?
    // group_names_with_access // ?
    // check // ?
    // check_group // ?
    // check_groups // ?
    // call_cache_clearing_methods // ?
    // check_object_reference // ?
    // toggle_noupdate // ?

    // in 16.0 

    // user_has_groups
    // flush
    // refresh
    // invalidate_cache
    // recompute

    // in 18.0

    // get_property_definition

    // ir.attachment methods

    // force_storage
    // action_get
    // regenerate_assets_bundles
    // create
    // read_group
    // check
    // get_serving_groups

}
public static class OdooClient<T> where T : HpBaseModel<T>, new()
{
    private static string _model = HpBaseModel<T>.GetHpModel();
    public static object Execute(string method, ArrayList parameters, int? timeout = null) 
        => OdooClient.Execute(_model, method, parameters, timeout);

    // generic execute command that'll return the response or the default/empty type
    public static TReturn Command<TReturn>(string method, ArrayList execParams, int? timeout = null) where TReturn : new()
        => Command<TReturn>(method, execParams, timeout, false);
    private static TReturn Command<TReturn>(string method, ArrayList execParams, int? timeout = null, bool isPrivate = true) where TReturn : new()
    {
        object response = Execute(method, execParams, timeout);

        if (typeof(TReturn).IsValueType)
        {
            return (TReturn)(response ?? default(TReturn));
        }
        return (TReturn)(response ?? new TReturn());
    }

    // CRUD operations
    public static int Create(Hashtable values, int? timeout = null)
        => Command<int>("create", [values], timeout); //
    public static ArrayList Read(ArrayList ids, ArrayList fields, int? timeout = null)
        => Command<ArrayList>("read", [ids, fields], timeout); //
    public static ArrayList FastRead(ArrayList ids, ArrayList fields, int? timeout = null)
        => Command<ArrayList>("fast_read", [ids, fields], timeout);
    public static bool Update(int id, Hashtable values, int? timeout = null)
        => Command<bool>("write", [id, values], timeout); //
    public static bool Delete(ArrayList execParams, int? timeout = null)
        => Command<bool>("unlink", execParams, timeout); //

    // search functions
    public static ArrayList Search(ArrayList domain, int? timeout = null)
        => Command<ArrayList>("search", domain, timeout); // read-only
    public static int SearchCount(ArrayList execParams, int? timeout = null)
        => Command<int>("search_count", execParams, timeout); // read-only
    public static ArrayList Browse(ArrayList execParams, int? timeout = null)
        => Command<ArrayList>("search_read", execParams, timeout); // read-only
    public static ArrayList RelatedBrowse(ArrayList execParams, int? timeout = null)
        => Command<ArrayList>("related_browse", execParams, timeout); // read-only
    // field functions
    public static Hashtable GetFields(ArrayList execParams, int? timeout = null)
        => Command<Hashtable>("fields_get", execParams, timeout); //
    // "fields_view_get" is depreciated
    public static Hashtable GetFieldViews(ArrayList execParams, int? timeout = null)
        => Command<Hashtable>("get_view", execParams, timeout); // ?

    // common functions
    public static ArrayList NameSearch(ArrayList execParams, int? timeout = null)
        => Command<ArrayList>("display_name", execParams, timeout); // read-only
    public static Hashtable GetDefault(ArrayList execParams, int? timeout = null)
        => Command<Hashtable>("default_get", execParams, timeout); //
    public static ArrayList Duplicate(ArrayList execParams, int? timeout = null)
        => Command<ArrayList>("copy_data", execParams, timeout); //


    // asynchronous commands
    public static async Task<object> ExecuteAsync(string method, ArrayList parameters, int? timeout = null)
    {
        OdooClient.LatestException = "";
        int userTimeout = timeout == null ? OdooClient.ObjectTimeout : (int)timeout;

        XmlRpcRequest objectClient = new()
        {
            MethodName = "execute"
        };
        objectClient.Params.Clear();
        objectClient.Params.Add(OdooDefaults.OdooDb);
        objectClient.Params.Add(OdooDefaults.OdooId);
        objectClient.Params.Add(OdooDefaults.OdooPass);
        objectClient.Params.Add(_model);
        objectClient.Params.Add(method);

        foreach (object obj in parameters)
            objectClient.Params.Add(obj);

        object resVal;
        try
        {
            XmlRpcResponse objectResponseAsync = await objectClient.SendAsync(OdooDefaults.OdooUrl + OdooClient.XmlrpcEndpoint + OdooClient.ObjectEndpoint, userTimeout);

            if (objectResponseAsync.IsFault)
            {
                // possible for faultCode to have a null value
                string faultCode = (string)((Hashtable)objectResponseAsync.Value)["faultCode"];
                throw new Exception(faultCode);
                //latestException = objectResponse.Value.ToString();
                //return null;
            }
            resVal = objectResponseAsync.Value;
        }
        catch (Exception exc)
        {
            OdooClient.LatestException = exc.Message;
            return null;
        }

        return resVal;
    }

    public static async Task<TReturn> CommandAsync<TReturn>(string method, ArrayList execParams, int? timeout = null) where TReturn : new()
    {
        object response = await ExecuteAsync(method, execParams, timeout);

        if (typeof(TReturn).IsValueType)
        {
            return (TReturn)(response ?? default(T));
        }
        return (TReturn)(response ?? new T());
    }



    // crud operations
    public static async Task<int> CreateAsync(Hashtable values, int? timeout = null)
        => await CommandAsync<int>("create", [values], timeout); //
    public static async Task<ArrayList> ReadAsync(ArrayList ids, ArrayList fields, int? timeout = null)
        => await CommandAsync<ArrayList>("read", [ids, fields], timeout); //
    public static async Task<bool> UpdateAsync(int id, Hashtable values, int? timeout = null)
        => await CommandAsync<bool>("write", [id, values], timeout); //
    public static async Task<bool> DeleteAsync(ArrayList execParams, int? timeout = null)
        => await CommandAsync<bool>("unlink", execParams, timeout); //


    // search functions
    public static async Task<ArrayList> SearchAsync(ArrayList domain, int? timeout = null)
        => await CommandAsync<ArrayList>("search", domain, timeout); // read-only
    public static async Task<int> SearchCountAsync(string model, ArrayList execParams, int? timeout = null)
        => await CommandAsync<int>("search_count", execParams, timeout); // read-only
    public static async Task<ArrayList> BrowseAsync(ArrayList execParams, int? timeout = null)
        => await CommandAsync<ArrayList>("search_read", execParams, timeout); // read-only
    public static async Task<ArrayList> RelatedBrowseAsync(ArrayList execParams, int? timeout = null)
        => await CommandAsync<ArrayList>("related_browse", execParams, timeout); // read-only

    // field functions
    public static async Task<Hashtable> GetFieldsAsync(ArrayList execParams, int? timeout = null)
        => await CommandAsync<Hashtable>("fields_get", execParams, timeout); //
    // "fields_view_get" is depreciated
    public static async Task<Hashtable> GetFieldViewsAsync(ArrayList execParams, int? timeout = null)
        => await CommandAsync<Hashtable>("get_view", execParams, timeout); // ?


    // common functions
    public static async Task<ArrayList> NameSearchAsync(ArrayList execParams, int? timeout = null)
        => await CommandAsync<ArrayList>("display_name", execParams, timeout); // read-only
    public static async Task<Hashtable> GetDefaultAsync(ArrayList execParams, int? timeout = null)
        => await CommandAsync<Hashtable>("default_get", execParams, timeout); //
    public static async Task<ArrayList> DuplicateAsync(ArrayList execParams, int? timeout = null)
        => await CommandAsync<ArrayList>("copy_data", execParams, timeout); //

}