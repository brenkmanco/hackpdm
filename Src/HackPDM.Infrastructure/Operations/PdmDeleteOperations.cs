using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;

using OClient = HackPDM.Infrastructure.Odoo.OdooClient;

namespace HackPDM.Infrastructure.Operations;

/// <summary>
/// Infrastructure service for permanent and logical delete operations on PDM entities.
/// Encapsulates Odoo XML-RPC model calls, relationship cleanup, and attachment purges.
/// </summary>
public static class PdmDeleteOperations
{
    public static async Task<bool> PermanentDeleteVersionProperty(ArrayList ids, Action<StatusMessage, string>? log = null)
    {
        if (ids is null || ids.Count < 1)
            return false;

        HpVersionProperty[]? vProps = await HpVersionProperty.GetRecordsBySearchAsync([new ArrayList() { "version_id", "in", ids }]);
        bool deletedVersionProps;

        if (vProps is not null && vProps.Length > 0)
        {
            ArrayList newIds = vProps.GetIDs();
            log?.Invoke(StatusMessage.PROCESSING, "Deleting version properties...");
            deletedVersionProps = await OClient.DeleteAsync(HpVersionProperty.GetHpModel(), [newIds], 100000);
            if (deletedVersionProps)
            {
                log?.Invoke(StatusMessage.SUCCESS, $"Deleted version properties: {string.Join(", ", newIds.ToArray())}");
            }
            else
            {
                log?.Invoke(StatusMessage.ERROR, "Unable to delete version properties");
            }
        }
        else
        {
            deletedVersionProps = true;
            log?.Invoke(StatusMessage.SKIP, "No version properties to delete");
        }

        return deletedVersionProps;
    }

    public static async Task<bool> PermanentDeleteVersionRelationships(ArrayList ids, Action<StatusMessage, string>? log = null)
    {
        if (ids is null || ids.Count < 1)
            return false;

        HpVersionRelationship[]? vRelationsParent = await HpVersionRelationship.GetRecordsBySearchAsync([new ArrayList() { "parent_id", "in", ids }]);
        HpVersionRelationship[]? vRelationsChild = await HpVersionRelationship.GetRecordsBySearchAsync([new ArrayList() { "child_id", "in", ids }]);

        bool deletedVersionRelParent;
        bool deletedVersionRelChild;

        if (vRelationsParent is not null && vRelationsParent.Length > 0)
        {
            ArrayList newIds = vRelationsParent.GetIDs();
            log?.Invoke(StatusMessage.PROCESSING, "Deleting parent version relationships...");
            deletedVersionRelParent = await OdooClient<HpVersionRelationship>.DeleteAsync([newIds], 100000);
            if (deletedVersionRelParent)
            {
                log?.Invoke(StatusMessage.SUCCESS, $"Deleted parent version relationships: {string.Join(", ", newIds.ToArray())}");
            }
            else
            {
                log?.Invoke(StatusMessage.ERROR, "Unable to delete parent version relationships");
            }
        }
        else
        {
            deletedVersionRelParent = true;
            log?.Invoke(StatusMessage.SKIP, "No version relationship parents to delete");
        }

        if (vRelationsChild is not null && vRelationsChild.Length > 0)
        {
            ArrayList newIds = vRelationsChild.GetIDs();
            log?.Invoke(StatusMessage.PROCESSING, "Deleting child version relationships...");
            deletedVersionRelChild = await OClient.DeleteAsync(HpVersionRelationship.GetHpModel(), [newIds], 100000);
            if (deletedVersionRelChild)
            {
                log?.Invoke(StatusMessage.SUCCESS, $"Deleted child version relationships: {string.Join(", ", newIds.ToArray())}");
            }
            else
            {
                log?.Invoke(StatusMessage.ERROR, "Unable to delete child version relationships");
            }
        }
        else
        {
            deletedVersionRelChild = true;
            log?.Invoke(StatusMessage.SKIP, "No version relationship children to delete");
        }

        return deletedVersionRelChild && deletedVersionRelParent;
    }

    public static async Task<bool> PermanentDeleteVersions(ArrayList ids, Action<StatusMessage, string>? log = null)
    {
        if (ids is null || ids.Count < 1)
            return false;

        HpVersion[]? versions = await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>(ids, "version_ids", includedFields: ["ID"]);
        ArrayList vIds = versions?.Select(v => v.id).ToArrayList() ?? [];

        bool deletedVersionsProps = false;
        bool deletedVersionsRel = false;
        IrAttachment[]? irAttachments = null;

        if (vIds.Count > 0)
        {
            deletedVersionsProps = await PermanentDeleteVersionProperty(vIds, log);
            deletedVersionsRel = await PermanentDeleteVersionRelationships(vIds, log);
            irAttachments = await IrAttachment.GetRecordsBySearchAsync(
            [
                new ArrayList() { "res_id", "in", vIds },
                new ArrayList() { "res_model", "=", HpVersion.GetHpModel() },
                new ArrayList() { "res_field", "=", "file_contents" },
            ]);
        }

        log?.Invoke(StatusMessage.PROCESSING, "Deleting IR Attachments...");
        bool deletedIrAttachments = deletedVersionsProps
                               && deletedVersionsRel
                               && (irAttachments is null
                                   || !irAttachments.Any()
                                   || await OClient.DeleteAsync(IrAttachment.GetHpModel(), [irAttachments.GetIDs()], 100000));

        if (deletedIrAttachments)
        {
            log?.Invoke(StatusMessage.SUCCESS, "Deleted IR Attachments");
        }
        else
        {
            log?.Invoke(StatusMessage.INFO, "unable to delete IR Attachments");
        }

        log?.Invoke(StatusMessage.PROCESSING, "Deleting versions...");
        bool deletedVersions = deletedIrAttachments
                          && (vIds.Count <= 0
                               || await OClient.DeleteAsync(HpVersion.GetHpModel(), [vIds], 100000));

        if (deletedVersions)
        {
            log?.Invoke(StatusMessage.SUCCESS, "Deleted versions");
        }
        else
        {
            log?.Invoke(StatusMessage.ERROR, "Unable to delete versions");
        }

        return deletedIrAttachments && deletedVersions;
    }

    public static async Task<bool> PermanentDeleteEntry(ArrayList ids, Action<StatusMessage, string>? log = null)
    {
        if (ids is null || ids.Count < 1)
            return false;

        bool deletedVersions = await PermanentDeleteVersions(ids, log);
        log?.Invoke(StatusMessage.PROCESSING, "Deleting entries...");
        bool deletedEntries = deletedVersions && OClient.Delete(HpEntry.GetHpModel(), [ids]);
        if (deletedEntries)
        {
            log?.Invoke(StatusMessage.SUCCESS, "Deleted entries");
        }
        else
        {
            log?.Invoke(StatusMessage.ERROR, "Unable to delete entries");
        }

        return deletedVersions && deletedEntries;
    }

    public static async Task<bool> LogicalDeleteEntries(HpEntry[]? entries, Action<StatusMessage, string>? log = null)
    {
        if (entries is null || entries.Length < 1)
            return false;

        bool allSuccess = true;
        foreach (var entry in entries)
        {
            if (entry is null) continue;
            log?.Invoke(StatusMessage.PROCESSING, $"Setting InActive {entry.name}: {entry.id}");
            await entry.LogicalDelete();
        }
        return allSuccess;
    }

    public static async Task<bool> LogicalUnDeleteEntries(HpEntry[]? entries, Action<StatusMessage, string>? log = null)
    {
        if (entries is null || entries.Length < 1)
            return false;

        bool allSuccess = true;
        foreach (var entry in entries)
        {
            if (entry is null) continue;
            log?.Invoke(StatusMessage.PROCESSING, $"Setting Active {entry.name}: {entry.id}");
            await entry.LogicalUnDelete();
        }
        return allSuccess;
    }
}
