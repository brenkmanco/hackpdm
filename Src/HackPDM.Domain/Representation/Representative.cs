using HackPDM.Abstractions;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.Representation;

public class BasicStatusMessage
{
	// // (MVVM) VIEW
	public StatusMessage Status { get; set; } = StatusMessage.OTHER;
	public string? Message { get; set; }
	public DateTime Timestamp { get; set; } = DateTime.Now;

	public static implicit operator BasicStatusMessage( (StatusMessage status, string message) log ) => new() { Message = log.message, Status = log.status };

}
public partial class DataGridData
{
	public virtual string? Name { get; set; } = "";
	public virtual string? Text
	{
		get => field ??= Name;
		set;
	}
}


