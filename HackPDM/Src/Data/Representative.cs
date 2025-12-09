using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using HackPDM.Odoo.OdooModels.Models;
using HackPDM.Properties;
using HackPDM.Extensions.Controls;
using HackPDM.Forms.Hack;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

using HackPDM.Extensions.General;
using static HackPDM.Odoo.OdooModels.Models.HpVersionProperty;
using HackPDM.Src.ClientUtils.Types;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using HackPDM.ClientUtils;
using HackPDM.Hack;
using HackPDM.Odoo;

namespace HackPDM.Data;

#region VIEWS
public partial class OperatorsRow : DataGridData, IRowData<OperatorsRow>
{
	// (MVVM) VIEW
	public partial Operators Operator { get; set; }
	public partial string? OpRepr { get; set; }

	public OperatorsRow Clone()
	{
		var cItem = new OperatorsRow
		{
			Name = this.Name?.Cloned(),
			Text = this.Text?.Cloned(),
			Operator = this.Operator,
			OpRepr = this.OpRepr
		};
		return cItem;
	}
}
public partial class EntryRow : DataGridData, IRowData<EntryRow>
{
	// (MVVM) VIEW
	public ImageSource? Icon			{ get; set; }
	public ImageSource? StatusIcon		{ get; set; }
	public int?         Id				{ get; set; }
	public string?      Type			{ get; set; }
	public long?        Size			{ get; set; }
	public FileStatus   Status			{ get; set; } = FileStatus.Lo;
	public HpUser?      Checkout		{ get; set; }
	public HpCategory?  Category		{ get; set; }
	public DateTime?    LocalDate		{ get; set; }
	public DateTime?    RemoteDate		{ get; set; }
	public string?      FullName		{ get; set; }
	public int?			LatestId		{ get; set; }
	public int?			LatestReleaseId { get; set; }	
}
public class HistoryRow : DataGridData, IRowData<HistoryRow>
{
	// (MVVM) VIEW
	public int          Version     { get; set; }
	public HpUser?      ModUser     { get; set; }
	public DateTime?    ModDate     { get; set; }
	public long?         Size        { get; set; }
	public DateTime?    RelDate     { get; set; }
	public HistoryRow() {}
	public HistoryRow Clone()
	{
		var cItem = new HistoryRow
		{
			Name = this.Name?.Cloned(),
			Text = this.Text?.Cloned(),
			Version = this.Version,
			ModUser = this.ModUser,
			ModDate = this.ModDate,
			Size = this.Size,
			RelDate = this.RelDate,
		};
		
		return cItem;
	}
}
public class ParentRow : DataGridData, IRowData<ParentRow>
{
	// (MVVM) VIEW
	public int          Version     { get; set; }
	public string?      BasePath    { get; set; }
	public ParentRow() {}
	public ParentRow Clone()
	{
		var cItem = new ParentRow
		{
			Name = this.Name?.Cloned(),
			Text = this.Text?.Cloned(),
			Version = this.Version,
			BasePath = this.BasePath?.Cloned(),
		};

		return cItem;
	}
}
public class ChildrenRow : DataGridData, IRowData<ChildrenRow>
{
	// (MVVM) VIEW
	public int          Version     { get; set; }
	public string?      BasePath    { get; set; }
	public ChildrenRow() {}
	public ChildrenRow Clone()
	{
		var cItem = new ChildrenRow
		{
			Name = this.Name?.Cloned(),
			Text = this.Text?.Cloned(),
			Version = this.Version,
			BasePath = this.BasePath?.Cloned(),
		};

		return cItem;
	}
}
public class PropertiesRow : DataGridData, IRowData<PropertiesRow>
{
	// (MVVM) VIEW
	public int          Version     { get; set; }
	public int?         Property    { get; set; }
	public string?      Configuration{  get; set; }
	public PropertyType?Type        { get; set; }
	public object?      ValueData       { get; set; }
	public PropertiesRow() {}
	public PropertiesRow Clone()
	{
		var cItem = new PropertiesRow
		{
			Name = this.Name?.Cloned(),
			Text = this.Text?.Cloned(),
			Version = this.Version,
			Property = this.Property,
			Configuration = this.Configuration?.Cloned(),
			Type = this.Type,
			ValueData = Activator.CreateInstance(this.ValueData!.GetType()),
		};
		
		return cItem;
	}
}
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
			Name = this.Name?.Cloned(),
			Text = this.Text?.Cloned(),
			Id = this.Id,
			FileSize = this.FileSize,
			DirectoryId = this.DirectoryId,
			NodeId = this.NodeId,
			EntryId = this.EntryId,
			AttachmentId = this.AttachmentId,
			ModifyDate = this.ModifyDate,
			Checksum = this.Checksum?.Cloned(),
			OdooCompletePath = this.OdooCompletePath?.Cloned(),
		};

		return cItem;
	}
}
public class SearchRow : DataGridData, IRowData<SearchRow>
{
	// (MVVM) VIEW
	public int? Id { get; set; }
	public string? Directory { get; set; }
	public SearchRow Clone()
	{
		var cItem = new SearchRow
		{
			Name = this.Name?.Cloned(),
			Text = this.Text?.Cloned(),
			Id = this.Id,
			Directory = this.Directory,
		};

		return cItem;
	}
}
//public class SearchPropRow : DataGridData, IRowData
//{
//	// (MVVM) VIEW
//	public string? Comparer { get; set; }
//	public string? Value { get; set; }
//}
public class SearchPropertiesRow : DataGridData, IRowData<SearchPropertiesRow>
{
	// (MVVM) VIEW
	public int ID { get; set; }
	public Operators Comparer { get; set; }
	public string? Value { get; set; }
	public bool IsTextOrDate { get; set; }
	public SearchPropertiesRow Clone()
	{
		var cItem = new SearchPropertiesRow
		{
			ID = this.ID,
			Name = this.Name?.Cloned(),
			Text = this.Text?.Cloned(),
			Comparer = this.Comparer,
			Value = this.Value?.Cloned(),
			IsTextOrDate = this.IsTextOrDate,
		};

		return cItem;
	}
}
public class FileTypeRow : DataGridData, IRowData<FileTypeRow>
{
	// (MVVM) VIEW
	public string? Extension { get; set; }
	public string? Category { get; set; }
	public string? RegEx { get; set; }
	public string? Description { get; set; }
	public FileTypeRow Clone()
	{
		var cItem = new FileTypeRow
		{
			Name = this.Name?.Cloned(),
			Text = this.Text?.Cloned(),
			Extension = this.Extension?.Cloned(),
			Category = this.Category?.Cloned(),
			RegEx = this.RegEx?.Cloned(),
			Description = this.Description?.Cloned(),
		};

		return cItem;
	}
}
public class FileTypeEntryFilterRow : DataGridData, IRowData<FileTypeEntryFilterRow>
{
	// (MVVM) VIEW
	public int Id { get; set; }
	public string? Proto { get; set; }
	public string? RegEx { get; set; }
	public string? Description { get; set; }
	public FileTypeEntryFilterRow Clone()
	{
		var cItem = new FileTypeEntryFilterRow
		{
			Name = this.Name?.Cloned(),
			Text = this.Text?.Cloned(),
			Id = this.Id,
			Proto = this.Proto?.Cloned(),
			RegEx = this.RegEx?.Cloned(),
			Description = this.Description?.Cloned(),
		};

		return cItem;
	}
}
public class FileTypeLocRow : DataGridData, IRowData<FileTypeLocRow>
{
	// (MVVM) VIEW
	public string? Extension { get; set; }
	public string? Status { get; set; }
	public string? Example { get; set; }
	public FileTypeLocRow Clone()
	{
		var cItem = new FileTypeLocRow
		{
			Name = this.Name?.Cloned(),
			Text = this.Text?.Cloned(),
			Extension = this.Extension?.Cloned(),
			Status = this.Status?.Cloned(),
			Example = this.Example?.Cloned(),
		};

		return cItem;
	}
}
public class FileTypeLocDatRow : DataGridData, IRowData<FileTypeLocDatRow>
{
	// (MVVM) VIEW
	public string? Extension { get; set; }
	public string? RegEx { get; set; }
	public string? Category { get; set; }
	public string? Description { get; set; }
	public object? Icon { get; set; } // Type is Unknown, so use object
	public object? RemoveIcon { get; set; } // Type is Unknown, so use object
	public FileTypeLocDatRow Clone()
	{
		var cItem = new FileTypeLocDatRow
		{
			Name = this.Name?.Cloned(),
			Text = this.Text?.Cloned(),
			Extension = this.Extension?.Cloned(),
			RegEx = this.RegEx?.Cloned(),
			Category = this.Category?.Cloned(),
			Description = this.Description?.Cloned(),
			Icon = this.Icon,
			RemoveIcon = this.RemoveIcon,
		};

		return cItem;
	}
}
public partial class TreeData : IEnumerable<TreeData>
{
	// (MVVM) VIEW
	public partial string? Name { get; set; }
	public partial string? FullPath { get; }
	public partial TreeData? Parent { get; }
	public object? Tag { get; set; }
	public int? DirectoryId { get; set; }
	public ImageSource? Icon { get; set; }
	public TreeView? ParentTree { get; internal set; }
	public TreeViewNode? Node { get; internal set; }
	public partial TreeViewItem? VisualContainer { get; }
	public partial IEnumerable<TreeData>? Children { get; }
	public partial bool IsLinked { get; }
	public partial bool HasChildren { get; }
	public partial int Depth { get; }
	public partial bool IsExpanded { get; set; }
}
#endregion

#region VIEWMODELS
public partial class OperatorsRow : DataGridData
{
	// (MVVM) ViewModel
	public partial Operators Operator 
	{
		get => field;
		set
		{
			field = value;
			OpRepr = OperatorConverter.OperatorToString(value);
		}
	}
	public partial string? OpRepr 
	{
		get => field;
		set
		{
			field = value;
		} 
	}
}
public partial class TreeData : IEnumerable<TreeData>
{
	// (MVVM) ViewModel
	public partial string? Name 
	{
		get
		{
			return field ??= Depth is < 0 ? null : StorageBox.EMPTY_PLACEHOLDER;
		} 
		set; 
	}
	public partial string? FullPath => Parent is null ? Depth < 0 ? null : Name : $"{Parent?.FullPath}\\{Name}";
	public partial TreeData? Parent => Node?.Depth <= 0 ? null : Node?.Parent?.LinkedData;
	public partial TreeViewItem? VisualContainer => ParentTree?.ContainerFromNode(Node) as TreeViewItem;
	public partial IEnumerable<TreeData>? Children => Node?.Children.Select(n => n.LinkedData);
	public partial bool IsLinked => Node is not null;
	public partial bool HasChildren => Node?.HasChildren ?? false;
	public partial int Depth => Node?.Depth ?? -1;
	public bool? IsLocalOnly
	{
		get => Node is null
			? null
			: IsLocal is true && IsRemote is false;
	}
	public bool? IsLocal
	{
		get
		{
			if (Node is null) return null;
			if (DirectoryId is null or 0) return true;
			if (HpDirectory.NodePathToWindowsPath(FullPath, true) is not string path) return false;

			DirectoryInfo folder = new(path);
			return folder.Exists;
		}
	}
	public bool? IsRemoteOnly
	{
		get => Node is null
			? null
			: IsRemote is true && IsLocal is false;
	}
	public bool? IsRemote
	{
		get =>
			Node is null
				? null
				: DirectoryId is not null and not 0;
		
	}
	public partial bool IsExpanded
	{
		get => Node?.IsExpanded ?? false;
		set => Node?.IsExpanded = value;
	}
	public TreeData(string? name)
	{
		Name = name;
		Tag = null;
	}
	public IEnumerator<TreeData> GetEnumerator() => Children?.GetEnumerator() ?? Enumerable.Empty<TreeData>().GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
	public override string ToString()
	{
		return Name ?? "";
	}

	public void SortTree()
	{
		var root = Node;
		if (root is null || root.Children.Count == 0) return;
		
		// sort children by TreeData.Name
		ObservableCollection<TreeViewNode> sortedChildren =
		[
			.. root.Children
				.OrderBy(n => (n.LinkedData?.Name ?? string.Empty), StringComparer.OrdinalIgnoreCase)
		];
		root.Children.Clear();
		foreach (var child in sortedChildren)
		{
			root.Children.Add(child);
			child.LinkedData.SortTree();
		}
	}
}
public partial class EntryRow : DataGridData
{
	// (MVVM) ViewModel
	public EntryReprType? ReprType
	{
		get;
		internal set;
	}
	public FileInfo? LocalFile
	{
		get
		{
			if (field is not null) return field;

			string? path = ReprType switch
			{
				EntryReprType.Both or EntryReprType.Remote => HpDirectory.ConvertToWindowsPath(FullName, true),
				EntryReprType.Local => FullName,
				_ => null,
			};
			
			FileInfo? file = string.IsNullOrEmpty(path) ? null : new(path);
			field = file?.Exists is true ? file : null;
			return field;
		}
	}
	public bool? IsLocal
	{
		get
		{
			field = Id switch
			{
				null => null,
				0 => true,
				_ => LocalFile?.Exists ?? false,
			};
			
			return field;
		}
	}
	public bool? IsRemote
	{
		get
		{
			field = Id switch
			{
				null => null,
				not 0 => true,
				_ => false,
			};
			return field;
		}
	}
	// need IsLocal to be hit so that LocalFile is evaluated
	public bool?	IsOnlyLocal => IsRemote is false & IsLocal is true;
	public bool?	IsOnlyRemote => IsRemote is true & IsLocal is false;
	public EntryRow Clone()
	{
		var cItem = new EntryRow
		{
			Name = this.Name?.Cloned(),
			Text = this.Text?.Cloned(),
			Icon = this.Icon,
			StatusIcon = this.StatusIcon,
			Id = this.Id,
			Type = this.Type?.Cloned(),
			Size = this.Size,
			Status = this.Status,
			Checkout = this.Checkout,
			Category = this.Category,
			LocalDate = this.LocalDate,
			RemoteDate = this.RemoteDate,
			FullName = this.FullName?.Cloned(),
			LatestId = this.LatestId,
			LatestReleaseId = this.LatestReleaseId,
		};
		return cItem;
	}
}
#endregion

public class BasicStatusMessage : IRowData<BasicStatusMessage>
{
	// // (MVVM) VIEW
	public StatusMessage Status { get; set; } = StatusMessage.OTHER;
	public string? Message { get; set; }

	public BasicStatusMessage Clone() => new()
	{
		Status = this.Status,
		Message = this.Message?.Cloned(),
	};
}
//public partial class ItemData : IRowData<ItemData>
//{
//	// (MVVM) VIEW
//	public virtual string? Name { get; set; } = "";
//	public virtual string? Text
//	{
//		get => field ??= Name;
//		set;
//	}
//}
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


