using HackPDM.Abstractions;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.Representation;

public class BasicStatusMessage : IRowData<BasicStatusMessage>
{
	// // (MVVM) VIEW
	public StatusMessage Status { get; set; } = StatusMessage.OTHER;
	public string? Message { get; set; }

	public BasicStatusMessage Clone() => new()
	{
		Status = this.Status,
		Message = this.Message is null ?  null : new(this.Message),
	};
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
public class Wrap<T>(T value) where T : struct
{
	T Value = value;
	public static implicit operator T(Wrap<T> wrap) => wrap.Value;
	public static implicit operator Wrap<T>(T value) => new Wrap<T>(value);
}


