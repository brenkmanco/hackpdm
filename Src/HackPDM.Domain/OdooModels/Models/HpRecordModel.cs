using System;
using System.Collections.Generic;
using System.Text;

namespace HackPDM.Domain.OdooModels.Models
{
	public class HpRecordModel : HpBaseModel
	{
		public bool IsCreated { get; set; }
		public string Name { get; set; }

		public static implicit operator HpRecordModel(bool v)
		{
			HpRecordModel recordModel = new() { IsCreated = v };
			return recordModel;
		}
	}
	
}
