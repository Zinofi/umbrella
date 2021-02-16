﻿using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Umbrella.DataAnnotations
{
	/// <summary>
	/// An extensions of the <see cref="CompareAttribute"/> with a fix to ensure that the <see cref="ValidationResult"/> that is
	/// returned on validation failure contains the member name that the attribute targets.
	/// </summary>
	/// <seealso cref="System.ComponentModel.DataAnnotations.CompareAttribute" />
	public class UmbrellaCompareAttribute : CompareAttribute
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="UmbrellaCompareAttribute"/> class.
		/// </summary>
		/// <param name="otherProperty">The property to compare with the current property.</param>
		public UmbrellaCompareAttribute(string otherProperty)
			: base(otherProperty)
		{
		}

		/// <inheritdoc />
		protected override ValidationResult IsValid(object value, ValidationContext validationContext)
		{
			ValidationResult result = base.IsValid(value, validationContext);

			if (result == ValidationResult.Success)
				return result;

			if (!result.MemberNames.Any() && !string.IsNullOrEmpty(validationContext.MemberName))
				result = new ValidationResult(result.ErrorMessage, new[] { validationContext.MemberName });

			return result;
		}
	}
}