using System;
using System.Collections;
using System.Collections.Generic;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Tests.Mocks;

/// <summary>
/// Decoupled builder for creating customizable mock <see cref="HpEntry"/> instances.
/// Allows tests to construct specific entry states without boilerplate.
/// </summary>
public class HpEntryMockBuilder
{
    private int _id = 1;
    private string _name = "TestPart.sldprt";
    private string _windowsCompleteName = @"C:\Vault\TestPart.sldprt";
    private bool _deleted = false;
    private int? _latestVersionId = 10;
    private string? _latestVersionName = "v1";
    private int? _dirId = 100;
    private string? _dirName = "Vault";
    private int? _typeId = 1;
    private string? _typeName = "SolidWorks Part";
    private int? _checkoutUserId;
    private string? _checkoutUserName;
    private int? _checkoutNodeId;
    private DateTime? _checkoutDate;
    private int? _catId = 1;

    public HpEntryMockBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public HpEntryMockBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public HpEntryMockBuilder WithWindowsPath(string path)
    {
        _windowsCompleteName = path;
        return this;
    }

    public HpEntryMockBuilder WithDeleted(bool deleted)
    {
        _deleted = deleted;
        return this;
    }

    public HpEntryMockBuilder WithLatestVersion(int? versionId, string? versionName = null)
    {
        _latestVersionId = versionId;
        _latestVersionName = versionName ?? (versionId.HasValue ? $"v{versionId}" : null);
        return this;
    }

    public HpEntryMockBuilder WithDirectory(int? dirId, string? dirName = null)
    {
        _dirId = dirId;
        _dirName = dirName;
        return this;
    }

    public HpEntryMockBuilder WithType(int? typeId, string? typeName = null)
    {
        _typeId = typeId;
        _typeName = typeName;
        return this;
    }

    public HpEntryMockBuilder AsCheckedOut(int userId, string userName = "Test User", int? nodeId = 1)
    {
        _checkoutUserId = userId;
        _checkoutUserName = userName;
        _checkoutNodeId = nodeId;
        _checkoutDate = DateTime.UtcNow;
        return this;
    }

    public HpEntryMockBuilder AsCheckedIn()
    {
        _checkoutUserId = null;
        _checkoutUserName = null;
        _checkoutNodeId = null;
        _checkoutDate = null;
        return this;
    }

    public HpEntry Build()
    {
        return new HpEntry
        {
            id = _id,
            name = _name,
            windows_complete_name = _windowsCompleteName,
            deleted = _deleted,
            latest_version_id = _latestVersionId.HasValue ? new Many2One { id = _latestVersionId, name = _latestVersionName } : null,
            dir_id = _dirId.HasValue ? new Many2One { id = _dirId, name = _dirName } : null,
            type_id = _typeId.HasValue ? new Many2One { id = _typeId, name = _typeName } : null,
            checkout_user = _checkoutUserId.HasValue ? new Many2One { id = _checkoutUserId, name = _checkoutUserName } : null,
            checkout_node = _checkoutNodeId.HasValue ? new Many2One { id = _checkoutNodeId, name = "Node-1" } : null,
            checkout_date = _checkoutDate,
            cat_id = _catId.HasValue ? new Many2One { id = _catId, name = "CAD" } : null
        };
    }
}

/// <summary>
/// Decoupled builder for creating customizable mock <see cref="HpVersion"/> instances.
/// </summary>
public class HpVersionMockBuilder
{
    private int _id = 10;
    private string _name = "TestPart.sldprt";
    private string _fileExt = "sldprt";
    private string _checksum = "e5e9fa1ba31ecd1ae84f75caaa474f3a663f05f4";
    private string _windowsCompleteName = @"C:\Vault\TestPart.sldprt";
    private int? _entryId = 1;
    private int? _dirId = 100;
    private int? _nodeId = 1;
    private int? _checkoutUserId;
    private int? _attachmentId = 1000;
    private int? _fileSize = 102400;
    private DateTime? _fileModifyStamp = DateTime.UtcNow;
    private byte[]? _fileContents = [0x53, 0x57, 0x5F, 0x50, 0x41, 0x52, 0x54]; // "SW_PART"
    private byte[]? _previewImage;

    public HpVersionMockBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public HpVersionMockBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public HpVersionMockBuilder WithFileExtension(string ext)
    {
        _fileExt = ext;
        return this;
    }

    public HpVersionMockBuilder WithChecksum(string checksum)
    {
        _checksum = checksum;
        return this;
    }

    public HpVersionMockBuilder WithEntryId(int? entryId)
    {
        _entryId = entryId;
        return this;
    }

    public HpVersionMockBuilder WithAttachmentId(int? attachmentId)
    {
        _attachmentId = attachmentId;
        return this;
    }

    public HpVersionMockBuilder WithCheckoutUser(int? userId)
    {
        _checkoutUserId = userId;
        return this;
    }

    public HpVersionMockBuilder WithFileSize(int? size)
    {
        _fileSize = size;
        return this;
    }

    public HpVersionMockBuilder WithFileContents(byte[]? contents)
    {
        _fileContents = contents;
        return this;
    }

    public HpVersionMockBuilder WithPreviewImage(byte[]? preview)
    {
        _previewImage = preview;
        return this;
    }

    public HpVersion Build()
    {
        return new HpVersion
        {
            id = _id,
            name = _name,
            file_ext = _fileExt,
            checksum = _checksum,
            windows_complete_name = _windowsCompleteName,
            entry_id = _entryId.HasValue ? new Many2One { id = _entryId, name = _name } : null,
            dir_id = _dirId.HasValue ? new Many2One { id = _dirId, name = "Vault" } : null,
            node_id = _nodeId.HasValue ? new Many2One { id = _nodeId, name = "Node-1" } : null,
            checkout_user = _checkoutUserId.HasValue ? new Many2One { id = _checkoutUserId, name = "User" } : null,
            attachment_id = _attachmentId.HasValue ? new Many2One { id = _attachmentId, name = $"{_name}.attach" } : null,
            file_size = _fileSize,
            file_modify_stamp = _fileModifyStamp,
            file_contents = _fileContents,
            preview_image = _previewImage
        };
    }
}

/// <summary>
/// Decoupled builder for creating customizable mock <see cref="HpVersionProperty"/> instances.
/// </summary>
public class HpVersionPropertyMockBuilder
{
    private int _id = 100;
    private int _versionId = 10;
    private int _propId = 1;
    private string _propName = "Material";
    private string _swConfigName = "@";
    private string? _textValue = "Aluminum 6061-T6";
    private float? _numberValue;
    private bool? _yesNoValue;

    public HpVersionPropertyMockBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public HpVersionPropertyMockBuilder WithVersionId(int versionId)
    {
        _versionId = versionId;
        return this;
    }

    public HpVersionPropertyMockBuilder WithName(string propName)
    {
        _propName = propName;
        return this;
    }

    public HpVersionPropertyMockBuilder WithConfigName(string config)
    {
        _swConfigName = config;
        return this;
    }

    public HpVersionPropertyMockBuilder WithTextValue(string? text)
    {
        _textValue = text;
        _numberValue = null;
        _yesNoValue = null;
        return this;
    }

    public HpVersionPropertyMockBuilder WithNumberValue(float number)
    {
        _numberValue = number;
        _textValue = null;
        _yesNoValue = null;
        return this;
    }

    public HpVersionPropertyMockBuilder WithBooleanValue(bool value)
    {
        _yesNoValue = value;
        _textValue = null;
        _numberValue = null;
        return this;
    }

    public HpVersionProperty Build()
    {
        return new HpVersionProperty
        {
            id = _id,
            version_id = new Many2One { id = _versionId, name = $"Version-{_versionId}" },
            prop_id = new Many2One { id = _propId, name = _propName },
            prop_name = _propName,
            sw_config_name = _swConfigName,
            text_value = _textValue,
            number_value = _numberValue,
            yesno_value = _yesNoValue
        };
    }
}

/// <summary>
/// Decoupled builder for creating customizable mock <see cref="HpVersionRelationship"/> instances.
/// </summary>
public class HpVersionRelationshipMockBuilder
{
    private int _id = 200;
    private int _parentId = 10;
    private int _childId = 20;

    public HpVersionRelationshipMockBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public HpVersionRelationshipMockBuilder WithParentId(int parentId)
    {
        _parentId = parentId;
        return this;
    }

    public HpVersionRelationshipMockBuilder WithChildId(int childId)
    {
        _childId = childId;
        return this;
    }

    public HpVersionRelationship Build()
    {
        return new HpVersionRelationship
        {
            id = _id,
            parent_id = new Many2One { id = _parentId, name = $"Parent-{_parentId}" },
            child_id = new Many2One { id = _childId, name = $"Child-{_childId}" }
        };
    }
}

/// <summary>
/// Decoupled builder for creating customizable mock <see cref="IrAttachment"/> instances.
/// </summary>
public class IrAttachmentMockBuilder
{
    private int _id = 1000;
    private string _name = "TestPart.sldprt";
    private int _resId = 10;
    private string _resModel = "hp.version";
    private string _checksum = "e5e9fa1ba31ecd1ae84f75caaa474f3a663f05f4";
    private string _mimetype = "application/octet-stream";
    private string _type = "binary";
    private int _fileSize = 102400;

    public IrAttachmentMockBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public IrAttachmentMockBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public IrAttachmentMockBuilder WithResId(int resId, string resModel = "hp.version")
    {
        _resId = resId;
        _resModel = resModel;
        return this;
    }

    public IrAttachmentMockBuilder WithChecksum(string checksum)
    {
        _checksum = checksum;
        return this;
    }

    public IrAttachmentMockBuilder WithFileSize(int fileSize)
    {
        _fileSize = fileSize;
        return this;
    }

    public IrAttachment Build()
    {
        return new IrAttachment
        {
            id = _id,
            name = _name,
            res_id = new Many2One { id = _resId, name = $"Version-{_resId}" },
            res_model = _resModel,
            checksum = _checksum,
            mimetype = _mimetype,
            type = _type,
            file_size = _fileSize
        };
    }
}

/// <summary>
/// Static factory providing decoupled mock Odoo data sets and pre-built models.
/// </summary>
public static class MockOdooFactory
{
    public static HpEntry CreateEntry(int id = 1, string name = "Bracket.sldprt", bool deleted = false, int? latestVersionId = 10)
        => new HpEntryMockBuilder().WithId(id).WithName(name).WithDeleted(deleted).WithLatestVersion(latestVersionId).Build();

    public static HpEntry CreateCheckedOutEntry(int id = 2, string name = "Assembly.sldasm", int userId = 42, string userName = "Engineer")
        => new HpEntryMockBuilder().WithId(id).WithName(name).AsCheckedOut(userId, userName).Build();

    public static HpEntry CreateDeletedEntry(int id = 3, string name = "OldPart.sldprt")
        => new HpEntryMockBuilder().WithId(id).WithName(name).WithDeleted(true).Build();

    public static HpVersion CreateVersion(int id = 10, int entryId = 1, string name = "Bracket.sldprt", string ext = "sldprt", int fileSize = 20480)
        => new HpVersionMockBuilder().WithId(id).WithEntryId(entryId).WithName(name).WithFileExtension(ext).WithFileSize(fileSize).Build();

    public static HpVersionProperty CreateProperty(int id = 100, int versionId = 10, string name = "Material", string value = "Steel 1018")
        => new HpVersionPropertyMockBuilder().WithId(id).WithVersionId(versionId).WithName(name).WithTextValue(value).Build();

    public static HpVersionRelationship CreateRelationship(int id = 200, int parentVersionId = 50, int childVersionId = 10)
        => new HpVersionRelationshipMockBuilder().WithId(id).WithParentId(parentVersionId).WithChildId(childVersionId).Build();

    public static IrAttachment CreateAttachment(int id = 1000, int resId = 10, string name = "Bracket.sldprt", int fileSize = 20480)
        => new IrAttachmentMockBuilder().WithId(id).WithResId(resId).WithName(name).WithFileSize(fileSize).Build();
}
