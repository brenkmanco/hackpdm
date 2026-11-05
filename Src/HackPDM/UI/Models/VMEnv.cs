// Compatibility aliases for decoupled ViewModels
// ViewModels have been extracted into individual classes under HackPDM.ViewModels for SRP and MVVM architecture.
global using VMEnv = HackPDM.ViewModels.ViewModelEnvironment;
global using HFM_VM = HackPDM.ViewModels.HackFileManagerViewModel;
global using PM_VM = HackPDM.ViewModels.ProfileManagerViewModel;
global using SearchOdoo_VM = HackPDM.ViewModels.SearchOdooViewModel;
global using StatusDialog_VM = HackPDM.ViewModels.StatusDialogViewModel;
global using HackSettings_VM = HackPDM.ViewModels.HackSettingsViewModel;
global using OdooSettings_VM = HackPDM.ViewModels.OdooSettingsViewModel;
global using AppSettings_VM = HackPDM.ViewModels.ApplicationSettingsViewModel;
global using Home_VM = HackPDM.ViewModels.HomeViewModel;
global using NotLoggedIn_VM = HackPDM.ViewModels.NotLoggedInViewModel;

namespace HackPDM.UI.Models;
