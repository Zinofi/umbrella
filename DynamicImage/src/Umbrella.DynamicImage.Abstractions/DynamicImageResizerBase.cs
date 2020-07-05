﻿using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Umbrella.DynamicImage.Abstractions.Caching;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities;

namespace Umbrella.DynamicImage.Abstractions
{
	/// <summary>
	/// Serves as the base class for all Dynamic Image resizers.
	/// </summary>
	/// <seealso cref="Umbrella.DynamicImage.Abstractions.IDynamicImageResizer" />
	public abstract class DynamicImageResizerBase : IDynamicImageResizer
    {
		#region Protected Properties		
		/// <summary>
		/// Gets the log.
		/// </summary>
		protected ILogger Log { get; }

		/// <summary>
		/// Gets the cache.
		/// </summary>
		protected IDynamicImageCache Cache { get; }
		#endregion

		#region Constructors		
		/// <summary>
		/// Initializes a new instance of the <see cref="DynamicImageResizerBase"/> class.
		/// </summary>
		/// <param name="logger">The logger.</param>
		/// <param name="dynamicImageCache">The dynamic image cache.</param>
		public DynamicImageResizerBase(
			ILogger logger,
            IDynamicImageCache dynamicImageCache)
        {
            Log = logger;
            Cache = dynamicImageCache;
        }
		#endregion

		#region IDynamicImageResizer Members
		/// <inheritdoc />
		public async Task<DynamicImageItem> GenerateImageAsync(IUmbrellaFileProvider sourceFileProvider, DynamicImageOptions options, CancellationToken cancellationToken = default)
        {
			cancellationToken.ThrowIfCancellationRequested();
			Guard.ArgumentNotNull(sourceFileProvider, nameof(sourceFileProvider));

			try
            {
                var fileInfo = await sourceFileProvider.GetAsync(options.SourcePath, cancellationToken).ConfigureAwait(false);

                if (fileInfo == null)
                    return null;

                if (await fileInfo.ExistsAsync().ConfigureAwait(false))
                {
                    return await GenerateImageAsync(() => fileInfo.ReadAsByteArrayAsync(cancellationToken),
                        fileInfo.LastModified.Value,
                        options,
                        cancellationToken)
                        .ConfigureAwait(false);
                }

                return null;
            }
            catch (Exception exc) when (Log.WriteError(exc, new { options }, returnValue: true) && exc is DynamicImageException == false)
            {
                throw new DynamicImageException("An error has occurred during image resizing.", exc, options);
            }
        }

		/// <inheritdoc />
		public async Task<DynamicImageItem> GenerateImageAsync(Func<Task<byte[]>> sourceBytesProvider, DateTimeOffset sourceLastModified, DynamicImageOptions options, CancellationToken cancellationToken = default)
        {
			cancellationToken.ThrowIfCancellationRequested();
			Guard.ArgumentNotNull(sourceBytesProvider, nameof(sourceBytesProvider));

			try
            {
                if (Log.IsEnabled(LogLevel.Debug))
                    Log.WriteDebug(new { sourceLastModified, options }, "Started generating the image based on the recoreded state.");

                //Check if the image exists in the cache
                DynamicImageItem dynamicImage = await Cache.GetAsync(options, sourceLastModified, options.Format.ToFileExtensionString()).ConfigureAwait(false);

                if (Log.IsEnabled(LogLevel.Debug))
                    Log.WriteDebug(new { options, sourceLastModified, options.Format }, "Searched the image cache using the supplied state.");

                if (dynamicImage != null)
                {
                    if (Log.IsEnabled(LogLevel.Debug))
                        Log.WriteDebug(new { dynamicImage.ImageOptions, dynamicImage.LastModified }, "Image found in cache.");

                    return dynamicImage;
                }

                //Item cannot be found in the cache - build a new image
                byte[] originalBytes = await sourceBytesProvider().ConfigureAwait(false);

                //Need to get the newly resized image and assign it to the instance
                dynamicImage = new DynamicImageItem
                {
                    ImageOptions = options,
                    LastModified = DateTimeOffset.UtcNow
                };

                dynamicImage.Content = ResizeImage(originalBytes, options);

                //Now add to the cache
                await Cache.AddAsync(dynamicImage).ConfigureAwait(false);

                return dynamicImage;
            }
            catch (Exception exc) when (Log.WriteError(exc, new { sourceLastModified, options }, returnValue: true) && exc is DynamicImageException == false)
            {
                throw new DynamicImageException("An error has occurred during image resizing.", exc, options);
            }
        }

		/// <inheritdoc />
		public abstract bool IsImage(byte[] bytes);

		/// <inheritdoc />
		public byte[] ResizeImage(byte[] originalImage, DynamicImageOptions options)
            => ResizeImage(originalImage, options.Width, options.Height, options.ResizeMode, options.Format);

		/// <inheritdoc />
		public abstract byte[] ResizeImage(byte[] originalImage, int width, int height, DynamicResizeMode resizeMode, DynamicImageFormat format);
		#endregion

		#region Protected Methods		
		/// <summary>
		/// Gets the destination dimensions.
		/// </summary>
		/// <param name="originalWidth">Width of the original.</param>
		/// <param name="originalHeight">Height of the original.</param>
		/// <param name="targetWidth">Width of the target.</param>
		/// <param name="targetHeight">Height of the target.</param>
		/// <param name="mode">The mode.</param>
		/// <returns>A tuple containing the destination dimensions.</returns>
		protected (int width, int height, int offsetX, int offsetY, int cropWidth, int cropHeight) GetDestinationDimensions(int originalWidth, int originalHeight, int targetWidth, int targetHeight, DynamicResizeMode mode)
        {
            int? requestedWidth = null;
            int? requestedHeight = null;

            int offsetX = 0;
            int offsetY = 0;
            int cropWidth = originalWidth;
            int cropHeight = originalHeight;

            switch (mode)
            {
                case DynamicResizeMode.UseWidth:
                    requestedWidth = targetWidth < originalWidth ? targetWidth : originalWidth;
                    break;
                case DynamicResizeMode.UseHeight:
                    requestedHeight = targetHeight < originalHeight ? targetHeight : originalHeight;
                    break;
                case DynamicResizeMode.Fill:
                    requestedWidth = targetWidth;
                    requestedHeight = targetHeight;
                    break;
                case DynamicResizeMode.Uniform:
                    // If both requested dimensions are greater than source image, we don't need to do any resizing.
                    if (targetWidth < originalWidth || targetHeight < originalHeight)
                    {
						// Calculate requested width and height so as not to squash image.

						// First, resize based on max width and check whether resized height will be more than target height.
						var (_, tempHeight) = CalculateOutputDimensions(originalWidth, originalHeight, targetWidth, null);

                        if (tempHeight > targetHeight)
                        {
                            // If so, we need to resize based on max height instead.
                            requestedHeight = targetHeight;
                        }
                        else
                        {
                            // If not, we have our max dimension.
                            requestedWidth = targetWidth;
                        }
                    }
                    else
                    {
                        requestedWidth = originalWidth;
                        requestedHeight = originalHeight;
                    }
                    break;
                case DynamicResizeMode.UniformFill:
                    // Resize based on width first. If this means that height is less than target height, we resize based on height.
                    if (targetWidth < originalWidth || targetHeight < originalHeight)
                    {
						// Calculate requested width and height so as not to squash image.

						// First, resize based on width and check whether resized height will be more than max height.
						var (_, tempHeight) = CalculateOutputDimensions(originalWidth, originalHeight, targetWidth, null);

						if (tempHeight < targetHeight)
                        {
                            // If so, we need to resize based on max height instead.
                            requestedHeight = targetHeight;
							int tempWidth;
							(tempWidth, _) = CalculateOutputDimensions(originalWidth, originalHeight, null, targetHeight);

							// Then crop width and calculate offset.
							requestedWidth = targetWidth;
                            cropWidth = (int)(targetWidth / (float)tempWidth * originalWidth);
                            offsetX = (originalWidth - cropWidth) / 2;
                        }
                        else
                        {
                            // If not, we have our max dimension.
                            requestedWidth = targetWidth;

                            // Then crop height and calculate offset.
                            requestedHeight = targetHeight;
                            cropHeight = (int)((targetHeight / (float)tempHeight) * originalHeight);
                            offsetY = (originalHeight - cropHeight) / 2;
                        }
                    }
                    else
                    {
                        requestedWidth = originalWidth;
                        requestedHeight = originalHeight;
                    }
                    break;
            }

            var (width, height) = CalculateOutputDimensions(originalWidth, originalHeight, requestedWidth, requestedHeight);

            return (width, height, offsetX, offsetY, cropWidth, cropHeight);
        }
        #endregion

        #region Private Methods
        private (int width, int height) CalculateOutputDimensions(int nInputWidth, int nInputHeight, int? nRequestedWidth, int? nRequestedHeight)
        {
            // both width and height are specified - squash image
            if (nRequestedWidth != null && nRequestedHeight != null)
            {
                return (nRequestedWidth.Value, nRequestedHeight.Value);
            }
            else if (nRequestedWidth != null) // calculate height to keep aspect ratio
            {
                double aspectRatio = (double)nInputWidth / nInputHeight;

                return (nRequestedWidth.Value, (int)(nRequestedWidth.Value / aspectRatio));
            }
            else if (nRequestedHeight != null) // calculate width to keep aspect ratio
            {
                double aspectRatio = (double)nInputHeight / nInputWidth;

                return ((int)(nRequestedHeight.Value / aspectRatio), nRequestedHeight.Value);
            }
            else
            {
                throw new Exception("Width or height, or both, must be specified");
            }
        } 
        #endregion
    }
}