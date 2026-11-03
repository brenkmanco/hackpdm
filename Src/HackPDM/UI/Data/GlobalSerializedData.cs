using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using CommunityToolkit.WinUI.UI.Controls;

using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Controls;
using HackPDM.UI.Forms.Hack;
using HackPDM.UI.Forms.Helper;
using HackPDM.UI.Forms.Odoo;
using HackPDM.UI.Forms.Settings;
using HackPDM.UI.Types;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;

using Color = Windows.UI.Color;
using EntryRow = HackPDM.UI.Types.EntryRow;

namespace HackPDM.UI.Data;

public class GlobalSerializedData
{
	private const string DefaultFileName = "AppConfig.json";
	[JsonIgnore]
	private static readonly string DefaultFilePath = TryHelper
			.TryGet(() => Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.Personal),
					DefaultFileName), out string? path)
						? path ?? ""
						: "";
	[JsonIgnore]
	public static JsonSerializerOptions Options = new()
	{
		Converters =
		{
			new CustomSerializers.ColorSerializer(),
			new CustomSerializers.Vector4Serializer(),
		}
	};

	[JsonIgnore]
	public static string? FilePath { get; set; } = GetFilePathDirectory();

	[JsonIgnore]
	public bool IsLoadedFromFile { get; set; } = false;

	[ JsonPropertyName( "WindowSettings" )]
	public WindowSettings Win	{ get; set; } = new();
	[JsonPropertyName( "OdooEntryColors" )]
	public ColorSettings EColors	{ get; set; } = new();
	[JsonPropertyName( "ColumnSettings" )]
	public OdooEntryColumns ColumnsSettings { get; set; } = new();

	public static string? GetFilePathDirectory()
	{
		FileInfo file;
		try
		{
			if (FilePath is null)
			{
				;
				if (!TryHelper.Try(() => SetFilePath(FilePath = DefaultFilePath)))
				{
					HackApp.CoreSettings.Set("AppConfigFilePath", FilePath);
				}
				return FilePath;
			}

			file = new(string.IsNullOrEmpty(FilePath)
				? HackApp.CoreSettings.Get("AppConfigFilePath", "") ?? DefaultFilePath
				: FilePath);
		}
		finally
		{
			FilePath = DefaultFilePath;
			file = new(FilePath);
		}

		if (file is { Exists: false } or { Extension: not ".json" })
			SetFilePath(file);

		return FilePath;
	}
	public static void SetFilePath(FileInfo fileInfo)
	{
		if (fileInfo is { Exists: false } or { Extension: not ".json" })
			FilePath = DefaultFilePath;

		HackApp.CoreSettings.Set("AppConfigFilePath", FilePath);
	}
	public static void SetFilePath(string? filePath)
		=> SetFilePath(new FileInfo(string.IsNullOrEmpty(filePath) ? DefaultFilePath : filePath));

	public static GlobalSerializedData LoadFromFile( string filePath )
	{
		if( !File.Exists( filePath ) )
		{
			return new GlobalSerializedData();
		}
		string json = File.ReadAllText( filePath );
		return LoadFromJson( json );
	}
	public static GlobalSerializedData LoadFromJson( string json )
	{
		var result = JsonSerializer.Deserialize<GlobalSerializedData>( json, Options );
		if( result == null )
		{
			result = new GlobalSerializedData();
		}
		else
		{
			result.IsLoadedFromFile = true;
		}
		return result;
	}
	public void SaveToFile( )
	{
		string json = JsonSerializer.Serialize( this, Options );
		File.WriteAllText( FilePath, json );
		HackApp.CoreSettings.Set("AppConfigFilePath", FilePath);
	}
}
