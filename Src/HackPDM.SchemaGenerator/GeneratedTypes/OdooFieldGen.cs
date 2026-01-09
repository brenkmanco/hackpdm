using System;
using System.Collections.Generic;
using System.Text;

using HackPDM.Shared.GlobalData;

namespace HackPDM.SchemaGenerator.GeneratedTypes;

public sealed record OdooFieldGen(OdooFieldType odooType);
public sealed record OdooModelGen(string OdooName, string OdooDBName);