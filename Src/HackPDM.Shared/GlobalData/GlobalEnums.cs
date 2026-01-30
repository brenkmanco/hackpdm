using System;
using System.Collections.Generic;
using System.Text;

namespace HackPDM.Shared.GlobalData
{
	public enum FlowDirection
	{
		LeftToRight,
		TopToBottom,
		TopLeftToBottomRight,
		BottomLeftToTopRight,
	}
	public enum OdooFieldType
	{
		// Basic scalar types
		Char,          // string -------------------------------|
		Text,          // long string / multiline               |
		Html,          // HTML content--------------------------|
		Integer,       // int                                   |
		Float,         // double/decimal -----------------------|
		Monetary,      // decimal with currency                 |
		Boolean,       // bool ---------------------------------|
		Date,          // DateOnly                              |
		DateTime,      // DateTime -----------------------------|
		Binary,        // byte[] (attachments, images)          |
					   //                                                      |      
					   // Relational types                                     |
		Many2One,      // foreign key to another model ---------|
		One2Many,      // collection of related records         |
		Many2Many,     // many-to-many relation ----------------|
					   //                                                      |
					   // Special / computed types                             |
		Selection,     // enum-like choice field                |
		Reference,     // polymorphic relation (model + id) ----|
		Serialized,    // JSON/dict stored in DB                |
					   //                                                      |
		Unknown,       // Catch all case to bruteforce type ----|
	}
	public enum NullChangeType
	{
		NoAssign,
		ModifyWithFunction,
		AssignNull
	}
	public enum NullFixType
	{
		Pass,
		Error,
		Success,
	}
	public enum ChangeType
	{
		Added,
		Removed,
		Updated,
		Selected,
		Clicked,
		Rendering,
		Focused,
		Hovered,
	}
	public enum ReasonForCall
	{
		EndOfUpdate,
		BeginningOfUpdate,
		Other,
	}
	public enum PropertyType
	{
		Text,
		Number,
		Yesno,
		Date,
		None,
	}
	public enum MessageBoxType
	{
		Default,
		ListDetail,
		GridDetail,
		ContentDetail,
		ToolTip,
		Notification,
	}
	public enum MessageBoxWidth
	{
		Default,
		DynamicWidthText,
		DynamicWidthTextWithThreshold,
	}
	public enum DialogResult
	{
		None		= 0,
		OK			= 1,
		Cancel		= 2,
		Abort		= 3,
		Retry		= 4,
		Ignore		= 5,
		Yes			= 6,
		No			= 7,
		TryAgain	= 10,
		Continue	= 11,
	}
	// uint bitmask for messagebox fallbacks
	public enum MessageBoxButtons : uint
	{
		// order of primary=1, secondary=2, tertiary=3
		// 1 button:  1
		// 2 buttons: 2, 1
		// 3 buttons: 3, 1, 2

		// primary
		OK					= 0x00000000,
		// secondary, primary
		OKCancel			= 0x00000001,
		// tertiary, primary, secondary
		AbortRetryIgnore	= 0x00000002,
		// tertiary, primary, secondary
		YesNoCancel			= 0x00000003,
		// secondary, primary
		YesNo				= 0x00000004,
		// secondary, primary
		RetryCancel			= 0x00000005,
		// tertiary, primary, secondary
		CancelTryContinue	= 0x00000006,
	}
	public enum MessageBoxIcon : uint
	{
		None,
		Stop		= 0x00000010,
		Error		= 0x00000010,
		Question	= 0x00000020,
		Exclamation = 0x00000030,
		Warning		= 0x00000030,
		Information = 0x00000040,
	}
	public enum MessageBoxRepresentation : uint
	{
		None		= 0x00000100,
		Primary		= 0x00000100,
		Secondary	= 0x00000200,
		Tertiary		= 0x00000300,
	}
	public enum MessageBoxModal : uint
	{
		AppModal	= 0x00000000,
		SystemModal = 0x00001000,
		TaskModal	= 0x00002000,
	}
	public enum MessageBoxAFeature : uint
	{
		HelpButton = 0x00004000
	}
	public enum MessageBoxWFeature : uint
	{
		HelpButton = 0x00004000
	}
	public enum MessageBoxTypeFallback : uint
	{
		SetForeground	= 0x00010000,
		DesktopOnly		= 0x00020000,
		TopMost			= 0x00040000,
		RightJustify	= 0x00080000,
		RightToLeft		= 0x00100000,
		ServiceNotif	= 0x00200000,
	}
}
