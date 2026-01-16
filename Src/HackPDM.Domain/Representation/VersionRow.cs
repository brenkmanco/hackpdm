using System;
using HackPDM.Abstractions;

namespace HackPDM.Domain.Representation;

public class VersionRow : DataGridData, IRowData<VersionRow>
{
	// (MVVM) VIEW
	public int Id { get; set; }
	public long? FileSize { get; set; }
	public int? DirectoryId { get; set; }
	public int? NodeId { get; set; }
	public int? EntryId { get; set; }
	public int? AttachmentId { get; set; }
	public DateTime? ModifyDate { get; set; }
	public string? Checksum { get; set; }
	public string? OdooCompletePath { get; set; }
	public VersionRow Clone()
	{
		var cItem = new VersionRow
		{
			Name = this.Name is null ?  null : new(this.Name),
			Text = this.Text is null ?  null : new(this.Text),
			Id = this.Id,
			FileSize = this.FileSize,
			DirectoryId = this.DirectoryId,
			NodeId = this.NodeId,
			EntryId = this.EntryId,
			AttachmentId = this.AttachmentId,
			ModifyDate = this.ModifyDate,
			Checksum = this.Checksum is null ?  null : new(this.Checksum),
			OdooCompletePath = this.OdooCompletePath is null ?  null : new(this.OdooCompletePath),
		};

		return cItem;
	}
}


