using System;
using System.Collections.Generic;
using HackPDM.Abstractions.Structures;
using HackPDM.UI.Forms.FormTransport;

namespace HackPDM.ViewModels;

public class ViewModelEnvironment
{
	public HackFileManagerViewModel HFM { get; set; } = new();
	public ProfileManagerViewModel PM { get; set; } = new();
	public SearchOdooViewModel SearchOdoo { get; set; } = new();
	public Dictionary<Guid, WeakReference<Tuple<StatusDialogViewModel, StatusLogSession>>> Dialogs { get; } = [];
	public (StatusDialogViewModel StatusDialog, StatusLogSession Session) Logger { get; set; } = StatusDialogViewModel.CreateSessionPair();

	public OdooSettingsViewModel OdooSettings { get; set; } = new();
	public HackSettingsViewModel HackSettings { get; set; } = new();
	public ApplicationSettingsViewModel AppSettings { get; set; } = new();
	public HomeViewModel Home { get; set; } = new();
	public NotLoggedInViewModel NotLoggedIn { get; set; } = new();
}
