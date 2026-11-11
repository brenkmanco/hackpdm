using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using WinUI.TableView;

using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.UI.Types;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;

using Color = Windows.UI.Color;
using EntryRow = HackPDM.Mvvm.Models.Hack.EntryRow;

namespace HackPDM.Mvvm.Views.Common.Settings;

public class GroupByField
{
	public string FieldName						{ get; set; } = string.Empty;
	public SortDirection SortDirection	{ get; set; } = SortDirection.Ascending;
}
