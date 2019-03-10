﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Umbrella.Utilities.Comparers
{
	public class GenericEqualityComparer<TObject> : GenericEqualityComparer<TObject, TObject>
	{
		public GenericEqualityComparer(
			Func<TObject, TObject> propertySelector,
			Func<TObject, TObject, bool> customComparer = null)
			: base(propertySelector, customComparer)
		{
		}
	}

	/// <summary>
	/// A generic comparer to allow for determining equality between 2 object instances
	/// of the same type.
	/// </summary>
	/// <typeparam name="TObject">The type of the object instances to compare.</typeparam>
	/// <typeparam name="TProperty">The type of the property used for comparison.</typeparam>
	public class GenericEqualityComparer<TObject, TProperty> : EqualityComparer<TObject>
    {
        private readonly Func<TObject, TProperty> m_PropertySelector;
        private readonly Func<TObject, TObject, bool> m_CustomComparer;

        /// <summary>
        /// Create a new instance of the comparer using the specified delegate to select the value used to check
        /// for object instance equality. If a custom comparer is specified then that will be used instead with the property
        /// selector only being used to determine the HashCode for an object instance.
        /// <para>
        /// If a custom comparer is not specified then equality will be determined by trying to get values from each object using the <paramref name="propertySelector"/> and comparing those.
        /// </para>
        /// </summary>
        /// <param name="propertySelector">The property selector delegate.</param>
        /// <param name="customComparer">An optional delegate for custom comparison instead of using the <paramref name="propertySelector"/> if possible.</param>
        public GenericEqualityComparer(Func<TObject, TProperty> propertySelector, Func<TObject, TObject, bool> customComparer = null)
        {
            Guard.ArgumentNotNull(propertySelector, nameof(propertySelector));

            m_PropertySelector = propertySelector;
            m_CustomComparer = customComparer;
        }

        /// <summary>
        /// Determines if the 2 provided object instances are equal.
        /// </summary>
        /// <param name="x">The first object instance.</param>
        /// <param name="y">The second object instance.</param>
        /// <returns>Whether or not the 2 object instances are equal.</returns>
        public override bool Equals(TObject x, TObject y)
        {
            // Check the objects for null combinations first.
            if (x == null && y == null)
                return true;

            if (x == null)
                return false;

            if (y == null)
                return false;

            if (m_CustomComparer != null)
                return m_CustomComparer(x, y);

            TProperty xProperty = m_PropertySelector(x);
            TProperty yProperty = m_PropertySelector(y);

            if (xProperty == null && yProperty == null)
                return true;

            if (xProperty == null)
                return false;

            if (yProperty == null)
                return false;

            // Check if we can compare the 2 values using the more efficient IEquatable interface to avoid any unnecessary boxing.
            if (xProperty is IEquatable<TProperty> xPropertyEquatable && yProperty is IEquatable<TProperty> yPropertyEquatable)
                return xPropertyEquatable.Equals(yPropertyEquatable);

            // Use the default mechanism as a last resort.
            return xProperty.Equals(yProperty);
        }

        /// <summary>
        /// Computes the hash code for the specified object instance. Internally this calls GetHashCode on the value that
        /// is returned by the propertySelector delegate supplied in the constructor.
        /// </summary>
        /// <param name="obj">The object instance to compute the hash code for.</param>
        /// <returns>The hash code</returns>
        public override int GetHashCode(TObject obj) => m_PropertySelector(obj)?.GetHashCode() ?? 0;
    }
}