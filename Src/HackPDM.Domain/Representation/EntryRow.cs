using System;
using System.IO;
using HackPDM.Abstractions;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.Representation;

public partial class EntryRow : DataGridData
{
	// (MVVM) VIEW
	public int? Id { get; set; }
	public string? Type { get; set; }
	public long? Size { get; set; }
	public FileStatus Status { get; set; } = FileStatus.Lo;
	public IHpUserModel? Checkout { get; set; }
	public IHpCategoryModel? Category { get; set; }
	public DateTime? LocalDate { get; set; }
	public DateTime? RemoteDate { get; set; }
	public string? FullName { get; set; }
	public int? LatestId { get; set; }
	public int? LatestReleaseId { get; set; }
	public virtual FileInfo? LocalFile { get; set; } = null;
	public virtual bool? IsLocal { get; } = null;
	public virtual bool? IsRemote { get; } = null;
	public virtual bool? IsOnlyLocal { get; } = null;
	public virtual bool? IsOnlyRemote { get; } = null;
	public EntryReprType? ReprType { get; set; }
}



