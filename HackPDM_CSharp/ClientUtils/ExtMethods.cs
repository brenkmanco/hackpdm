using Nwc.XmlRpc;
using System.Xml;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.Metrics;
using System.Drawing;
using static HackPDM.FileOperations;
using System.Windows.Forms;

namespace HackPDM.ClientUtils
{
    public static class ExtMethods
    {
        // xmlrpc
		private static readonly Encoding _encoding = new ASCIIEncoding();
		private static readonly XmlRpcRequestSerializer _serializer = new();
		private static readonly XmlRpcResponseDeserializer _deserializer = new();

        // useful extension methods
        public static IEnumerable<Tout> Select<Tin, Tout>(this ArrayList list, Func<Tin, Tout> selector)
        {
            foreach (object obj in list)
            {
                yield return selector((Tin)obj);
            }
        }
        public static IEnumerable<Tout> Select<Tin, Tout>(this Hashtable ht, Func<Tin, Tout> selector)
        {
            foreach (object obj in ht)
            {
                yield return selector((Tin)obj);
            }
        }
        public static IEnumerable<Tout> SelectWhere<Tin, Tout>(this Hashtable ht, Func<Tin, Tout> selector, Predicate<Tout> predicate)
        {
            foreach (Tin obj in ht)
            {
                Tout result = selector(obj);
                bool isPredicate = predicate(result);
                if (isPredicate) yield return selector(obj);
            }
        }
        public static IEnumerable<Tout> SelectWhere<Tin, Tout>(this Hashtable ht, Func<Tin, Tout> selector, Func<Tin, Tout, bool> predicate)
        {
            foreach (Tin obj in ht)
            {
                Tout result = selector(obj);
                bool isPredicate = predicate(obj, result);
                if (isPredicate) yield return selector(obj);
            }
        }
        public static IEnumerable<Tout> SelectMany<Tin, Tout>(this Hashtable source, Func<Tin, IEnumerable<Tout>> selector)
        {
            foreach (object item in source)
            {
                if (item is Tin al)
                {
                    foreach (var result in selector(al))
                    {
                        yield return result;
                    }
                }
            }
        }
        public static IEnumerable<Tout> SelectMany<Tin, Tout>(this ArrayList source, Func<Tin, IEnumerable<Tout>> selector)
        {
            foreach (object item in source)
            {
                if (item is Tin al)
                {
                    foreach (var result in selector(al))
                    {
                        yield return result;
                    }
                }
            }
        }
        public static IEnumerable<Tout> SkipSelect<Tin, Tout>(this IEnumerable<Tin> source, Predicate<Tin> predicate, Func<Tin, Tout> selector)
        {
            foreach (Tin obj in source)
            {
                if (!predicate(obj))
                {
                    yield return selector(obj);
                }
            }
        }
        public static IEnumerable<Tout> SkipList<Tout>(this IEnumerable<Tout> source, IEnumerable<Tout> match)
        {
            foreach (Tout obj in source)
            {
                if (!match.Contains(obj))
                {
                    yield return obj;
                }
            }
        }
        public static Hashtable TakeWhere(this Hashtable ht, Predicate<DictionaryEntry> predicate)
        {
            Hashtable newHT = [];
            foreach (DictionaryEntry de in ht)
            {
                bool isPredicate = predicate(de);
                if (isPredicate)
                {
                    newHT.Add(de.Key, de.Value);
                }
            }
            foreach (DictionaryEntry de in newHT)
            {
                ht.Remove(de.Key);
            }
            return newHT;
        }
        public static List<Tout> TakeAndRemove<Tout>(this List<Tout> source, Func<Tout, bool> predicate)
        {
			var takenElements = source.Where(predicate).ToList();

			// Remove the elements that match the predicate
			foreach ( var element in takenElements )
			{
				source.Remove( element );
			}

			return takenElements;
		}
        public static bool TryGetValue(this Hashtable ht, object key, out object value)
        {
            value = ht[key];
            if (value != null) return true;
            return false;
        }
        public static bool ContainsAny<TSource>(this IEnumerable<TSource> source, IEnumerable<TSource> values)
        {
            foreach (TSource value in values)
            {
                foreach (TSource item in source)
                {
                    if (item.Equals(value)) return true;
                }
            }
            return false;
        }
		public static bool SelectContains<Tin, Tout>( this IEnumerable<Tin> source, Tout value, Func<Tin, Tout> selector )
		{
			foreach ( Tin obj in source )
			{
				Tout sourceValue = selector(obj);
				if ( sourceValue.Equals(value) )
					return true;
			}
			return false;
		}
		public static bool SelectContainsAny<Tin, Tout>(this IEnumerable<Tin> source, IEnumerable<Tout> values, Func<Tin, Tout> selector)
        {
            foreach (Tin obj in source)
            {
                Tout value = selector(obj);
                if (values.Contains(value)) return true;
            }
            return false;
        }
        public static IEnumerable<object> Flatten(this IEnumerable source)
        {
            foreach (object obj in source)
            {
                if (obj is IEnumerable ie)
                {
                    foreach (var nestedItem in ie.Flatten())
                    {
                        yield return nestedItem;
                    }
                }
                else
                {
                    yield return obj;
                }
            }
        }
        public static IEnumerable<string> Split(this string str, string delimiter = " ", StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries)
        {
            // "split/this/string"
            if (str.Length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(str));
            }

            if (options < StringSplitOptions.None || options > StringSplitOptions.RemoveEmptyEntries)
            {
                throw new ArgumentException(nameof(options));
            }

            ReadOnlyMemory<char> newMem = str.AsMemory();

            bool isRunning = true;

            while (isRunning)
            {
                string newString;
                string returnString;

                for (int i = 0; i < newMem.Length; i++)
                {
                    int indexDelimLength = delimiter.Length + i + 1;
                    if (indexDelimLength - 1 >= newMem.Length)
                    {
                        returnString = newMem.ToString();
                        if (returnString.HasEndingDelimiter(delimiter))
                        {
                            returnString = newMem.Slice(0, newMem.Length - delimiter.Length).ToString();
                        }
                    
                        if (options == StringSplitOptions.RemoveEmptyEntries && returnString.IsEmptySpace())
                            break;

                        yield return returnString;
                        
                        isRunning = false;
                        break;
                    }

                    if (delimiter == newMem.Slice(i, delimiter.Length).ToString())
                    {
                        returnString = newMem.Slice(0, i).ToString();
                        newString = newMem.Slice(indexDelimLength - 1, newMem.Length - indexDelimLength + 1).ToString();

                        newMem = newString.AsMemory();

                        if (options == StringSplitOptions.RemoveEmptyEntries && returnString.IsEmptySpace())
                            break;

                        yield return returnString;
                        break;
                    }
                }
            }
        }
        public static Tarray Split<Tarray>(this string str, string delimiter = " ", StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries)
            where Tarray : IList, new()
        {
            IEnumerable<string> strSplit = str.Split(delimiter, options);
            Tarray tarray = new();
            foreach (string s in strSplit)
            {
                tarray.Add(s);
            }
            return tarray;
        }
        private static bool HasEndingDelimiter(this string str, in string delimiter)
        {
            if (delimiter.Length > str.Length) return false;

            int strOffset = str.Length - delimiter.Length;
            for (int i = delimiter.Length - 1; i >= 0; i--)
            {
                if (str[i + strOffset] != delimiter[i]) return false;
            }
            return true;
        }
        public static bool GetFileEndType(this string str, out string extension)
        {
            extension = null;
            for (int i = str.Length-1; i >= 0; i--)
            {
                if (str[i] == '.') 
                {
                    extension = str.Substring(i+1).ToLower();
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Determines whether <paramref name="str"/> contains only empty space.
        /// Tests against ' ' \n \t \f \r
        /// </summary>
        /// <param name="str">The string.</param>
        /// <returns>
        ///   <c>true</c> if <paramref name="str"/> is empty space; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsEmptySpace(this string str)
        {
            foreach (char c in str)
            {
                if (
                    !(c == ' '
                    || c == '\n'
                    || c == '\t'
                    || c == '\f'
                    || c == '\r'))
                {
                    return false;
                }
            }
            return true;
        }
        public static List<Hashtable> ScalpWhere(this Hashtable ht, bool fromLeftOver, params Predicate<DictionaryEntry>[] predicates)
        {
            List<Hashtable> hashtables = [];
            foreach (Predicate<DictionaryEntry> predicate in predicates)
            {
                Hashtable newHT = [];
                foreach (DictionaryEntry de in ht)
                {
                    if (predicate(de))
                    {
                        newHT.Add(de.Key, de.Value);
                    }
                }
                if (fromLeftOver)
                {
                    foreach (DictionaryEntry de in newHT)
                    {
                        ht.Remove(de.Key);
                    }
                }
                hashtables.Add(newHT);
            }
            if (!fromLeftOver)
            {
                foreach (Hashtable hash in hashtables)
                {
                    foreach (DictionaryEntry de in hash)
                    {
                        ht.Remove(de.Key);
                    }
                }
            }
            return hashtables;
        }
        public static Dictionary<string, Hashtable> ScalpWhere(this Hashtable ht, bool fromLeftOver, params (string, Predicate<DictionaryEntry>)[] keyPredicates)
        {
            Dictionary<string, Hashtable> hashtables = [];
            foreach ((string, Predicate<DictionaryEntry>) keyPredicate in keyPredicates)
            {
                Hashtable newHT = [];
                foreach (DictionaryEntry de in ht)
                {
                    if (keyPredicate.Item2(de))
                    {
                        newHT.Add(de.Key, de.Value);
                    }
                }
                if (fromLeftOver)
                {
                    foreach (DictionaryEntry de in newHT)
                    {
                        ht.Remove(de.Key);
                    }
                }
                hashtables.Add(keyPredicate.Item1, newHT);
            }
            if (!fromLeftOver)
            {
                foreach (Hashtable hash in hashtables.Values)
                {
                    foreach (DictionaryEntry de in hash)
                    {
                        ht.Remove(de.Key);
                    }
                }
            }
            return hashtables;
        }



        // xmlrpc request
        public async static Task<XmlRpcResponse> SendAsync(this XmlRpcRequest request, string url, int timeout = 0, IWebProxy proxy = null)
        {
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
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

            XmlTextWriter xmlTextWriter = new(httpWebRequest.GetRequestStream(), _encoding);
            _serializer.Serialize(xmlTextWriter, request);
            xmlTextWriter.Flush();
            xmlTextWriter.Close();

            //HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            HttpWebResponse httpWebResponse = (HttpWebResponse)await httpWebRequest.GetResponseAsync();

            StreamReader streamReader = new(httpWebResponse.GetResponseStream());
            
            XmlRpcResponse result = (XmlRpcResponse)_deserializer.Deserialize(streamReader);
            streamReader.Close();
            httpWebResponse.Close();
            return result;
        }

		// treeview functions
		public static TreeNode FindTreeNode( this TreeView view, string path )
		{
			TreeNodeCollection nodes = null;
			TreeNode node = null;
			string[] paths = path.Split('\\');
			try
			{
				for ( int i = 0; i < paths.Length; i++ )
				{
					if ( i == 0 )
						nodes = view.Nodes;
					else
						nodes = node.Nodes;

					bool wasFound = false;
					foreach ( TreeNode n in nodes )
					{
						if ( n.Text == paths [ i ] )
						{
							wasFound = true;
							node = n;
							break;
						}
					}
					if ( !wasFound )
						return null;
				}
                return node;
			}
			catch
			{
				return null;
			}
		}

		// convert to another type
		public static ArrayList ToArrayListIDs<T>( this T [] models ) where T : HpBaseModel<T>, new()
		{
			ArrayList ids = [];
			foreach ( T model in models )
			{
				ids.Add( model.ID );
			}
			return ids;
		}
		public static ConcurrentBag<T> ConvertToBag<T>(this IEnumerable<T> items)
        {
            try { return new ConcurrentBag<T>(items); }
            catch { return null; }
        }
        public static ConcurrentBag<T> ConvertToBag<T>(this IEnumerable items)
        {
            return items.Cast<object>().ConvertToBag<T>();
        }
		public static T [] ToArray<T>( this ArrayList list ) => list.Cast<T>().ToArray();
        public static HashSet<T> ToHashSet<T>( this ArrayList list ) => list.Cast<T>().ToHashSet();
        public static ConcurrentSet<T> ToConcurrentSet<T>( this IEnumerable list )
            => list.Cast<T>().ToConcurrentSet();
        public static ConcurrentSet<T> ToConcurrentSet<T>( this IEnumerable<T> list )
        {
            ConcurrentSet<T> set = new();
            foreach ( T item in list )
            {
                set.Add(item);
            }
            return set;
        }
		public static ArrayList ToArrayList<T>( this IEnumerable<T> source )
		{
			if ( source == null )
				throw new ArgumentNullException( nameof( source ) );

			return [ .. source ];
		}
		public static ArrayList ToArrayList( this IEnumerable source )
		{
			if ( source == null )
				throw new ArgumentNullException( nameof( source ) );

			return [ .. source ];
		}
		public static HackFile [] ToHackArray( this IEnumerable<FileInfo> fileInfos )
			=> fileInfos.Select( file => new HackFile( file ) ).ToArray();
        public static byte[] ToBytes(this Image image) => ImageToByteArray(image);
        public static string ToBase64String(this Image image) => Convert.ToBase64String(image.ToBytes());
	}
}
