﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using Umbrella.Utilities.Extensions;

namespace Umbrella.DataAccess.Abstractions
{
	public class IncludeMap<TEntity> : IEnumerable<Expression<Func<TEntity, object>>>
	{
		#region Public Properties
		public HashSet<Expression<Func<TEntity, object>>> Includes { get; } = new HashSet<Expression<Func<TEntity, object>>>();
		public HashSet<string> PropertyNames { get; } = new HashSet<string>();
		#endregion

		#region Constructors
		public IncludeMap(params Expression<Func<TEntity, object>>[] paths)
		{
			foreach (var path in paths)
			{
				Includes.Add(path);

				// TODO: Consider changing PropertyNames to PropertyPaths here so the paths are effectively generated once
				// as IncludeMap is static most of the time.
				string propertyName = path.AsPath();

				if (!string.IsNullOrEmpty(propertyName))
					PropertyNames.Add(propertyName);
			}
		}
		#endregion

		#region Overridden Methods
		public override string ToString() => string.Join(", ", PropertyNames);
		#endregion

		#region IEnumerable Members
		public IEnumerator<Expression<Func<TEntity, object>>> GetEnumerator() => Includes.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => Includes.GetEnumerator();
		#endregion
	}
}