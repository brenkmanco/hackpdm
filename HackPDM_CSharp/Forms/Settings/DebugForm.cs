using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using HackPDM.Forms.Hack;
using HackPDM.Forms.Odoo;
using HackPDM.Verifier;

namespace HackPDM.Forms.Settings
{
	public partial class DebugForm : Form
	{
		public DebugForm()
		{
			while (!Verify.VerifySettings())
			{
				if (new ProfileManager().ShowDialog() != DialogResult.OK)
				{
					return;
				}
			}
			InitializeComponent();
			LoadFormSelectionComboBox();
			DebugChooseFormBtn.Focus();
		}
		private void LoadFormSelectionComboBox()
		{
			foreach(FormSelection form in Enum.GetValues(typeof(FormSelection)))
			{
				DebugChooseForm.Items.Add(form);
				//if (form == FormSelection.HackFileManager)
				if (form == FormSelection.HackFileManager)
					DebugChooseForm.SelectedItem = form;
			}
		}

		private void DebugChooseFormBtn_Click( object sender, EventArgs e )
		{
			Form form = ((FormSelection)DebugChooseForm.SelectedItem).ChooseForm();
			form.Show();
		}
		
	}
	public static class DebugFormChooser
	{
		public static Form ChooseForm (this FormSelection form) => form switch
		{
			FormSelection.HackFileManager			=> new HackFileManager(),
			FormSelection.OdooFileTypeManager		=> new OdooFileTypeManager(),
			FormSelection.HackSettings				=> new HackSettings(),
			FormSelection.OdooSettings				=> new OdooSettings(),
			FormSelection.StatusSettings			=> new StatusSettings(),
			FormSelection.OdooViewer				=> new OdooViewer(),
			FormSelection.SearchOdoo				=> new SearchOdoo(),
			FormSelection.StatusDialog				=> new StatusDialog(),
			FormSelection.LegacyFileTypeManager		=> new MainForm(ClientUtils.MainFormDirective.FileTypeManager),
			FormSelection.LegacyMainForm			=> new MainForm(),
			// ProfileManager is initiated by MainForm and then MainForm is pulled up
			//FormSelection.LegacyProfileManager		=> new MainForm(ClientUtils.MainFormDirective.ProfileManager),
			FormSelection.LegacyProfileManager		=> new ProfileManager(),
			_ => throw new ArgumentOutOfRangeException(nameof(form), $"Invalid Form Value: {form}"),
		};
	}
	public enum FormSelection
	{
		HackFileManager,
		OdooFileTypeManager,

		HackSettings,
		OdooSettings,
		StatusSettings,

		OdooViewer,
		SearchOdoo,
		StatusDialog,

		LegacyMainForm,
		LegacyProfileManager,
		LegacyFileTypeManager,
		LegacySearchDialog,
	}
}
