using System.Collections;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Abstractions;


/// <summary>The text and buttons of a message dialog.</summary>
public struct ContentDialogInfo
{
    /// <summary>Creates dialog info with no text and the primary button representation.</summary>
    public ContentDialogInfo() { }

    public string Message { get; set; } = "";
    public string? Caption { get; set; } = null;
    public string? PrimaryText { get; set; } = null;
    public string? SecondaryText { get; set; } = null;
    public string? CloseText { get; set; } = null;
    public MessageBoxRepresentation ButtonRepresentation { get; set; } =  MessageBoxRepresentation.Primary;
}