using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.CodeAnalysis;

namespace HackPDM.SchemaGenerator.GeneratedTypes;

public sealed record OdooFieldGen(OdooFieldType odooType);
public sealed record OdooPropertyGen(OdooFieldType odooType, string odooFieldName, IPropertySymbol property);
public sealed record OdooModelGen(string OdooName, string OdooDBName);