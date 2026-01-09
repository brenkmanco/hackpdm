using System;
using System.Collections.Generic;
using System.Text;

namespace HackPDM.UI.Data
{
	public enum ToolTipIcon
	{
		None,
		Info,
		Warning,
		Error
	}
	public enum FormType
	{
		Hfm,
		HSet,
		Oftm,
		OSet,
		Pm,
		So,
		Sd,
		Ss,
		Ts
	}
	public enum HorizontalAlignment
	{
		Left,
		Center,
		Right,
		Stretch
	}
	[Serializable]
	public enum ThemeType
	{
		Default,
		Dark,
		Light,
		Preset1,
	}
}
