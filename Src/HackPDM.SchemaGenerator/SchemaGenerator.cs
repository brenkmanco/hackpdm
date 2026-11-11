using HackPDM.Shared.GlobalData;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

using HackPDM.SchemaGenerator.GeneratedTypes;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HackPDM.SchemaGenerator;

/// <summary>
/// For each [OdooModel] class: its inbound field map, outbound <c>ToOdoo</c> and field list; and for
/// all of them the model-name dictionary and the type-keyed registry. Each model is read into an
/// <see cref="OdooModelGen"/> of plain values, so an edit only regenerates the models it changed
/// (the registry still regenerates whenever the set of models changes).
/// </summary>
[Generator]
public sealed class OdooSchemaGenerator : IIncrementalGenerator
{
	private const string OdooModelAttribute = "HackPDM.Shared.OdooAttributes.OdooModelAttribute";
	private const string OdooPropAttribute = "HackPDM.Shared.OdooAttributes.OdooPropAttribute";

	private static readonly DiagnosticDescriptor InvalidOdooProp = new(
		id: "HPDM001",
		title: "Malformed [OdooProp]",
		messageFormat: "{0}: [OdooProp] needs a field type and a field name; the property is not mapped",
		category: "HackPDM.SchemaGenerator",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true );

	private static readonly DiagnosticDescriptor InvalidOdooModel = new(
		id: "HPDM002",
		title: "Malformed [OdooModel]",
		messageFormat: "{0}: [OdooModel] needs a name and a database model name; no code generated for it",
		category: "HackPDM.SchemaGenerator",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true );

	public void Initialize( IncrementalGeneratorInitializationContext context )
	{
		IncrementalValuesProvider<OdooModelGen?> models = context.SyntaxProvider.ForAttributeWithMetadataName(
			OdooModelAttribute,
			predicate: static ( node, _ ) => node is ClassDeclarationSyntax,
			transform: static ( ctx, token ) => ReadModel( ctx, token ) );

		context.RegisterSourceOutput( models, static ( spc, model ) =>
		{
			if( model is null )
				return;
			foreach( string property in model.InvalidProperties )
				spc.ReportDiagnostic( Diagnostic.Create( InvalidOdooProp, Location.None, $"{model.Name}.{property}" ) );
			if( model.DbName.Length == 0 )
			{
				spc.ReportDiagnostic( Diagnostic.Create( InvalidOdooModel, Location.None, model.FullName ) );
				return;
			}
			EmitModelBundle( spc, model );
		} );

		IncrementalValueProvider<ImmutableArray<OdooModelGen>> all = models
			.Where( static m => m is { DbName.Length: > 0 } )
			.Select( static ( m, _ ) => m! )
			.Collect();

		context.RegisterSourceOutput( all, static ( spc, list ) =>
		{
			// stable output whatever order the compiler visits the files in
			var sorted = list.OrderBy( m => m.FullName, StringComparer.Ordinal ).ToList();
			EmitHpBaseModelPartial( spc, sorted );	// model name → Odoo model (HpModelDictionaryGen)
			EmitRegistry( spc, sorted );			// model type → its generated mapper and field selector
		} );
	}

	private static OdooModelGen? ReadModel( GeneratorAttributeSyntaxContext ctx, CancellationToken token )
	{
		if( ctx.TargetSymbol is not INamedTypeSymbol classSymbol )
			return null;
		AttributeData attr = ctx.Attributes[ 0 ];
		string dbName = attr.ConstructorArguments.Length > 1 && attr.ConstructorArguments[ 1 ].Value is string db ? db : "";

		var properties = ImmutableArray.CreateBuilder<OdooPropertyGen>();
		var invalid = ImmutableArray.CreateBuilder<string>();
		foreach( IPropertySymbol prop in classSymbol.GetMembers().OfType<IPropertySymbol>() )
		{
			token.ThrowIfCancellationRequested();
			AttributeData? propAttr = prop.GetAttributes()
				.FirstOrDefault( a => a.AttributeClass?.ToDisplayString() == OdooPropAttribute );
			if( propAttr is null )
				continue;
			if( propAttr.ConstructorArguments.Length < 2
				|| propAttr.ConstructorArguments[ 0 ].Value is not int typeVal
				|| propAttr.ConstructorArguments[ 1 ].Value is not string fieldName )
			{
				invalid.Add( prop.Name );
				continue;
			}
			string clrType = prop.Type.ToDisplayString( SymbolDisplayFormat.FullyQualifiedFormat );
			if( !prop.Type.IsValueType && prop.NullableAnnotation == NullableAnnotation.Annotated )
				clrType += "?";
			properties.Add( new OdooPropertyGen( (OdooFieldType)typeVal, fieldName, prop.Name, clrType ) );
		}

		return new OdooModelGen(
			classSymbol.Name,
			classSymbol.ContainingNamespace.ToDisplayString(),
			classSymbol.ToDisplayString( SymbolDisplayFormat.FullyQualifiedFormat ),
			dbName,
			new( properties.ToImmutable() ),
			new( invalid.ToImmutable() ) );
	}

	private static void EmitModelBundle( SourceProductionContext context, OdooModelGen model )
	{
		string className = model.Name;
		string ns = model.Namespace;

		// inbound assignments
		var assignmentLines = string.Join( ",\n            ",
			model.Properties.Select( p =>
				$"[\"{p.OdooFieldName}\"] = (model, value) => model.{p.PropertyName} = ({p.ClrType})global::HackPDM.Infrastructure.Odoo.OdooValueConverter.Convert((global::HackPDM.Shared.GlobalData.OdooFieldType.{p.OdooType}), value)" ) );

		// outbound entries
		var outboundLines = string.Join( ",\n            ",
			model.Properties.Select( p => $"[\"{p.OdooFieldName}\"] = {GenerateOutboundExpression( p, "model" )}" ) );

		// field names
		var fieldNames = string.Join( ",\n        ",
			model.Properties.Select( p => $"\"{p.OdooFieldName}\"" ) );

		var source = $$"""
// <auto-generated />
namespace {{ns}};

public static partial class OdooAssignments_{{className}}
{
    public static readonly global::System.Collections.Generic.Dictionary<string, global::System.Action<{{ns}}.{{className}}, object?>> Map =
        new()
        {
            {{assignmentLines}}
        };

    public static global::System.Collections.Generic.Dictionary<string, object?> ToOdoo({{ns}}.{{className}} model) =>
        new()
        {
            {{outboundLines}}
        };
}

public static partial class OdooFieldSelection_{{className}}
{
    internal static readonly string[] ModelFields = new[]
    {
        {{fieldNames}}
    };

    public static string[] GetFields(
        string[]? excluded = null,
        string[]? included = null,
        string[]? insert = null) =>
        global::HackPDM.Infrastructure.Odoo.Models.OdooFieldSelector.Select(
            ModelFields, excluded, included, insert);
}


public partial class {{className}}
{
    public override global::System.Collections.Generic.Dictionary<string, object?> ToOdoo() =>
        global::{{ns}}.OdooAssignments_{{className}}.ToOdoo(this);
}

""";

		context.AddSource( $"{className}_OdooModelBundle.g.cs", source );
	}
	/// <summary>How each field type's value is written to Odoo, as an expression over the property access.</summary>
	private static readonly Dictionary<OdooFieldType, Func<string, string>> OutboundEmitters = new()
	{
		[ OdooFieldType.Integer ] = access => $"(object?){access}",
		[ OdooFieldType.Float ] = access => $"{access} is null ? null : global::System.Convert.ToDouble({access})",
		[ OdooFieldType.Boolean ] = access => $"{access} is true ? 1 : {access} is false ? 0 : null",
		[ OdooFieldType.Date ] = access => $"{access}?.ToString(\"yyyy-MM-dd\")",
		[ OdooFieldType.DateTime ] = access => $"global::HackPDM.Shared.GlobalData.OdooDefaultsConstants.OdooDateFormat({access})",
		[ OdooFieldType.Many2One ] = access => $"{access}?.id",
		// replace the whole set: [(6, 0, ids)]
		[ OdooFieldType.One2Many ] = X2ManyOutbound,
		[ OdooFieldType.Many2Many ] = X2ManyOutbound,
		[ OdooFieldType.Binary ] = access => $"global::HackPDM.Core.FileOperations.ConvertBytesToBase64({access})",
		[ OdooFieldType.Image ] = access => $"global::HackPDM.Core.FileOperations.ConvertBytesToBase64({access})",
	};

	private static string X2ManyOutbound( string access )
		=> $"{access} is null ? null : new object?[] {{ new object?[] {{ 6, 0, (int[]){access} }} }}";

	/// <summary>The property as is for text, JSON and anything without an emitter.</summary>
	private static string GenerateOutboundExpression( OdooPropertyGen p, string modelVar )
	{
		string access = $"{modelVar}.{p.PropertyName}";
		return OutboundEmitters.TryGetValue( p.OdooType, out var emit ) ? emit( access ) : access;
	}

	private static void EmitHpBaseModelPartial( SourceProductionContext context, List<OdooModelGen> models )
	{
		var dictEntries = string.Join(",\n        ",
			models.Select(m => $"{{ nameof({m.FullName}), \"{m.DbName}\" }}"));

		var source = $$"""
// <auto-generated />
namespace HackPDM.Infrastructure.Odoo.Models;

public partial class HpBaseModelTransport
{
    public static readonly global::System.Collections.Generic.Dictionary<string, string> HpModelDictionaryGen =
        new()
        {
            {{dictEntries}}
        };
}
""";

		context.AddSource(
			"HpBaseModelTransport_HpModelDictionary.g.cs",
			source
		);
	}

	/// <summary>
	/// One registry keyed by the model's type (the facades used to switch on <c>typeof(TModel).Name</c>,
	/// so two models with the same class name in different namespaces would have collided), and the
	/// generic caches that read it once per model type.
	/// </summary>
	private static void EmitRegistry( SourceProductionContext context, List<OdooModelGen> models )
	{
		string Entries( Func<OdooModelGen, string> value )
			=> string.Join( ",\n            ", models.Select( m => $"[typeof({m.FullName})] = {value( m )}" ) );

		var source = $$"""
// <auto-generated />
namespace HackPDM.Infrastructure.Odoo.Models;

internal static class OdooGeneratedRegistry
{
    /// <summary>Model type → its inbound assignment map (a Dictionary&lt;string, Action&lt;TModel, object?&gt;&gt;).</summary>
    public static readonly global::System.Collections.Generic.Dictionary<global::System.Type, object> Assignments = new()
    {
            {{Entries( m => $"global::{m.Namespace}.OdooAssignments_{m.Name}.Map" )}}
    };

    /// <summary>Model type → its field selector (excluded, included, insert).</summary>
    public static readonly global::System.Collections.Generic.Dictionary<global::System.Type, global::System.Func<string[]?, string[]?, string[]?, string[]>> FieldSelections = new()
    {
            {{Entries( m => $"global::{m.Namespace}.OdooFieldSelection_{m.Name}.GetFields" )}}
    };

    public static T Lookup<T>( global::System.Collections.Generic.Dictionary<global::System.Type, T> registry, global::System.Type model, string what )
        => registry.TryGetValue( model, out T? found )
            ? found
            : throw new global::System.InvalidOperationException( $"No {what} generated for {model.FullName}" );
}

public static class OdooAssignments<TModel>
{
    public static global::System.Collections.Generic.Dictionary<string, global::System.Action<TModel, object?>> Map =>
        OdooAssignmentsCache<TModel>.Map;
}

internal static class OdooAssignmentsCache<TModel>
{
    public static readonly global::System.Collections.Generic.Dictionary<string, global::System.Action<TModel, object?>> Map =
        (global::System.Collections.Generic.Dictionary<string, global::System.Action<TModel, object?>>)
            OdooGeneratedRegistry.Lookup( OdooGeneratedRegistry.Assignments, typeof(TModel), "OdooAssignments" );
}

internal static class OdooFieldSelectionCache<TModel>
{
    public static readonly global::System.Func<string[]?, string[]?, string[]?, string[]> GetFields =
        OdooGeneratedRegistry.Lookup( OdooGeneratedRegistry.FieldSelections, typeof(TModel), "OdooFieldSelection" );
}
""";

		context.AddSource( "OdooGeneratedRegistry.g.cs", source );
	}
}

