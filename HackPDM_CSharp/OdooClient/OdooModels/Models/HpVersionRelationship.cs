using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

using HackPDM.Extensions.General;


//using static System.Net.Mime.MediaTypeNames;



namespace HackPDM
{
    public class HpVersionRelationship : HpBaseModel<HpVersionRelationship>
    {
        public int parent_id;
        public int child_id;

        public HpVersionRelationship() { } 
        public HpVersionRelationship(
            int parent_id = 0,
            int child_id = 0)
        {
            this.parent_id = parent_id;
            this.child_id = child_id;
        }
        public async static void Create(params HpVersion[] versions)
        {
            //ArrayList ids = versions.Select(v => v.ID).ToArrayList();
            //ArrayList versionFields = OClient.Read(HpVersion.GetHpModel(), ids, ["id", "file_ext"]);
            HpVersionRelationship[] hvrCreate = [];
            foreach (HpVersion version in versions)
            {
                if (version is not null && !OdooDefaults.dependentExt.Contains($".{version.file_ext.ToUpper()}")) continue;
                string pathway = version.winPathway;
                List<string> paths = [];
                List<string[]> dependencies = HackDefaults.docMgr.GetDependencies(pathway); // NoInterrupt: true
                if (dependencies is not null && dependencies.Count > 0)
                {
                    foreach (string[] deps in dependencies)
                    {
                        string path = deps[1];
                        string absolute = "";
                        var splitPath = path.Split([$"\\{HackDefaults.PWAPathRelative}\\"], StringSplitOptions.RemoveEmptyEntries);
                        if (splitPath.Length == 2)
                            absolute = Path.Combine([HackDefaults.PWAPathAbsolute, splitPath[1]]);
                        else continue;
                        paths.Add(absolute);
                    }
                    HpVersion[] getVersions = HpVersion.GetFromPaths(includedFields: ["name", "entry_id"], fullPaths: [.. paths]);
                    hvrCreate = [.. hvrCreate, .. 
                        getVersions.Select(v => new HpVersionRelationship()
                        {
                            parent_id = version.ID,
                            child_id = v.ID,
                        })
                    ];
                }
            }
            
            if (hvrCreate.Length > 0)
            {
                await MultiCreateAsync<HpVersionRelationship>(hvrCreate.ToArrayList());
            }
        }
    }
}
