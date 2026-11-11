using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using HackPDM.Core;
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
	public async static Task<bool> StageRelationshipRecords(params HpRecordStaged[] versionStaged)
	{
		//ArrayList ids = versions.Select(v => v.ID).ToArrayList();
		//ArrayList versionFields = OClient.Read(HpVersion.GetHpModel(), ids, ["id", "file_ext"]);
		ArrayList stagedRecords = [];
		SwReferenceResolver? resolver = null;
        try
        {

		    foreach (HpRecordStaged vStaged in versionStaged)
		    {
			    if (vStaged is null || !SwDocumentTypes.IsSolidWorksExtension(vStaged.payload?["file_ext"]?.ToString())) continue;

                // TODO:
                // windows_complete_name is null
                // find version property where this is set
				string? pathway = ( vStaged.payload?["WinPathway"]?.ToString() ?? vStaged.HashedValues?["windows_complete_name"] as string ) ?? throw new ArgumentException();
				// "root\dir\file" whatever the OS
				string? filePathway = PwaPaths.ToAbsolute( pathway );
				resolver ??= new SwReferenceResolver(HackDefaults.Instance?.PwaPathAbsolute);
				// the server links a dependency by its file name, so keep every reference: one saved
				// from another folder or machine (not under the vault) used to be dropped here
				List<string> paths = [.. SwDependencies.Of(filePathway!, resolver).Select(r => r.Local ?? r.Stored)];
			    if (paths.Count > 0)
			    {

				    //HpVersion[] getVersions = HpVersion.GetFromPaths(includedFields: ["name", "entry_id"], fullPaths: [.. paths]);
                    // every version's dependencies are staged, not just the last version's
                    stagedRecords.AddRange(paths.Select(p => new HpRecordStaged()
                    {
                        commit_id = vStaged.commit_id,
                        committing_id = (Many2One?)vStaged.commit_id,
                        target_model = GetHpModel(),
                        payload = new Hashtable
                        {
                            { "path", p },
                            // lets the server link this exact file, not just any file of that name
                            { "windows_complete_name", PwaPaths.ToServerName(p) },
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
                    }).ToArray());
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