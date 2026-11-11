// Compatibility aliases for decoupled ViewModels
// View models live under HackPDM.Mvvm.ViewModels; these short names are used by the shell.
global using VMEnv = HackPDM.Shell.ViewModelEnvironment;
global using HFM_VM = HackPDM.Mvvm.ViewModels.Hack.HackFileManagerViewModel;
global using PM_VM = HackPDM.Mvvm.ViewModels.Settings.ProfileManagerViewModel;
global using SearchOdoo_VM = HackPDM.Mvvm.ViewModels.Odoo.SearchOdooViewModel;
global using StatusDialog_VM = HackPDM.Mvvm.ViewModels.Settings.StatusDialogViewModel;
global using HackSettings_VM = HackPDM.Mvvm.ViewModels.Hack.HackSettingsViewModel;
global using OdooSettings_VM = HackPDM.Mvvm.ViewModels.Odoo.OdooSettingsViewModel;
global using AppSettings_VM = HackPDM.Mvvm.ViewModels.Settings.ApplicationSettingsViewModel;
global using Home_VM = HackPDM.Mvvm.ViewModels.Hack.HomeViewModel;
global using NotLoggedIn_VM = HackPDM.Mvvm.ViewModels.Settings.NotLoggedInViewModel;

namespace HackPDM.Shell;
