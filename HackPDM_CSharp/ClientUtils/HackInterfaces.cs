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
    
}
