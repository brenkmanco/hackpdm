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
				if (!OperatingSystem.IsWindows()) return null;
				return field ??= !string.IsNullOrEmpty(OdooDefaults.Instance.SwApi) ? new(OdooDefaults.Instance.SwApi) : null;
			}
			set;
		}
	}
}
