using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;

namespace HackPDM.Core;

public sealed class ReplaceExpressionVisitor : ExpressionVisitor
{
	private readonly IReadOnlyDictionary<Expression, Expression> _replacements;

	public ReplaceExpressionVisitor(IReadOnlyDictionary<Expression, Expression> replacements)
	{
		_replacements = _replacements = replacements ?? throw new ArgumentNullException(nameof(replacements));
	}

	[return: NotNullIfNotNull(nameof(node))]
	public override Expression? Visit(Expression? node) 
		=> node is null ? null : _replacements.TryGetValue(node, out var replacement) ? replacement : base.Visit(node);
}
public static class ExpressionExtensions
{
	public static Expression Replace(this Expression expression, Expression from, Expression to)
	{
		if (expression == null) throw new ArgumentNullException(nameof(expression));
		if (from == null) throw new ArgumentNullException(nameof(from));
		if (to == null) throw new ArgumentNullException(nameof(to));

		var dict = new Dictionary<Expression, Expression> { [from] = to };
		var visitor = new ReplaceExpressionVisitor(dict);
		return visitor.Visit(expression);
	}

	public static Expression ReplaceMany(this Expression expression, params (Expression from, Expression to)[] pairs)
	{
		if (expression == null) throw new ArgumentNullException(nameof(expression));
		if (pairs == null) throw new ArgumentNullException(nameof(pairs));

		var dict = new Dictionary<Expression, Expression>();
		foreach (var (from, to) in pairs)
		{
			if (from == null) throw new ArgumentNullException(nameof(from));
			if (to == null) throw new ArgumentNullException(nameof(to));
			dict[from] = to;
		}

		var visitor = new ReplaceExpressionVisitor(dict);
		return visitor.Visit(expression);
	}
}
public static class QueryableHijackExtensions
{
	/// <summary>
	/// Injects one or more computed values into a projection without materializing,
	/// by rewriting the model expression into a simple Select.
	///
	/// model: (T item, T2 inj1, T3 inj2, ..., TOut) => ...
	/// injectors: Expression<Func<T, T2>>, Expression<Func<T, T3>>, ...
	/// </summary>
	public static IQueryable<TOut> RewriteQuery<T, TOut>(
		this IQueryable<T> source,
		LambdaExpression model,
		params LambdaExpression[] injectors)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(model);
		ArgumentNullException.ThrowIfNull(injectors);

		// model must be a Func<T, ...injectors..., TOut>
		if (!typeof(Delegate).IsAssignableFrom(model.Type))
			throw new ArgumentException("Model must be a lambda expression.", nameof(model));

		var modelParams = model.Parameters;
		if (modelParams.Count != injectors.Length + 1)
		{
			throw new ArgumentException(
				$"Model must have exactly {injectors.Length + 1} parameters " +
				$"(1 item + {injectors.Length} injectors), but had {modelParams.Count}.",
				nameof(model));
		}

		// new parameter: the item in the final Select
		var itemParam = Expression.Parameter(typeof(T), "item");

		// Build injector bodies: injectorN(item)
		var injectedBodies = new Expression[injectors.Length];

		for (int i = 0; i < injectors.Length; i++)
		{
			var inj = injectors[i];
			if (inj == null)
				throw new ArgumentNullException(nameof(injectors), $"Injector at index {i} is null.");

			if (inj is not LambdaExpression injLambda)
				throw new ArgumentException($"Injector at index {i} must be a lambda expression.", nameof(injectors));

			// Expecting Func<T, Tn>
			if (injLambda.Parameters.Count != 1 || injLambda.Parameters[0].Type != typeof(T))
			{
				throw new ArgumentException(
					$"Injector at index {i} must have exactly one parameter of type {typeof(T).Name}.",
					nameof(injectors));
			}

			// injLambda.Body with its parameter replaced by itemParam
			injectedBodies[i] = injLambda.Body.Replace(injLambda.Parameters[0], itemParam);
		}

		// Build replacements: model's first param → itemParam, then model's nth param → injectedBodies[n-1]
		var replacements = new List<(Expression from, Expression to)>
		{
			(modelParams[0], itemParam)
		};

		for (int i = 0; i < injectors.Length; i++)
		{
			var modelParamForInjector = modelParams[i + 1];
			var injectedBody = injectedBodies[i];

			// Optional: validate type alignment
			if (!modelParamForInjector.Type.IsAssignableFrom(injectedBody.Type))
			{
				throw new InvalidOperationException(
					$"Model parameter '{modelParamForInjector.Name}' expects type " +
					$"{modelParamForInjector.Type.Name} but injector {i} returns {injectedBody.Type.Name}.");
			}

			replacements.Add((modelParamForInjector, injectedBody));
		}

		// Rewrite the model body with the new bindings
		var newBody = model.Body.ReplaceMany([.. replacements]);

		// Build final lambda: item => rewrittenBody
		var finalLambda = Expression.Lambda<Func<T, TOut>>(newBody, itemParam);

		// Let the provider (EF, etc.) translate it
		return source.Select(finalLambda);
	}
}


