using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HackPDM.ClientUtils
{
    public class SingletonForm<T> : SingletonFormBase
        where T : SingletonForm<T>, new()
    {
        private static bool _staticInstance = false;
        private static bool _isCreated = false;
        public static T Singleton
        {
            get
            {
                if (field is null)
                {
                    _staticInstance = true;
                    field = new T();
                    _isCreated = true;
                    _staticInstance = false;
                }
                else
                {
                    _isCreated = true;
                }
                return field;
            }
            set
            {
                _isCreated = value is not null;
                field = value;
                field?.IsSingleton = true;
            }
        }

        public override Form SingletonInstance { get => Singleton; internal set => Singleton.SingletonInstance = value; }

        public SingletonForm(bool reassignSingleton = false)
        {
            if (!_staticInstance || reassignSingleton || !_isCreated)
            {
                Singleton = (T)this;
            }
        }
    }
    public abstract class SingletonFormBase : Form
    {
        internal bool IsSingleton { get; set; } = false;
        public abstract Form SingletonInstance
        {
            get;
            internal set;
        }
        //public abstract void ExecuteAfterConstruct();
    }
}
