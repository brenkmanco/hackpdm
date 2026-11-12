using System.Collections.Generic;

namespace HackPDM.Mvvm.Models.Hack;

/// <summary>Rows for one directory plus the counts shown under the entry list.</summary>
/// <param name="Rows">The rows to show: remote entries first, then local-only files.</param>
/// <param name="RemoteCount">Entries Odoo has in this directory.</param>
/// <param name="LocalOnlyCount">Files on disk with no remote entry.</param>
/// <param name="LocalAndRemoteCount">Remote entries that also exist on disk.</param>
public sealed record EntryListResult( List<EntryRow> Rows, int RemoteCount, int LocalOnlyCount, int LocalAndRemoteCount );
