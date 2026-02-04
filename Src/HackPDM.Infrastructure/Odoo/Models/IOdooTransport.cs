using System;
using System.Collections.Generic;
using System.Text;

namespace HackPDM.Infrastructure.Odoo.Models
{
	internal interface IOdooTransport<T> where T : HpBaseModelTransport<T>, new()
	{
		static abstract Dictionary<string, object?> ToOdoo(T model);
	}
}
