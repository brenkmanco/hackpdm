using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using HackPDM.Abstractions;

namespace HackPDM.Domain.Hack
{
	/// <summary>The client's local settings with a process-wide instance.</summary>
	public interface IHackDefaults : IHackDefaultBase
	{
		/// <summary>The process-wide settings.</summary>
		static virtual IHackDefaults Instance { get; set; }
	}
}
