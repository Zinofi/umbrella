﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Umbrella.FileSystem.Abstractions;

namespace Umbrella.DynamicImage.Abstractions
{
	/// <summary>
	/// A utility for resizing images.
	/// </summary>
	public interface IDynamicImageResizer
    {
		/// <summary>
		/// Determines whether the specified bytes is an image.
		/// </summary>
		/// <param name="bytes">The bytes.</param>
		/// <returns>
		///   <c>true</c> if the specified bytes is an image; otherwise, <c>false</c>.
		/// </returns>
		bool IsImage(byte[] bytes);

		/// <summary>
		/// Generates the image using the specified options or finds its existing version in the cache.
		/// </summary>
		/// <param name="sourceFileProvider">The source file provider.</param>
		/// <param name="options">The options.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The resized image.</returns>
		Task<DynamicImageItem?> GenerateImageAsync(IUmbrellaFileProvider sourceFileProvider, DynamicImageOptions options, CancellationToken cancellationToken = default);

		/// <summary>
		/// Generates the image using the specified options or finds its existing version in the cache.
		/// </summary>
		/// <param name="sourceBytesProvider">The source bytes provider.</param>
		/// <param name="sourceLastModified">The source last modified.</param>
		/// <param name="options">The options.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>Resized image.</returns>
		Task<DynamicImageItem> GenerateImageAsync(Func<Task<byte[]>> sourceBytesProvider, DateTimeOffset sourceLastModified, DynamicImageOptions options, CancellationToken cancellationToken = default);

		/// <summary>
		/// Resizes the image using the specified options.
		/// </summary>
		/// <param name="originalImage">The original image.</param>
		/// <param name="options">The options.</param>
		/// <returns>The resized image.</returns>
		byte[] ResizeImage(byte[] originalImage, DynamicImageOptions options);

		/// <summary>
		/// Resizes the image.
		/// </summary>
		/// <param name="originalImage">The original image.</param>
		/// <param name="width">The width.</param>
		/// <param name="height">The height.</param>
		/// <param name="resizeMode">The resize mode.</param>
		/// <param name="format">The format.</param>
		/// <returns>The resized image.</returns>
		byte[] ResizeImage(byte[] originalImage, int width, int height, DynamicResizeMode resizeMode, DynamicImageFormat format);
    }
}