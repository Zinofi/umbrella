﻿using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Sorting;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Expressions;
using Umbrella.Utilities.Extensions;
using Umbrella.Utilities.Helpers;

namespace Umbrella.Utilities.Data
{
	/// <summary>
	/// A factory used to create instance of <see cref="IDataExpression"/>.
	/// </summary>
	/// <seealso cref="IDataExpressionFactory" />
	public class DataExpressionFactory : IDataExpressionFactory
	{
		private readonly ILogger<DataExpressionFactory> _logger;
		private readonly ConcurrentDictionary<string, (MemberExpression member, LambdaExpression lambda, Lazy<Delegate> @delegate, Lazy<string> memberPath)> _cache = new ConcurrentDictionary<string, (MemberExpression, LambdaExpression, Lazy<Delegate>, Lazy<string>)>();

		/// <summary>
		/// Initializes a new instance of the <see cref="DataExpressionFactory"/> class.
		/// </summary>
		/// <param name="logger">The logger.</param>
		public DataExpressionFactory(
			ILogger<DataExpressionFactory> logger)
		{
			_logger = logger;
		}

		/// <inheritdoc />
		public IDataExpression? Create<TDescriptor>(Type elementType, TDescriptor descriptor)
			where TDescriptor : IDataExpressionDescriptor
		{
			Guard.ArgumentNotNull(elementType, nameof(elementType));
			Guard.ArgumentNotNull(descriptor, nameof(descriptor));

			try
			{
				if (string.IsNullOrWhiteSpace(descriptor.MemberPath))
					return null;

				string cacheKey = $"{elementType.FullName}:{descriptor}";

				var result = _cache.GetOrAdd(cacheKey, key =>
				{
					Type innerType = elementType.GetGenericArguments()[0];

					ParameterExpression parameter = Expression.Parameter(innerType);

					var memberAccess = UmbrellaDynamicExpression.CreateMemberAccess(parameter, descriptor.MemberPath, false);

					if (memberAccess == null)
						return default;

					UnaryExpression objectMemberExpression = Expression.Convert(memberAccess, typeof(object));
					LambdaExpression lambdaExpression = Expression.Lambda(elementType.GetProperty("Expression")?.PropertyType.GetGenericArguments()[0], objectMemberExpression, parameter);

					return (memberAccess, lambdaExpression, new Lazy<Delegate>(() => lambdaExpression.Compile()), new Lazy<string>(() => lambdaExpression.GetMemberPath()));
				});

				if (result == default)
					return default;

				if (descriptor is FilterExpressionDescriptor filterExpressionDescriptor)
				{
					string descriptorValue = filterExpressionDescriptor.Value;

					if (result.member.Type.IsEnum && EnumHelper.TryParseEnum(result.member.Type, filterExpressionDescriptor.Value, true, out object enumValue))
					{
						object underlyingValue = Convert.ChangeType(enumValue, result.member.Type.GetEnumUnderlyingType());

						if (underlyingValue != null)
							descriptorValue = underlyingValue.ToString();
					}

					return (IDataExpression)Activator.CreateInstance(elementType, result.lambda, result.@delegate, result.memberPath, descriptorValue, filterExpressionDescriptor.Type);
				}
				else if (descriptor is SortExpressionDescriptor sortExpressionDescriptor)
				{
					return (IDataExpression)Activator.CreateInstance(elementType, result.lambda, result.@delegate, result.memberPath, sortExpressionDescriptor.Direction);
				}

				throw new NotSupportedException("The specified descriptor type is not supported.");
			}
			catch (Exception exc) when (_logger.WriteError(exc, new { elementType.FullName, descriptor }, returnValue: true))
			{
				throw new UmbrellaException("There has been a problem creating the data expression.", exc);
			}
		}
	}
}