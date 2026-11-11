using System.Collections;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Abstractions;


public struct ContentDialogInfo
{
    public ContentDialogInfo() { }

    public string Message { get; set; } = null;
    public string? Caption { get; set; } = null;
    public string? PrimaryText { get; set; } = null;
    public string? SecondaryText { get; set; } = null;
    public string? CloseText { get; set; } = null;
    public MessageBoxRepresentation ButtonRepresentation { get; set; } =  MessageBoxRepresentation.Primary;
}