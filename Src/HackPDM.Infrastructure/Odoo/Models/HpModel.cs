using System.Linq;
﻿using System;
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
	IMany2One? IHpOdooRecord.commit_id { get => commit_id; set => commit_id = (Many2One?)value; }

	public static implicit operator int?(HpModel? model) => model?.id;
	public static implicit operator HpModel?(int? id) => id is null ? null : new() { id = id };
}
public class Many2One : HpModel, IMany2One
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
/// <summary>
/// The records of a one2many / many2many field (each an <see cref="HpModel"/> with its id). The one
/// base for <see cref="Many2Many"/> and <see cref="One2Many"/>, which used to repeat its conversions
/// (and turned <c>int[]</c> into themselves by calling the same conversion forever).
/// </summary>
public abstract class MultiRecord : IList<HpModel?>, IMultiRecord
{
	// a list: the array this used to wrap ignored Add, so a many2many always read as empty
	private readonly List<HpModel?> _items = [];

	protected MultiRecord() { }
	protected MultiRecord( IEnumerable<int> ids ) => _items.AddRange( ids.Select( id => (HpModel?)new HpModel { id = id } ) );

	public HpModel?[]? Ids
	{
		get => [ .. _items ];
		set
		{
			_items.Clear();
			if( value is not null )
				_items.AddRange( value );
		}
	}
	IHpOdooRecord?[]? IMultiRecord.Ids
	{
		get => Ids;
		set => Ids = value?.Select( r => r as HpModel ?? ( r is null ? null : new HpModel { id = r.id } ) ).ToArray();
	}

	/// <summary>The ids (records without one left out).</summary>
	public int[] IdValues => [ .. _items.Select( m => m?.id ).OfType<int>() ];

	public static implicit operator int[]( MultiRecord? multi ) => multi?.IdValues ?? [];

	public HpModel? this[ int index ] { get => _items[ index ]; set => _items[ index ] = value; }
	public int Count => _items.Count;
	public bool IsReadOnly => false;
	public void Add( HpModel? item ) => _items.Add( item );
	public void Clear() => _items.Clear();
	public bool Contains( HpModel? item ) => _items.Contains( item );
	public void CopyTo( HpModel?[] array, int arrayIndex ) => _items.CopyTo( array, arrayIndex );
	public IEnumerator<HpModel?> GetEnumerator() => _items.GetEnumerator();
	public int IndexOf( HpModel? item ) => _items.IndexOf( item );
	public void Insert( int index, HpModel? item ) => _items.Insert( index, item );
	public bool Remove( HpModel? item ) => _items.Remove( item );
	public void RemoveAt( int index ) => _items.RemoveAt( index );
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
public sealed class Many2Many : MultiRecord, IMany2Many
{
	public Many2Many() { }
	public Many2Many( IEnumerable<int> ids ) : base( ids ) { }
	public static implicit operator Many2Many( int[] ids ) => new( ids );
}
public sealed class One2Many : MultiRecord, IOne2Many
{
	public One2Many() { }
	public One2Many( IEnumerable<int> ids ) : base( ids ) { }
	public static implicit operator One2Many( int[] ids ) => new( ids );
}

/// <summary>Helpers for collections of model records.</summary>
public static class RecordIds
{
	/// <summary>Collects the ids of records.</summary>
	/// <param name="models">The records.</param>
	/// <returns>The ids in order, leaving out records whose id is <see langword="null"/> or 0.</returns>
	public static int[] Ids( this IEnumerable<HackPDM.Domain.OdooModels.Models.HpBaseModel> models )
		=> [ .. models.Select( model => model.id ).OfType<int>().Where( id => id != 0 ) ];
}
