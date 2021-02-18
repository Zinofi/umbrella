﻿using System;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.Xamarin.Converters;
using Xamarin.Forms;

namespace Umbrella.Xamarin.MarkupExtensions
{
	/// <summary>
	/// A markup extensions used to generate image URLs for use with the Dynamic Image infrastructure.
	/// </summary>
	/// <seealso cref="UmbrellaResponsiveImageExtension" />
	public class UmbrellaDynamicImageExtension : UmbrellaResponsiveImageExtension
	{
		/// <summary>
		/// The width request property.
		/// </summary>
		public static BindableProperty WidthRequestProperty = BindableProperty.Create(nameof(WidthRequest), typeof(int), typeof(UmbrellaDynamicImageExtension), 1);

		/// <summary>
		/// The height request property.
		/// </summary>
		public static BindableProperty HeightRequestProperty = BindableProperty.Create(nameof(HeightRequest), typeof(int), typeof(UmbrellaDynamicImageExtension), 1);

		/// <summary>
		/// Gets or sets the dynamic image path prefix. Defaults to <see cref="DynamicImageConstants.DefaultPathPrefix"/>.
		/// </summary>
		public string DynamicImagePathPrefix { get; set; } = DynamicImageConstants.DefaultPathPrefix;

		/// <summary>
		/// Gets or sets the width request in pixels. Defaults to 1.
		/// </summary>
		public int WidthRequest
		{
			get => (int)GetValue(WidthRequestProperty);
			set => SetValue(WidthRequestProperty, value);
		}

		/// <summary>
		/// Gets or sets the height request in pixels. Defaults to 1.
		/// </summary>
		public int HeightRequest
		{
			get => (int)GetValue(HeightRequestProperty);
			set => SetValue(HeightRequestProperty, value);
		}

		/// <summary>
		/// Gets or sets the resize mode. Defaults to <see cref="DynamicResizeMode.UniformFill"/>.
		/// </summary>
		/// <remarks>
		/// For more information on how these resize modes work, please refer to the <see cref="DynamicResizeMode"/> code documentation.
		/// </remarks>
		public DynamicResizeMode ResizeMode { get; set; } = DynamicResizeMode.UniformFill;

		/// <summary>
		/// Gets or sets the image format. Defaults to <see cref="DynamicImageFormat.Jpeg"/>.
		/// </summary>
		public DynamicImageFormat ImageFormat { get; set; } = DynamicImageFormat.Jpeg;

		/// <summary>
		/// Gets or sets the prefix to be stripped from the <see cref="UmbrellaResponsiveImageExtension.Path"/>.
		/// </summary>
		public string? StripPrefix { get; set; }

		/// <inheritdoc />
		public override BindingBase ProvideValue(IServiceProvider serviceProvider)
		{
			var converter = new UmbrellaDynamicImageConverter(MaxPixelDensity, Converter, DynamicImagePathPrefix, WidthRequest, HeightRequest, ResizeMode, ImageFormat, StripPrefix);

			return new Binding(Path, Mode, converter, ConverterParameter, StringFormat, Source);
		}
	}
}