using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HackPDM.Domain.Representation;

namespace HackPDM.ViewModels;

public partial class SearchOdooViewModel : ObservableObject
{
	public ObservableCollection<SearchRow> SearchResultsList { get; } = [];
	public ObservableCollection<SearchPropertiesRow> PropertiesActive { get; } = [];
	public ObservableCollection<OperatorsRow> OperatorList { get; } = [];
	public ObservableCollection<SearchPropertiesRow> PropertiesSearchable { get; } = [];

	public string FileName { get; set; } = string.Empty;
	public bool CheckedOutMe { get; set; } = false;
	public bool LocalOnly { get; set; } = false;
	public bool DeletedRemotely { get; set; } = false;
	public string MaxResults { get; set; } = "100";
	public string PropertyValue { get; set; } = string.Empty;
}
