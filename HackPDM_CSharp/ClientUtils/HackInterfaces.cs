using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HackPDM
{
    public interface IConvert<T>
    {
        T ConvertFromHT(Hashtable ht);
    }
    public interface IDefaultExclude
    {
        //internal virtual static string[] UsualExcludedFields = [];
    }
    public interface IOdooClient
    {
        public object Execute(string model, string method, ArrayList parameters, int? timeout = null);
        public Treturn Command<Treturn>(string model, string method, ArrayList execParams, int? timeout = null) where Treturn : new();
    }
    public interface IOdooClientAsync
    {
        public Task<object> ExecuteAsync(string model, string method, ArrayList parameters, int? timeout = null);
        public Task<T> CommandAsync<T>(string model, string method, ArrayList execParams, int? timeout = null) where T : new();
    }

    public interface ICrudOperations
    {
        public int          Create      (string model, Hashtable values, int? timeout = null);
        public ArrayList    Read        (string model, ArrayList ids, ArrayList fields, int? timeout = null);
        public bool         Update      (string model, int id, Hashtable values, int? timeout = null);
        public bool         Delete      (string model, ArrayList execParams, int? timeout = null);
    }
    public interface ISearchOperations
    {
        // search functions
        public ArrayList    Search      (string model, ArrayList domain, int? timeout = null);
        public int          SearchCount (string model, ArrayList execParams, int? timeout = null);
        public ArrayList    Browse      (string model, ArrayList execParams, int? timeout = null);
    }
}
