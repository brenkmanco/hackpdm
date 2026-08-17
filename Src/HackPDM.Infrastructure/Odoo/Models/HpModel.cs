using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;

using HackPDM.Core.General;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

namespace HackPDM.Infrastructure.Odoo.Models;

public class HpModel : IHpOdooRecord
{
	public int? id { get; set; }
	public Many2One? commit_id { get; set; }
	IMany2One? IHpOdooRecord.commit_id { get => (IMany2One?)commit_id; set => commit_id = (Many2One?)value; }

	public static implicit operator int?(HpModel? model) => model?.id;
	public static implicit operator HpModel?(int? id) => id is null ? null : new() { id = id };
}
public class Many2One : HpModel
{
	public string? name { get; set; }

	public void Deconstruct(out HpModel? id, out string? name)
	{
		id = this.id;
		name = this.name;
	}
	public void Deconstruct(out string? name, out HpModel? id)
	{
		name = this.name;
		id = this.id;
	}
	public void Deconstruct(out int? id, out string? name)
	{
		id = this.id;
		name = this.name;
	}
	public void Deconstruct(out string? name, out int? id)
	{
		name = this.name;
		id = this.id;
	}

	public static implicit operator int?(Many2One? model) => model?.id;
	public static implicit operator Many2One?(int? id) => id is null ? null : new() { id = id };

	public static implicit operator string?(Many2One? model) => model?.name;
	public static implicit operator Many2One?(string? name) => name is null ? null : new() { name = name };

	public static implicit operator (int? id, string? name)?(Many2One? model) => (model?.id, model?.name);
	public static implicit operator Many2One?((int? id, string? name)? tuple) => tuple?.id is null && tuple?.name is null ? null : new() { id = tuple?.id, name = tuple?.name };

	public static implicit operator (string? name, int? id)?(Many2One? model) => (model?.name, model?.id);
	public static implicit operator Many2One?((string? name, int? id)? tuple) => tuple?.id is null && tuple?.name is null ? null : new() { id = tuple?.id, name = tuple?.name };
}
public class MultiRecord : IList<HpModel?>, IMultiRecord
{
	public HpModel? this[int index] { get => (Ids as IList<HpModel?>)?[index]; set => (Ids as IList<HpModel?>)?[index] = value; }

	public HpModel?[]? Ids { get; set; }

	public int Count => (Ids as ICollection<HpModel?>)?.Count ?? 0;

	public bool IsReadOnly => Ids is ICollection<HpModel?> { IsReadOnly: true };

	IHpOdooRecord?[]? IMultiRecord.Ids { get => Ids; set => Ids = value as HpModel?[]; }

	public void Add(HpModel? item)
	{
		(Ids as ICollection<HpModel?>)?.Add(item);
	}

	public void Clear()
	{
		(Ids as ICollection<HpModel?>)?.Clear();
	}

	public bool Contains(HpModel? item)
	{
		return (Ids as ICollection<HpModel?>)?.Contains(item) is true;
	}

	public void CopyTo(HpModel?[] array, int arrayIndex)
	{
		(Ids as ICollection<HpModel?>)?.CopyTo(array, arrayIndex);
	}

	public IEnumerator<HpModel?> GetEnumerator()
	{
		return (Ids as ICollection<HpModel?>)?.GetEnumerator() ?? Enumerable.Empty<HpModel?>().GetEnumerator();
	}

	public int IndexOf(HpModel? item)
	{
		return (Ids as IList<HpModel?>)?.IndexOf(item) ?? -1;
	}

	public void Insert(int index, HpModel? item)
	{
		(Ids as IList<HpModel?>)?.Insert(index, item);
	}

	public bool Remove(HpModel? item)
	{
		return (Ids as ICollection<HpModel?>)?.Remove(item) is true;
	}

	public void RemoveAt(int index)
	{
		(Ids as IList<HpModel?>)?.RemoveAt(index);
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return Ids?.GetEnumerator() ?? Enumerable.Empty<HpModel?>().GetEnumerator(); ;
	}

	public static implicit operator int?[]?(MultiRecord? multi) => [.. multi?.Ids ?? []];
	public static implicit operator MultiRecord?(int?[]? ids) => ids is null ? null : new() { Ids = [.. ids?.SkipSelect(id => (id is null, id is null ? null : new HpModel() { id = id }))!]};

	public static implicit operator int[](MultiRecord multi) => [.. multi?.Ids?.SkipNullSelect(hp => hp.id ?? 0) ?? []];
	public static implicit operator MultiRecord(int[] multi) => multi;

	public static implicit operator ArrayList(MultiRecord? multi) => [.. multi?.Ids ?? []];
	public static implicit operator MultiRecord?(ArrayList? ids) => ids is null ? null : new() { Ids = [.. ids.Cast<int?>()?.SkipSelect(id => (id is null, id is null ? null : new HpModel() { id = id }))!] };
}
public class Many2Many : MultiRecord 
{
	public static implicit operator int?[]?(Many2Many? multi) => [.. multi?.Ids ?? []];
	public static implicit operator Many2Many?(int?[]? ids) => ids is null ? null : new() { Ids = [.. ids?.SkipSelect(id => (id is null, id is null ? null : new HpModel() { id = id }))!] };

	public static implicit operator int[](Many2Many multi) => [.. multi?.Ids?.SkipNullSelect(hp => hp.id ?? 0) ?? []];
	public static implicit operator Many2Many(int[] multi) => multi;

	public static implicit operator ArrayList(Many2Many? multi) => [.. multi?.Ids ?? []];
	public static implicit operator Many2Many?(ArrayList? ids) => ids is null ? null : new() { Ids = [.. ids.Cast<int?>()?.SkipSelect(id => (id is null, id is null ? null : new HpModel() { id = id }))!] };
}
public class One2Many : MultiRecord 
{
	public static implicit operator int?[]?(One2Many? multi) => [.. multi?.Ids ?? []];
	public static implicit operator One2Many?(int?[]? ids) => ids is null ? null : new() { Ids = [.. ids?.SkipSelect(id => (id is null, id is null ? null : new HpModel() { id = id }))!] };

	public static implicit operator int[](One2Many multi) => [.. multi?.Ids?.SkipNullSelect(hp => hp.id ?? 0) ?? []];
	public static implicit operator One2Many(int[] multi) => multi;

	public static implicit operator ArrayList(One2Many? multi) => [.. multi?.Ids ?? []];
	public static implicit operator One2Many?(ArrayList? ids) => ids is null ? null : new() { Ids = [.. ids.Cast<int?>()?.SkipSelect(id => (id is null, id is null ? null : new HpModel() { id = id }))!] };
}
public static class Test<T> where T : HpBaseModelTransport<T>, new()
{

}