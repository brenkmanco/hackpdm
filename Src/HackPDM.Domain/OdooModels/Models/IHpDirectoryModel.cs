using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

// Resharper disable InconsistentNaming

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_DIRECTORY_NAME, OdooDefaultsConstants.HP_DIRECTORY)]
public interface IHpDirectoryModel : IHpOdooRecord
{
	[OdooProp(OdooFieldType.Char, "name")] public string? name {get;set;}
	[OdooProp(OdooFieldType.Char, "parent_path")] public string? parent_path {get;set;}
	[OdooProp(OdooFieldType.Many2one, "parent_id")] public IMany2One? parent_id {get;set;}
	[OdooProp(OdooFieldType.Many2one, "default_cat")] public IMany2One? default_cat {get;set;}
	[OdooProp(OdooFieldType.Boolean, "deleted")] public bool? deleted {get;set;}
	[OdooProp(OdooFieldType.Boolean, "sandboxed")] public bool? sandboxed {get;set;}
}
public class ExplorerItem
{
    public string Name { get; set; }
    public string IconPath { get; set; } 
    public bool IsFolder { get; set; }
    public ObservableCollection<ExplorerItem> Children { get; set; }
}