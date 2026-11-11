using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using HackPDM.Configuration;
using HackPDM.Core;
using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Mvvm.ViewModels.Settings;

public partial class ProfileManagerViewModel : ObservableObject
{
	public ObservableCollection<BasicStatusMessage> OStatus { get; } = [];
	public bool IsLoggedIn { get; set; } = false;
	public bool IsLoggingIn { get; set; } = false;

	public async Task<bool> AbleToLoginAsync()
	{
		try
		{
			List<string> errors = [];

			await OdooDefaults.Instance!.InitializeAsync();
			if (!await OdooConnection.CorrectOdooAddress())
			{
				errors.Add("invalid odoo address or unreachable host");
			}
			else if (!await OdooConnection.CorrectOdooPort())
			{
				errors.Add("invalid odoo port or server is down");
			}
			else if (OdooConnection.CorrectUserId() is int status)
			{
				switch (status)
				{
					case 1:
						return true;
					case 0:
						errors.Add("invalid odoo credentials");
						break;
					default:
						errors.Add("odoo server isn't running");
						break;
				}
			}
			else if (!await HackUpdater.EnsureUpdated(true))
			{
				errors.Add("running outdated client version");
			}

			if (errors.Count > 0)
			{
				foreach (string message in errors)
				{
					OStatus.Add(new BasicStatusMessage
					{
						Status = StatusMessage.ERROR,
						Message = message
					});
				}
				return false;
			}
			return true;
		}
		catch
		{
			return false;
		}
	}
}
