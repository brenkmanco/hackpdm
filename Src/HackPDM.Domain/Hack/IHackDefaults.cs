using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using HackPDM.Abstractions;

namespace HackPDM.Domain.Hack
{
	public interface IHackDefaults : IHackDefaultBase
	{
		static virtual IHackDefaults? Instance { get; set; }
	}
}
