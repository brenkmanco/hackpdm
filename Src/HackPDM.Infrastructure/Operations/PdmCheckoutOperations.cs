using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Infrastructure.Operations;

/// <summary>
/// Infrastructure service for PDM checkout and uncheckout workflows.
/// Decoupled from WinUI dialogs and view models using progress callbacks and status delegates.
/// </summary>
public static class PdmCheckoutOperations
{
    public static async Task CheckOutEntriesAsync(
        HpEntry[] entries,
        Action<StatusMessage, string>? log = null,
        Action<int, int>? progress = null)
    {
        var filteredEntries = FilterCheckoutEntries(entries);
        int total = filteredEntries.Count;
        log?.Invoke(StatusMessage.INFO, $"{total} check outs");

        int processed = 0;
        foreach (var entry in filteredEntries)
        {
            log?.Invoke(StatusMessage.PROCESSING, $"Checking out {entry.name} ({entry.id})");
            await CheckOutEntryAsync(entry);
            processed++;
            progress?.Invoke(processed, total);
        }
    }

    public static async Task UnCheckOutEntriesAsync(
        HpEntry[] entries,
        int currentUserId,
        Action<StatusMessage, string>? log = null,
        Action<int, int>? progress = null)
    {
        var filteredEntries = FilterUnCheckoutEntries(entries, currentUserId);
        int total = filteredEntries.Count;
        log?.Invoke(StatusMessage.INFO, $"{total} uncheck outs");

        int processed = 0;
        foreach (var entry in filteredEntries)
        {
            log?.Invoke(StatusMessage.PROCESSING, $"Unchecking out {entry.name} ({entry.id})");
            await UnCheckOutEntryAsync(entry);
            processed++;
            progress?.Invoke(processed, total);
        }
    }

    public static async Task CheckOutEntryAsync(HpEntry? entry)
    {
        if (entry is null) return;
        await entry.CheckOut();
    }

    public static async Task UnCheckOutEntryAsync(HpEntry? entry)
    {
        if (entry is null) return;
        await entry.UnCheckOut();
    }

    public static List<HpEntry> FilterCheckoutEntries(IEnumerable<HpEntry> entries)
    {
        var result = new List<HpEntry>();
        foreach (var entry in entries)
        {
            if (entry.checkout_user?.id is null or 0)
            {
                result.Add(entry);
            }
        }
        return result;
    }

    public static List<HpEntry> FilterUnCheckoutEntries(IEnumerable<HpEntry> entries, int currentUserId)
    {
        var result = new List<HpEntry>();
        foreach (var entry in entries)
        {
            if (entry.checkout_user is not null && entry.checkout_user == currentUserId)
            {
                result.Add(entry);
            }
        }
        return result;
    }
}
