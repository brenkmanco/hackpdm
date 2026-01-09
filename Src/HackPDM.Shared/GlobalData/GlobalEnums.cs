using System;
using System.Collections.Generic;
using System.Text;

namespace HackPDM.Shared.GlobalData
{
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
		Many2one,      // foreign key to another model ---------|
		One2many,      // collection of related records         |
		Many2many,     // many-to-many relation ----------------|
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
	public enum DialogResult
	{
		None,
		OK,
		Cancel,
		Abort,
		Retry,
		Ignore,
		Yes,
		No,
		TryAgain,
		Continue,
	}
	public enum MessageBoxButtons
	{
		OK,
		OKCancel,
		AbortRetryIgnore,
		YesNoCancel,
		YesNo,
		RetryCancel,
		CancelTryContinue,
	}
	public enum MessageBoxIcon
	{
		None,
		Error,
		Question,
		Stop,
		Exclamation,
		Information,
		Warning,
	}
	public enum ContentDialogButtonRepresentation
	{
		None,
		Primary,
		Secondary,
		Close,
	}
}
