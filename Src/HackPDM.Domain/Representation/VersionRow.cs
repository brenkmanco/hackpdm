using System;
using HackPDM.Abstractions;

namespace HackPDM.Domain.Representation;

public class VersionRow : DataGridData
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
}


