﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Umbrella.Utilities.Constants;

namespace Umbrella.Utilities.Extensions
{
	// TODO: Rework some of this internally to take advantage of the new Span stuff.

	/// <summary>
	/// Extension methods that operation on <see langword="string"/> instances.
	/// </summary>
	public static class StringExtensions
	{
		private const string HtmlTagPattern = @"<.*?>";
		private const string EllipsisPattern = @"[\.]+$";

		private static readonly Regex s_HtmlTagPatternRegex = new Regex(HtmlTagPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		private static readonly Regex s_EllipsisPatternRegex = new Regex(EllipsisPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

		public static string Truncate(this string text, int maxLength)
		{
			// Ensure we strip out HTML tags
			if (!string.IsNullOrEmpty(text))
				text = text.StripHtml();

			if (text.Length < maxLength)
				return text;

			return AppendEllipsis(text.Substring(0, maxLength - 3));
		}

		public static string TruncateAtWord(this string value, int length)
		{
			//Ensure we strip out HTML tags
			if (!string.IsNullOrEmpty(value))
				value = value.StripHtml();

			if (value == null || value.Length < length || value.IndexOf(" ", length) == -1)
				return value;

			return AppendEllipsis(value.Substring(0, value.IndexOf(" ", length)));
		}

		public static string StripNbsp(this string value) => !string.IsNullOrEmpty(value) ? value.Replace("&nbsp;", "") : null;

		public static bool IsValidLength(this string value, int minLength, int maxLength, bool allowNull = true)
		{
			if (string.IsNullOrWhiteSpace(value))
				return allowNull;

			return value.Length >= minLength && value.Length <= maxLength;
		}

		public static string StripHtml(this string value)
		{
			if (string.IsNullOrEmpty(value))
				return value;

			return s_HtmlTagPatternRegex.Replace(value, string.Empty);
		}

		public static string ToCamelCase(this string value) => ToCamelCaseInternal(value, false);
		public static string ToCamelCaseInvariant(this string value) => ToCamelCaseInternal(value, true);

		// TODO: Internally reimplement using local functions and Span<char>
		public static string ToCamelCaseInternal(string value, bool useInvariantCulture)
		{
			if (string.IsNullOrWhiteSpace(value))
				return value;

			Func<string, string> stringLower;
			Func<char, char> charLower;

			if (useInvariantCulture)
			{
				stringLower = x => x.ToLowerInvariant();
				charLower = char.ToLowerInvariant;
			}
			else
			{
				stringLower = x => x.ToLower();
				charLower = char.ToLower;
			}

			if (value.Length == 1)
				return stringLower(value);

			// If 1st char is already in lowercase, return the value untouched
			if (char.IsLower(value[0]))
				return value;

			char[] buffer = new char[value.Length];

			bool stop = false;

			for (int i = 0; i < value.Length; i++)
			{
				if (!stop)
				{
					if (char.IsUpper(value[i]))
					{
						if (i > 1 && char.IsLower(value[i - 1]))
						{
							stop = true;
						}
						else
						{
							buffer[i] = charLower(value[i]);
							continue;
						}
					}
					else if (i > 1)
					{
						// Encountered first lowercase char
						// Check previous char and see if that was uppercase before we made it lowercase
						char previous = value[i - 1];
						if (char.IsUpper(previous))
						{
							buffer[i - 1] = previous;
							stop = true;
						}
					}
				}

				buffer[i] = value[i];
			}

			return new string(buffer);
		}

		/// <summary>
		/// Appends an ellipsis to the specified <paramref name="value"/>.
		/// </summary>
		/// <param name="value">The value.</param>
		/// <returns>The value with an ellipsis appended.</returns>
		public static string AppendEllipsis(string value)
		{
			if (string.IsNullOrEmpty(value))
				return value;

			value += "...";

			return s_EllipsisPatternRegex.Replace(value, "...");
		}

		public static string ConvertHtmlBrTagsToLineBreaks(this string value)
		{
			if (string.IsNullOrEmpty(value))
				return value;

			var sb = new StringBuilder(value);
			sb.ConvertHtmlBrTagsToReplacement("\n");

			return sb.ToString();
		}

		public static string ToSnakeCase(this string value, bool lowerCase = true, bool removeWhiteSpace = true, CultureInfo cultureInfo = null)
			=> ToSnakeCaseInternal(value, lowerCase, removeWhiteSpace, cultureInfo ?? CultureInfo.CurrentCulture);

		public static string ToSnakeCaseInvariant(this string value, bool lowerCase = true, bool removeWhiteSpace = true)
		=> ToSnakeCaseInternal(value, lowerCase, removeWhiteSpace, CultureInfo.InvariantCulture);

		private static string ToSnakeCaseInternal(string value, bool lowerCase, bool removeWhiteSpace, CultureInfo cultureInfo)
		{
			if (string.IsNullOrWhiteSpace(value))
				return value;

			if (value.Length == 1)
				return lowerCase ? value.ToLower(cultureInfo) : value;

			var buffer = new List<char>(value.Length)
			{
				lowerCase ? char.ToLower(value[0], cultureInfo) : value[0]
			};

			for (int i = 1; i < value.Length; i++)
			{
				char current = value[i];

				if (removeWhiteSpace && char.IsWhiteSpace(current))
					continue;

				if (char.IsUpper(value[i]))
				{
					if (lowerCase)
						current = char.ToLower(current, cultureInfo);

					buffer.Add('_');
				}

				buffer.Add(current);
			}

			return new string(buffer.ToArray());
		}

		/// <summary>
		/// Returns a value indicating whether a specified substring occurs within this string.
		/// </summary>
		/// <param name="target">The string to check.</param>
		/// <param name="value">The string to seek.</param>
		/// <param name="comparisonType">The type of <see cref="StringComparison"/> to use to locate the <paramref name="value"/> in the <paramref name="target"/>.</param>
		/// <returns>true if the value parameter occurs within this string, otherwise, false.</returns>
		public static bool Contains(this string target, string value, StringComparison comparisonType)
		{
			if (target == null || value == null)
				return false;

			return target.IndexOf(value, comparisonType) >= 0;
		}

		public static T ToEnum<T>(this string value) where T : struct, Enum => value.ToEnum(default(T));

		public static T ToEnum<T>(this string value, T defaultValue) where T : struct, Enum
		{
			if (Enum.TryParse(value, true, out T result))
				return result;

			return defaultValue;
		}

		public static string TrimToLower(this string value, CultureInfo culture = null)
		{
			if (string.IsNullOrEmpty(value))
				return value;

			ReadOnlySpan<char> span = value.AsSpan().Trim();
			Span<char> lowerSpan = span.Length <= StackAllocConstants.MaxCharSize ? stackalloc char[span.Length] : new char[span.Length];
			span.ToLowerSlim(lowerSpan, culture);

			return lowerSpan.ToString();
		}

		public static string TrimToLowerInvariant(this string value)
			=> TrimToLower(value, CultureInfo.InvariantCulture);

		public static string TrimToUpper(this string value, CultureInfo culture = null)
		{
			if (string.IsNullOrEmpty(value))
				return value;

			ReadOnlySpan<char> span = value.AsSpan().Trim();
			Span<char> upperSpan = span.Length <= StackAllocConstants.MaxCharSize ? stackalloc char[span.Length] : new char[span.Length];
			span.ToUpperSlim(upperSpan, culture);

			return upperSpan.ToString();
		}

		public static string TrimToUpperInvariant(this string value)
			=> TrimToUpper(value, CultureInfo.InvariantCulture);

		/// <summary>
		/// Trims the specified value in a null-safe manner.
		/// </summary>
		/// <param name="value">The value.</param>
		/// <returns>The trimmed value or <see langword="null"/>.</returns>
		public static string TrimNull(this string value) => value?.Trim();

		/// <summary>
		/// Attempts to convert the name of a person into some kind of normalized format
		/// where the name is not presently in a reasonable format, e.g. riCHARd would be better transformed
		/// into Richard. This method only deals with the simplest of cases currently.
		/// </summary>
		/// <param name="name">The name.</param>
		/// <returns>The normalized name</returns>
		public static string ToPersonNameCase(this string name)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				return name;
			}

			Span<char> span = name.Length <= StackAllocConstants.MaxCharSize ? stackalloc char[name.Length] : new char[name.Length];

			for (int i = 0; i < name.Length; i++)
			{
				char currentChar = name[i];

				// First letter should always be uppercase
				if (i == 0)
				{
					span[i] = char.ToUpper(currentChar);
					continue;
				}

				span[i] = char.ToLower(currentChar);

				if (currentChar == '-' && i < name.Length - 1)
				{
					// Ensure the next letter is in uppercase as we are most likely dealing with a double barrelled name
					span[i + 1] = char.ToUpper(name[i + 1]);
					i++;
					continue;
				}
			}

			return span.ToString();
		}

		/// <summary>
		/// Converts to the specified <paramref name="value"/> to title case using the rules of the specified
		/// <paramref name="cultureInfo"/>. If <paramref name="cultureInfo"/> is <see langword="null"/>, it defaults
		/// to <see cref="CultureInfo.CurrentCulture"/>.
		/// </summary>
		/// <param name="value">The value.</param>
		/// <param name="cultureInfo">The optional culture information. Defaults to <see cref="CultureInfo.CurrentCulture"/>.</param>
		/// <returns>The <paramref name="value"/> converted to title case.</returns>
		public static string ToTitleCase(this string value, CultureInfo cultureInfo = null)
			=> string.IsNullOrWhiteSpace(value) ? value : (cultureInfo ?? CultureInfo.CurrentCulture).TextInfo.ToTitleCase(value);

		/// <summary>
		/// Converts to the specified <paramref name="value"/> to title case using the rules of the invariant culture.
		/// </summary>
		/// <param name="value">The value.</param>
		/// <returns>The <paramref name="value"/> converted to title case.</returns>
		public static string ToTitleCaseInvariant(this string value)
			=> ToTitleCase(value, CultureInfo.InvariantCulture);
	}
}