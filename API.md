
# HackPDM API Documentation

This document describes the primary interfaces and base classes used for representing entities in the HackPDM system.

## Interfaces in `HackPDM.Abstractions`

### `IConvert<T>`
Defines a generic interface for converting a `Hashtable` into a strongly-typed object of type `T`.
- **`ConvertFromHt(Hashtable ht)`**: Returns a new instance of `T` populated with data from the given `Hashtable`.

### `ICloneable<T>`
Provides a generic cloning mechanism.
- **`Clone()`**: Returns a cloned instance of `T`.

### `IRowData<T>`
A marker interface that extends `ICloneable<T>`. Primarily used to denote types that represent rows of data.

### `ITreeItem`
Represents an item within a hierarchical tree structure.
- **`Tag`**: Optional generic object to store arbitrary data.
- **`Children`**: An `IEnumerable<ITreeItem>` representing the child nodes of this item.

### `IListItem<T>`
Represents a selectable item in a list containing a value of type `T`.
- **`Value`**: The strongly-typed underlying value.
- **`IsSelected`**: A boolean indicating if the item is selected.

### `ISettingsProvider`
Abstracts the storage and retrieval of application settings.
- **`Get<T>(string key, T? defaultValue = default)`**: Retrieves a setting by key, converting it to type `T`.
- **`Set<T>(string key, T value)`**: Stores a setting under the specified key.
- **`Contains(string key)`**: Checks if a setting exists.
- **`Remove(string key)`**: Deletes a setting by key.

### `IHackDefaultBase`
Defines core path and formatting defaults used across HackPDM.
- Properties like `PwaPathAbsolute`, `CurrentPath`, and `ByteSizeMultiplier` dictate how files are accessed and displayed.

## Interfaces in `HackPDM.Domain.OdooModels`

All Odoo models inherit from a base record interface and use attributes like `[OdooModel]` to map to the backend database.

- **`IHpOdooRecord`**: The foundational interface for all Odoo records.
- **`IHpDirectoryModel`**: Represents a directory in the PDM structure.
- **`IHpEntryModel`**: Represents a specific file entry.
- **`IHpCategoryModel`**: Represents file or metadata categories.
- **`IHpVersionModel`**: Represents a version of a specific entry.
- **`IHpSettingModel`**: Represents an Odoo-side setting.
- **`IIrAttachment`**: Maps to standard Odoo attachments containing the raw file payload.

## Core Models

### `HpBaseModel`
The abstract base implementation of `IHpOdooRecord`, providing default properties like `id` and common mapping functions.

### `HpRecordStaged`
Used to manage staged records prior to them being fully committed to the remote server.

## Extension Methods
HackPDM contains multiple generic extensions in `Src/HackPDM.Core/General/ExtensionMethods.cs`, such as `GetRange(Range range)` for ArrayLists and multiple operations on concurrent sets and standard arrays.
