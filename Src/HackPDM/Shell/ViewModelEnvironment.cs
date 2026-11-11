using System;
using System.Collections.Generic;
using HackPDM.Abstractions.Structures;
using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.ViewModels.Hack;
using HackPDM.Mvvm.ViewModels.Settings;
using HackPDM.Mvvm.ViewModels.Odoo;

namespace HackPDM.Shell;

/// <summary>The app's view model instances (one of each), created through <see cref="AppComposition"/>.</summary>
public class ViewModelEnvironment
{
	public HackFileManagerViewModel HFM { get; set; } = AppComposition.CreateHackFileManager();
	public ProfileManagerViewModel PM { get; set; } = new();
	public SearchOdooViewModel SearchOdoo { get; set; } = AppComposition.CreateSearchOdoo();
	public Dictionary<Guid, WeakReference<Tuple<StatusDialogViewModel, StatusLogSession>>> Dialogs { get; } = [];
	public (StatusDialogViewModel StatusDialog, StatusLogSession Session) Logger { get; set; } = StatusDialogViewModel.CreateSessionPair();

	public OdooSettingsViewModel OdooSettings { get; set; } = new();
	public HackSettingsViewModel HackSettings { get; set; } = new();
	public ApplicationSettingsViewModel AppSettings { get; set; } = new();
	public HomeViewModel Home { get; set; } = new();
	public NotLoggedInViewModel NotLoggedIn { get; set; } = new();
}
