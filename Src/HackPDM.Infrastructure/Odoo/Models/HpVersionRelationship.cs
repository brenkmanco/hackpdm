using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Infrastructure.SldWrks;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

// Resharper disable InconsistentNaming


//using static System.Net.Mime.MediaTypeNames;



namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_VERSION_RELATIONSHIP_NAME, OdooDefaultsConstants.HP_VERSION_RELATIONSHIP)]
public partial class HpVersionRelationship : HpBaseModelTransport<HpVersionRelationship>, IHpVersionRelationshipModel
{
    [OdooProp(OdooFieldType.Many2One, "parent_id")] public Many2One? parent_id { get; set; }
	[OdooProp(OdooFieldType.Many2One, "child_id")] public Many2One? child_id { get; set; }
	IMany2One? IHpVersionRelationshipModel.parent_id { get => (IMany2One?)parent_id; set => parent_id = (Many2One?)value; }
	IMany2One? IHpVersionRelationshipModel.child_id { get => (IMany2One?)child_id; set => child_id = (Many2One?)value; }

	public HpVersionRelationship() { } 
    public HpVersionRelationship(
        int parentId = 0,
        int childId = 0)
    {
        this.parent_id = parentId;
        this.child_id = childId;
    }
}
public partial class HpVersionRelationship : HpBaseModelTransport<HpVersionRelationship>
{
    public async static void Create(params HpVersion[] versions)
    {
        //ArrayList ids = versions.Select(v => v.ID).ToArrayList();
        //ArrayList versionFields = OClient.Read(HpVersion.GetHpModel(), ids, ["id", "file_ext"]);
        HpVersionRelationship[] hvrCreate = [];
        foreach (HpVersion version in versions)
        {
            if (version is not null && !OdooDefaultsConstants.DependentExt.Contains($".{version.file_ext.ToUpper()}")) continue;
            string pathway = version.WinPathway;
            List<string> paths = [];
            List<string[]> dependencies = SolidWorksUtil.DocMgr?.GetDependencies(pathway); // NoInterrupt: true
            if (dependencies is not null && dependencies.Count > 0)
            {
                foreach (string[] deps in dependencies)
                {
                    string path = deps[1];
                    string absolute = "";
                    var splitPath = path.Split([$"\\{HackDefaults.Instance.PwaPathRelative}\\"], StringSplitOptions.RemoveEmptyEntries);
                    if (splitPath.Length == 2)
                        absolute = Path.Combine([HackDefaults.Instance.PwaPathAbsolute, splitPath[1]]);
                    else continue;
                    paths.Add(absolute);
                }
                HpVersion[] getVersions = HpVersion.GetFromPaths(includedFields: ["name", "entry_id"], fullPaths: [.. paths]);
                hvrCreate = [.. hvrCreate, .. 
                    getVersions.Select(v => new HpVersionRelationship()
                    {
                        parent_id = version.id ?? 0,
                        child_id = v.id ?? 0,
                    })
                ];
            }
        }
            
        if (hvrCreate.Length > 0)
        {
            await MultiCreateAsync(hvrCreate.ToArrayList());
        }
    }
	public async static Task<bool> StageRelationshipRecords(params HpRecordStaged[] versionStaged)
	{
		//ArrayList ids = versions.Select(v => v.ID).ToArrayList();
		//ArrayList versionFields = OClient.Read(HpVersion.GetHpModel(), ids, ["id", "file_ext"]);
		ArrayList stagedRecords = [];
        try
        {

		    foreach (HpRecordStaged vStaged in versionStaged)
		    {
			    if (vStaged is not null && !OdooDefaultsConstants.DependentExt.Contains($".{vStaged.payload?["file_ext"]?.ToString().ToUpper()}")) continue;

                // TODO:
                // windows_complete_name is null
                // find version property where this is set
				string? pathway = ( vStaged.payload?["WinPathway"]?.ToString() ?? vStaged.HashedValues?["windows_complete_name"] as string ) ?? throw new ArgumentException();
                string? filePathway = Path.Combine(HackDefaults.Instance?.PwaPathAbsolute ?? string.Empty, pathway ?? string.Empty);
				List<string> paths = [];
			    List<string[]> dependencies = SolidWorksUtil.DocMgr?.GetDependencies(filePathway); // NoInterrupt: true
			    if (dependencies is not null && dependencies.Count > 0)
			    {
				    foreach (string[] deps in dependencies)
				    {
					    string path = deps[1];
					    string absolute = "";
					    var splitPath = path.Split([$"\\{HackDefaults.Instance.PwaPathRelative}\\"], StringSplitOptions.RemoveEmptyEntries);
					    if (splitPath.Length == 2)
						    absolute = Path.Combine([HackDefaults.Instance.PwaPathAbsolute, splitPath[1]]);
					    else continue;
					    paths.Add(absolute);
				    }

				    //HpVersion[] getVersions = HpVersion.GetFromPaths(includedFields: ["name", "entry_id"], fullPaths: [.. paths]);
                    stagedRecords = [.. paths.Select(p => new HpRecordStaged()
                    {
                        commit_id = vStaged.commit_id,
                        committing_id = (Many2One?)vStaged.commit_id,
                        target_model = GetHpModel(),
                        payload = new Hashtable
                        {
                            { "path", p },
                            {
                                "version",
                                new Hashtable
                                {
                                    { "name", vStaged?.payload?["name"] },
                                    { "entry_id", vStaged?.payload?["entry_id"] },
                                    { "version_staged_id", vStaged?.id },
                                }
                            }
                        },
                    })];
			    }
		    }

		    if (stagedRecords.Count > 0)
		    {
			    await HpRecordStaged.MultiCreateAsync(stagedRecords);
		    }
        }
        catch
        {
			Debug.WriteLine($"unable to create relationships");
			return false;
        }
        return true;
	}
}