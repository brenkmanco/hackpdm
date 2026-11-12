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
		/// <summary>The process-wide settings; every implementation keeps its own.</summary>
		static abstract IHackDefaults Instance { get; set; }
	}
}
