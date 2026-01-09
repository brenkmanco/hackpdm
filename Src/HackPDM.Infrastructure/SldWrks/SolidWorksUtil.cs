using System;
using System.Collections.Generic;
using System.Text;
using HackPDM.Core;
using HackPDM.Infrastructure.Odoo;

namespace HackPDM.Infrastructure.SldWrks
{
	public class SolidWorksUtil
	{
		public static SwDocMgr? DocMgr
		{
			get
			{
				return field ??= !string.IsNullOrEmpty(OdooDefaults.Instance.SwApi) ? new(OdooDefaults.Instance.SwApi) : null;
			}
			set;
		}
		public static SwHelper SwHelper
		{
			get
			{
				return field ??= new();
			}
			set;
		}
	}
}
