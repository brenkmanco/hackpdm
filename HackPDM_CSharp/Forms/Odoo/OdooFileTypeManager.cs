using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HackPDM.ClientUtils;
using System.Drawing.Imaging;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Text.RegularExpressions;

namespace HackPDM.Forms.Odoo
{
	public partial class OdooFileTypeManager : Form
	{
		private HackFileManager hackman;
		private readonly static Size closeImgSize = new Size(32, 32);
		private Image closeImg
		{
			get
			{
				if (field is null)
				{
					field = TypeImageList.Images["delete_image_button.png"];
					//field = Image.FromFile(Path.Combine(HackFileManager.ResourcesPath, "delete_image_button.png"));
					//field = ImageUtils.ResizeImage( field, closeImgSize.Width, closeImgSize.Height );
				}
				return field;
			}
		}
		private Image emptyImg
		{
			get
			{
				if ( field is null )
				{
					field = TypeImageList.Images["square_empty.png"];
					//field = Image.FromFile( Path.Combine( HackFileManager.ResourcesPath, "square_empty.png" ) );
					field = ImageUtils.ResizeImage( field, closeImgSize.Width, closeImgSize.Height );
				}
				return field;
			}
		}
		private readonly static Rectangle closeImgBounds = new Rectangle(0, 0, closeImgSize.Width, closeImgSize.Height);

		readonly Dictionary<string, int> TypeRows = new()
		{
			{NameConfig.FileTypeExtension.Name,	15},
			{NameConfig.FileTypeCategory.Name,	10},
			{NameConfig.FileTypeRegEx.Name,		18},
			{NameConfig.FileTypeDescription.Name, 0},
		};
		readonly Dictionary<string, int> LocalRows = new()
		{
			{NameConfig.FileTypeLocExt.Name,		15},
			{NameConfig.FileTypeLocStatus.Name,	21},
			{NameConfig.FileTypeLocExample.Name,	0},
		};
		readonly Dictionary<string, DataColumnSettings> EntryFilterRows = SetEntryFilterRows();
		readonly Dictionary<string, DataColumnSettings> LocalDataRows = SetLocalDataRows();
		private static Dictionary<string, DataColumnSettings> SetLocalDataRows()
		{
			string ftExt = NameConfig.FileTypeLocDatExt.Name;
			string ftCat = NameConfig.FileTypeLocDatCat.Name;
			string ftReg = NameConfig.FileTypeLocDatReg.Name;
			string ftDes = NameConfig.FileTypeLocDatDes.Name;
			string ftIco = NameConfig.FileTypeLocDatIco.Name;

			DataColumnSettings Ext = new ([("ColumnName", ftExt), ("DataType", typeof(string))]);
			DataColumnSettings Cat = new ([("ColumnName", ftCat), ("DataType", typeof(string))]);
			DataColumnSettings Reg = new ([("ColumnName", ftReg), ("DataType", typeof(string))]);
			DataColumnSettings Des = new ([("ColumnName", ftDes), ("DataType", typeof(string))]);
			DataColumnSettings Ico = new ([("ColumnName", ftIco), ("DataType", typeof(Image))]);

			DataGridViewColumn gv = new();
			DataColumn col = new();


			return new()
			{
				{ftExt, Ext},
				{ftCat, Cat},
				{ftReg, Reg},
				{ftDes, Des},
				{ftIco, Ico},
			};
		}
		public OdooFileTypeManager()
		{
			InitializeComponent();

			OdooOpenImage.Filter = GetImageFormatFilter();
			OdooOpenImage.InitialDirectory = HackDefaults.PWAPathAbsolute;
			OdooOpenImage.RestoreDirectory = true;

			OdooLocTypes.DrawItem += ListView_DrawItem;
			OdooLocTypes.DrawSubItem += ListView_DrawSubItem;
			OdooLocTypes.ItemSelectionChanged += this.OdooLocTypes_ItemSelectionChanged;

			foreach (var item in OdooDefaults.HpCategories)
			{
				cboCat.Items.Add(item);
			}
			//cboCat.Items
		}


		private void ListView_DrawItem( object sender, DrawListViewItemEventArgs e ) {}

		private void OdooLocTypes_ItemSelectionChanged( object sender, ListViewItemSelectionChangedEventArgs e )
		{
			if ( e.Item.SubItems [ NameConfig.FileTypeLocStatus.Name ].Text != "New Type" )
			{
				e.Item.Selected = false;
			}
		}

		public OdooFileTypeManager(HackFileManager manager) : this()
		{
			hackman = manager;
		}
		private void ListView_DrawSubItem( object sender, DrawListViewSubItemEventArgs e )
		{
			if (e.Item.SubItems[NameConfig.FileTypeLocStatus.Name].Text != "New Type" )
			{
				e.DrawDefault = false;
				e.Graphics.FillRectangle( Brushes.DarkGray, e.Bounds );
				TextRenderer.DrawText( e.Graphics, e.SubItem.Text, e.SubItem.Font, e.Bounds, Color.Red );
			}
			else
			{
				e.DrawDefault = true;
			}
		}
		private static Dictionary<string, DataColumnSettings> SetEntryFilterRows()
		{
			string ftID = NameConfig.FileTypeEntryFilterID.Name;
			string ftProto = NameConfig.FileTypeEntryFilterProto.Name;
			string ftRegEx = NameConfig.FileTypeEntryFilterRegEx.Name;
			string ftDescription = NameConfig.FileTypeEntryFilterDescription.Name;
			
			DataColumnSettings ID = new ([("ColumnName", ftID), ("DataType", typeof(int)), ("ReadOnly", true)]);
			DataColumnSettings Proto = new ([("ColumnName", ftProto), ("DataType", typeof(string))]);
			DataColumnSettings RegEx = new ([("ColumnName", ftRegEx), ("DataType", typeof(string))]);
			DataColumnSettings Description = new ([("ColumnName", ftDescription), ("DataType", typeof(string))]);

			return new()
			{
				{ftID, ID},
				{ftProto, Proto},
				{ftRegEx, RegEx},
				{ftDescription, Description},
			};
		}
		private string GetImageFormatFilter()
		{
			StringBuilder imageFilter = new();
			PropertyInfo[] propInfo = typeof(ImageFormat).GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);
			
			imageFilter.Append("Image Files|");
			for (int i = 0; i < propInfo.Length; i++)
			{
				imageFilter.Append($"*.{propInfo[i].Name.ToLower()}");
				if (i != propInfo.Length-1) imageFilter.Append(";");
			}
			//imageFilter.Append(")");
			return imageFilter.ToString();
		}
		private void btnRefreshRemote_Click( object sender, EventArgs e )
		{
			HackFileManager.InitListViewPercentage(OdooRemTypes, ColumnMap.FileTypeRows);
			foreach ( KeyValuePair<string, HpType> item in OdooDefaults.ExtToType)
			{
				HpType type = item.Value;
				ListViewItem listItem = HackFileManager.EmptyListItem(OdooRemTypes);
				
				listItem.SubItems[NameConfig.FileTypeExtension.Name].Text = type.file_ext;
				listItem.SubItems[NameConfig.FileTypeCategory.Name].Text = type.cat_id.ToString();
				listItem.SubItems[NameConfig.FileTypeRegEx.Name].Text = type.type_regex;
				listItem.SubItems[NameConfig.FileTypeDescription.Name].Text = type.description;
		
				listItem.ImageKey = AddOrGetImageKeyRemote(TypeImageList, type);

				OdooRemTypes.Items.Add(listItem);
			}
		}
		private string AddOrGetImageKeyRemote(ImageList list, HpType type)
		{
			// image key not present in ilListIcons
			string strKey = type.file_ext;

			if ( !list.Images.ContainsKey( strKey ) )
			{
				// get remote image
				byte[] imgBytes = FileOperations.ConvertFromBase64(type.icon);
				MemoryStream ms = new();
				ms.Write( imgBytes, 0, imgBytes.Length );
					
				Image img = Image.FromStream( ms );
				if (img != null) list.Images.Add(strKey, img);
				else strKey = "default";
			}

			return strKey;
		}
		private string AddOrGetImageKeyLocal(ImageList list, FileInfo type)
		{
			// image key not present in ilListIcons
			string strKey = type.Extension.Substring(1).ToLower();
			
			if (!list.Images.ContainsKey(strKey))
			{
				// get local image
				//string path = Path.Combine(HackFileManager.ExtensionIconPath, $"{strKey}.png");
				//if (File.Exists(path))
				//{
				//	list.Images.Add(strKey, Image.FromFile(path));
				//}
				//else
				//{
				strKey = "default";
				//}
								
			}

			return strKey;
		}
		private void btnRefreshFilters_Click( object sender, EventArgs e )
		{
			HackFileManager.InitGridView(OdooEntryFilters);
			DataTable table = HackFileManager.EmptyGridTable(OdooEntryFilters, EntryFilterRows);

			string id			= NameConfig.FileTypeEntryFilterID.Name;
			string proto		= NameConfig.FileTypeEntryFilterProto.Name;
			string regex		= NameConfig.FileTypeEntryFilterRegEx.Name;
			string description	= NameConfig.FileTypeEntryFilterDescription.Name;

			foreach (HpEntryNameFilter entryFilter in OdooDefaults.HpEntryNameFilters)
			{
				DataRow row = table.NewRow();
				
				row[ id ]			= entryFilter.ID;
				row[ proto ]		= entryFilter.name_proto;
				row[ regex ]		= entryFilter.name_regex;
				row[ description ]	= entryFilter.description;
		
				table.Rows.Add(row);
			}
			OdooEntryFilters.DataSource = table;
			OdooEntryFilters.Columns[ id ].Width						= 75;
			OdooEntryFilters.Columns[ id ].DefaultCellStyle.BackColor	= Color.LightSlateGray;
			OdooEntryFilters.Columns[ proto ].Width						= 100;
			OdooEntryFilters.Columns[ regex ].Width						= 100;
			OdooEntryFilters.Columns[ description ].Width				= 500;
		}
		private void btnRefreshLocal_Click( object sender, EventArgs e )
		{
			BackgroundWorker worker = new()
			{
				WorkerSupportsCancellation = true
			};
			//worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler((s, ev) => MessageBox.Show("Finished"));
			worker.DoWork += new DoWorkEventHandler(worker_RefreshLocal);
			worker.RunWorkerAsync();
		}
		private async void worker_RefreshLocal(object sender, DoWorkEventArgs e) => RefreshLocal();
		private async void RefreshLocal()
		{
			HackFileManager.InitListViewPercentage(OdooLocTypes, ColumnMap.FileTypeLocRows);
			IEnumerable<string> filePaths = Directory.EnumerateFiles(HackDefaults.PWAPathAbsolute, "*", SearchOption.AllDirectories);
			HashSet<string> uniqFiles = [];

			int pathsCount = filePaths.Count();
			HackFileManager.SafeInvoker(LocalFileCount, () => LocalFileCount.Text = $"files found: {pathsCount.ToString()}");

			foreach(var filepath in filePaths)
			{
				if ( filepath.GetFileEndType( out string extension ) && !uniqFiles.Contains( extension ) )
				{
					uniqFiles.Add( extension );
					var file = new FileInfo(filepath);
					ListViewItem item = HackFileManager.EmptyListItem(OdooLocTypes);
					string status = "New Type";

					item.SubItems [ NameConfig.FileTypeLocExt.Name ].Text = extension;
					if ( OdooDefaults.ExtToType.ContainsKey( $".{extension}" ) )
					{
						status = "Exists Remotely";
					}

					item.SubItems [ NameConfig.FileTypeLocStatus.Name ].Text = status;
					item.SubItems [ NameConfig.FileTypeLocExample.Name ].Text = file.FullName;

					item.ImageKey = AddOrGetImageKeyLocal( TypeImageList, file );

					HackFileManager.SafeInvoker( OdooLocTypes, () => OdooLocTypes.Items.Add( item ) );
				}
			}
		}

		private async void btnTypesCommit_Click( object sender, EventArgs e )
		{
			string ext = txtExt.Text;
			string reg = txtRegex.Text;
			int cat = ( cboCat.SelectedItem as HpCategory )?.ID ?? 0;
			string des = txtDesc.Text;
			Image img = pbIcon.Image;
			HpType userType = new(des, ext, img, reg, cat);
			await CommitTypes(userType);
		}
		private async Task CommitTypes(params HpType[] types)
		{
			StringBuilder errors = new();
			StringBuilder success = new();

			foreach(var t in types)
			{
				bool isError = false;
				errors.AppendLine("===========================================");
				if ( t.file_ext is "" or null )
				{
					errors.AppendLine( "file extension is invalid" );
					isError = true;
				}
				else
				{
					if ( t.file_ext [ 0 ] != '.' )
						t.file_ext = t.file_ext.Insert( 0, "." );
					if ( OdooDefaults.ExtToType.TryGetValue( t.file_ext, out HpType type ) )
					{
						errors.AppendLine( $"type exists remotely: " +
						$"(\n" +
						$"\tid: {type.ID},\n" +
						$"\textension: {type.file_ext},\n" +
						$"\tregex: {type.type_regex},\n" +
						$"\tcategory: {OdooDefaults.ExtToCat [ t.file_ext ].name},\n" +
						$"\tdescription: {type.description}\n)" );
						isError = true;
					}

				}

				if ( t.type_regex is null or "" )
				{
					errors.AppendLine( "regex is invalid" );
					isError = true;
				}
				if ( t.cat_id is 0 )
				{
					errors.AppendLine( "category id is invalid" );
					isError = true;
				}
				if ( t.description is null or "" )
				{
					errors.AppendLine( "description is invalid" );
					isError = true;
				}
				if ( t.image_save is null )
				{
					errors.AppendLine( "icon is invalid" );
					isError = true;
				}
				else
				{
					if ( t.image_save.Size == Size.Empty )
					{
						errors.AppendLine( "icon is empty" );
						isError = true;
					}
					if ( t.image_save.Width != t.image_save.Height )
					{
						errors.AppendLine( "icon is not 1:1" );
						isError = true;
					}
				}
				if ( isError )
				{
					continue;
				}
				t.image_save.ImageFormater( t.file_ext.Substring( 1 ), ImageFormat.Png );
				t.image_save = ImageUtils.ResizeImage( t.image_save, 32, 32 );

				int id = await t.CreateAsync();
				if ( id == 0 )
				{
					errors.AppendLine( $"Odoo was unable to create type record {t.file_ext}" );
				}
				else success.AppendLine($"Odoo type record created: ID = {id}");
			}
			if (errors.Length > 0)
			{
				MessageBox.Show( errors.ToString(),
					"Data Error",
					MessageBoxButtons.OK,
					MessageBoxIcon.Error );
			}
			if (success.Length > 0)
			{
				MessageBox.Show( success.ToString() );
			}
		}

		private void pbIcon_Click( object sender, EventArgs e )
		{
			if (OdooOpenImage.ShowDialog() != DialogResult.OK) return;
			pbIcon.Image = Image.FromStream(OdooOpenImage.OpenFile());
			pbIcon.BackgroundImage = null;
		}


		private async Task AddLocalTypes(params HpType[] types)
		{
			HackFileManager.InitGridView( LocalDataTypeGrid );
			DataTable table = HackFileManager.EmptyGridTable( LocalDataTypeGrid, LocalDataRows );

			string extension    = NameConfig.FileTypeLocDatExt.Name;
			string regex        = NameConfig.FileTypeLocDatReg.Name;
			string category     = NameConfig.FileTypeLocDatCat.Name;
			string description  = NameConfig.FileTypeLocDatDes.Name;
			string icon         = NameConfig.FileTypeLocDatIco.Name;

			foreach ( var type in types )
			{
				DataRow row = table.NewRow();

				row [ extension ] = type.file_ext;
				row [ regex ] = type.type_regex;

				table.Rows.Add( row );
			}
			LocalDataTypeGrid.DataSource = table;
			LocalDataTypeGrid.AutoResizeRows( DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders );
			string iconCancel   = NameConfig.FileTypeLocDatIcoCancel.Name;

			DataGridViewColumn tempColumn;
			// extension column settings
			tempColumn                              = LocalDataTypeGrid.Columns [ extension ];
			//tempColumn.CellTemplate					= new DataGridViewTextBoxCell();
			tempColumn.Width                        = 75;
			// regex column settings
			tempColumn                              = LocalDataTypeGrid.Columns [ regex ];
			//tempColumn.CellTemplate                 = new DataGridViewTextBoxCell();
			tempColumn.Width                        = 100;
			// category column settings
			tempColumn                              = LocalDataTypeGrid.Columns [ category ];
			//tempColumn.CellTemplate                 = new DataGridViewTextBoxCell();
			tempColumn.Width                        = 100;
			// description column settings
			tempColumn                              = LocalDataTypeGrid.Columns [ description ];
			//tempColumn.CellTemplate                 = new DataGridViewTextBoxCell();
			tempColumn.Width                        = 300;
			// icon column settings
			tempColumn                              = LocalDataTypeGrid.Columns [ icon ];
			//tempColumn.CellTemplate                 =  new DataGridViewImageCell();
			tempColumn.Width                        = 100;
			// icon cancel column settings
			var buttonColumn                        = new DataGridViewImageColumn();
			buttonColumn.Name                       = iconCancel;
			buttonColumn.HeaderText                 = iconCancel;
			buttonColumn.DefaultCellStyle.NullValue = emptyImg;
			tempColumn                              = LocalDataTypeGrid.Columns [ LocalDataTypeGrid.Columns.Add( buttonColumn ) ];
			tempColumn.Width                        = closeImgSize.Width;
		}
		// TODO: functional buttons
		// reset, delete, add selected, commit filters
		private async void btnAddSel_Click( object sender, EventArgs e )
		{
			List<HpType> types = [];
			foreach (ListViewItem item in OdooLocTypes.SelectedItems)
			{
				if (item.SubItems[NameConfig.FileTypeLocStatus.Name].Text != "New Type") continue;

				string ext = item.SubItems[NameConfig.FileTypeLocExt.Name].Text;
				HpType type = new(null, ext, null, $"\\.({ext})$", 0);
				types.Add(type);
			}		
			await AddLocalTypes([.. types]);
		}
		private async void AddAllNewTypesBtn_Click( object sender, EventArgs e )
		{
			List<HpType> types = [];
			foreach ( ListViewItem item in OdooLocTypes.Items )
			{
				if ( item.SubItems [ NameConfig.FileTypeLocStatus.Name ].Text != "New Type" )
					continue;

				string ext = item.SubItems[NameConfig.FileTypeLocExt.Name].Text;
				HpType type = new(null, ext, null, $"\\.({ext})$", 0);
				types.Add( type );
			}
			await AddLocalTypes([.. types]);
		}
		private void LocalDataTypeGrid_CellClick( object sender, DataGridViewCellEventArgs e )
		{
			DataGridViewColumn column = LocalDataTypeGrid.Columns[e.ColumnIndex];
			string iconStr = NameConfig.FileTypeLocDatIco.Name;
			string cancelStr = NameConfig.FileTypeLocDatIcoCancel.Name;

			if (column.Name == iconStr)
			{
				
				if (OdooOpenImage.ShowDialog() == DialogResult.OK)
				{
					DataGridViewRow row = LocalDataTypeGrid.Rows[e.RowIndex];
					DataGridViewCell cell = row.Cells[e.ColumnIndex];
					
					Image img = Image.FromStream(OdooOpenImage.OpenFile());

					if (img is null) 
					{
						MessageBox.Show("Invalid Image");
						return;
					}
					if (img.Size == Size.Empty)
					{
						MessageBox.Show("Empty Image");
						return;
					}
					if (img.Width != img.Height)
					{
						MessageBox.Show("Image ratio isn't 1:1");
						return;
					}
					img.ImageFormater( NameConfig.FileTypeLocDatExt.Name, ImageFormat.Png );
					img = ImageUtils.ResizeImage( img, 32, 32 );
					cell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
					cell.Value = img;

					DataGridViewCell buttonCell = row.Cells[cancelStr];
					buttonCell.Value = closeImg;
					//LocalDataTypeGrid.InvalidateCell(LocalDataTypeGrid.Columns[buttonColumnName].Index, e.RowIndex);
				}
			}
			if (column.Name == cancelStr)
			{
				DataGridViewRow row = LocalDataTypeGrid.Rows[e.RowIndex];
				DataGridViewCell icon = row.Cells[iconStr];
				DataGridViewCell iconCancel = row.Cells[cancelStr];
				icon.Value = null;
				iconCancel.Value = null;
			}
		}

		private async void button3_Click( object sender, EventArgs e )
		{
			List<HpType> types = [];
			string extension    = NameConfig.FileTypeLocDatExt.Name;
			string regex        = NameConfig.FileTypeLocDatReg.Name;
			string category     = NameConfig.FileTypeLocDatCat.Name;
			string description  = NameConfig.FileTypeLocDatDes.Name;
			string icon         = NameConfig.FileTypeLocDatIco.Name;

			foreach ( DataGridViewRow row in LocalDataTypeGrid.Rows )
			{
				string extensionVal, regexVal, descriptionVal;
				int categoryVal = 0;
				Image iconVal;

				extensionVal = row.Cells[extension].Value as string;
				if (extensionVal is null or "") continue;

				regexVal = row.Cells[regex].Value as string;
				if (regexVal is null or "") continue;

				string temp = row.Cells[category].Value as string;
				if (temp is null or "" || !int.TryParse(temp, out categoryVal)) continue;

				descriptionVal = row.Cells[description].Value as string;
				if (descriptionVal is null or "") continue;

				iconVal = row.Cells[regex].Value as Image;
				if (iconVal is null) continue;

				types.Add(
					new(
						file_ext: extensionVal, 
						type_regex: regexVal,
						cat_id: categoryVal,
						description: descriptionVal,
						icon: iconVal));
			}
			await CommitTypes([.. types]);
		}

		private async void btnFiltersCommit_Click( object sender, EventArgs e )
		{
			List<HpEntryNameFilter> commitFilters = [];
			List<HpEntryNameFilter> updateFilters = [];
			string id           = NameConfig.FileTypeEntryFilterID.Name;
			string proto        = NameConfig.FileTypeEntryFilterProto.Name;
			string regex        = NameConfig.FileTypeEntryFilterRegEx.Name;
			string description  = NameConfig.FileTypeEntryFilterDescription.Name;


			foreach (DataGridViewRow row in OdooEntryFilters.Rows)
			{
				bool isCommit = false;
				int idVal = 0;
				//isCommit = !(row.Cells[id].Value is int idTemp);

				string protoVal = row.Cells[proto]?.Value as string;
				if ( protoVal is null or "" ) continue;

				string regexVal = row.Cells[regex]?.Value as string;
				if ( regexVal is null or "" ) continue;

				string descriptionVal = row.Cells[description]?.Value as string;
				if ( descriptionVal is null or "") continue;
				
				HpEntryNameFilter filter;
				
				if (isCommit)
				{
					filter = new(
					name_regex: regexVal,
					name_proto: protoVal,
					description: descriptionVal );

					commitFilters.Add(filter);
				}
				else
				{
					filter = OdooDefaults.HpEntryNameFilters.First(filter => filter.ID == idVal);
					filter.name_proto = protoVal;
					filter.name_regex = regexVal;
					filter.description = descriptionVal;
					updateFilters.Add(filter);
				}
			}
			await CommitFilters([.. commitFilters]);
			await UpdateFilters([.. updateFilters]);
		}

		private async Task UpdateFilters(params HpEntryNameFilter[] filters )
		{
			StringBuilder errors = new();
			foreach ( var filter in filters )
			{
				if (!await filter.WriteChangedValuesAsync())
				{
					errors.AppendLine( $"{HpEntryNameFilter.GetHpModel()} was unable to update record \n" +
					$"\tID: {filter.ID}, \n" +
					$"\tproto: {filter.name_proto}, \n" +
					$"\tregex: {filter.name_regex}, \n" +
					$"\tdescription: {filter.description}" );
				}
			}
			if (errors.Length > 0) MessageBox.Show(errors.ToString());
		}

		private async Task CommitFilters(params HpEntryNameFilter[] filters)
		{
			StringBuilder errors = new();
			foreach (var filter in filters)
			{
				int id = await filter.CreateAsync();
				if (id == 0) 
				{
					errors.AppendLine($"{HpEntryNameFilter.GetHpModel()} was unable to create record \n" +
					$"\tproto: {filter.name_proto}, \n" +
					$"\tregex: {filter.name_regex}, \n" +
					$"\tdescription: {filter.description}");
				}
			}
			if (errors.Length > 0) MessageBox.Show(errors.ToString());
		}

		private void OdooEntryFilters_CellValidated( object sender, DataGridViewCellEventArgs e )
		{
			// \.(msi)$
			DataGridViewColumn entryGridColumn = OdooEntryFilters.Columns[e.ColumnIndex];
			string protoStr = NameConfig.FileTypeEntryFilterProto.Name;
			if (entryGridColumn.Name == protoStr)
			{
				DataGridViewCell cell = OdooEntryFilters[e.ColumnIndex, e.RowIndex];
				string cv = cell.Value as string;
				if (cv is not null and not "")
				{
					StringBuilder sb = new(); 
					for (int i = 0; i < cv.Length; i++)
					{
						if (i == 0)
						{
							if (cv[i] == '.')
							{
								sb.Append(@"\.(");
							}
							else
							{
								sb.Append($"({cv[i]}");
							}
						}
						else if (i >= cv.Length - 1)
						{
							sb.Append($"{cv[i]})$");
						}
						else
						{
							sb.Append(cv[i]);
						}
					}
					string regexStr = NameConfig.FileTypeEntryFilterRegEx.Name;
					OdooEntryFilters[regexStr, e.RowIndex].Value = sb.ToString();
				}
			}
		}
	}
	public struct DataColumnSettings
	{
		public DataColumn Column 
		{
			get
			{
				if (field == null) field = NewInstance();
				return field;
			}
			private set;
		}
		private Dictionary<string, object> fieldPropValues = null;
		public DataColumnSettings(string columnName, Type dataType=null)
		{
			if (dataType == null) dataType = typeof(string);

			this.fieldPropValues = new() 
			{
				{"ColumnName", columnName},
				{"DataType", dataType},
			};
		}
		public DataColumnSettings(params (string, object)[] fieldPropertyValues)
		{
			this.fieldPropValues = [];
			foreach(var tup in fieldPropertyValues)
				this.fieldPropValues.Add(tup.Item1, tup.Item2);
		}
		public DataColumnSettings(Dictionary<string, object> fieldPropertyValues)
		{
			this.fieldPropValues = fieldPropertyValues;
		}
		
		public DataColumn NewInstance()
		{
			Column = new DataColumn();
			Type type = Column.GetType();

			foreach (var entry in fieldPropValues)
            {
                PropertyInfo prop = type.GetProperty(entry.Key.ToString(), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (prop != null && prop.CanWrite)
                {
                    object value = entry.Value;
                    prop.SetValue(Column, value);
                }
                
                FieldInfo field = type.GetField(entry.Key.ToString(), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    object value = entry.Value;
                    field.SetValue(Column, value);
                }
              
            }
			return Column;
		}
	}
}
