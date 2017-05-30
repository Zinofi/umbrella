﻿using SoundInTheory.DynamicImage;
using SoundInTheory.DynamicImage.Filters;
using SoundInTheory.DynamicImage.Fluent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using Umbrella.Utilities.Hosting;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Extensions;
using Umbrella.DynamicImage.Abstractions;
using UDynamicImageFormat = Umbrella.DynamicImage.Abstractions.DynamicImageFormat;
using SDynamicImageFormat = SoundInTheory.DynamicImage.DynamicImageFormat;
using UDynamicImageException = Umbrella.DynamicImage.Abstractions.DynamicImageException;
using System.Threading.Tasks;
using Umbrella.FileSystem.Abstractions;
using System.Threading;

namespace Umbrella.DynamicImage.SoundInTheory
{
    public class DynamicImageResizer : IDynamicImageResizer
    {
        #region Private Members
        private readonly ILogger<DynamicImageResizer> m_Logger;
        private readonly IDynamicImageCache m_DynamicImageCache;
        #endregion

        #region Constructors
        public DynamicImageResizer(ILogger<DynamicImageResizer> logger,
            IDynamicImageCache dynamicImageCache)
        {
            m_Logger = logger;
            m_DynamicImageCache = dynamicImageCache;
        }
        #endregion

        #region IDynamicImageResizer Members
        public async Task<DynamicImageItem> GenerateImageAsync(IUmbrellaFileProvider sourceFileProvider, DynamicImageOptions options, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                var fileInfo = await sourceFileProvider.GetAsync(options.SourcePath, cancellationToken).ConfigureAwait(false);

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
            catch (Exception exc) when (m_Logger.WriteError(exc, new { options }, returnValue: true))
            {
                throw new UDynamicImageException("An error has occurred during image resizing.", exc, options);
            }
        }

        public async Task<DynamicImageItem> GenerateImageAsync(Func<Task<byte[]>> sourceBytesProvider, DateTimeOffset sourceLastModified, DynamicImageOptions options, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                if (m_Logger.IsEnabled(LogLevel.Debug))
                    m_Logger.WriteDebug(new { sourceLastModified, options }, "Started generating the image based on the recoreded state.");

                //Check if the image exists in the cache
                DynamicImageItem dynamicImage = await m_DynamicImageCache.GetAsync(options, sourceLastModified, options.Format.ToFileExtensionString()).ConfigureAwait(false);

                if (m_Logger.IsEnabled(LogLevel.Debug))
                    m_Logger.WriteDebug(new { options, sourceLastModified, options.Format }, "Searched the image cache using the supplied state.");

                if (dynamicImage != null)
                {
                    if (m_Logger.IsEnabled(LogLevel.Debug))
                        m_Logger.WriteDebug(new { dynamicImage.ImageOptions, dynamicImage.LastModified }, "Image found in cache.");

                    return dynamicImage;
                }

                //Item cannot be found in the cache - build a new image
                byte[] bytes = await sourceBytesProvider().ConfigureAwait(false);

                ImageLayerBuilder imageLayerBuilder = LayerBuilder.Image.SourceBytes(bytes);

                ResizeMode dynamicResizeMode = GetResizeMode(options.ResizeMode);
                SDynamicImageFormat dynamicImageFormat = GetImageFormat(options.Format);

                CompositionBuilder builder = new CompositionBuilder()
                    .WithLayer(imageLayerBuilder.WithFilter(FilterBuilder.Resize.To(options.Width, options.Height, dynamicResizeMode)))
                    .ImageFormat(dynamicImageFormat);

                GeneratedImage image = builder.Composition.GenerateImage();

                if (m_Logger.IsEnabled(LogLevel.Debug))
                    m_Logger.WriteDebug(new { image.Properties.IsImagePresent }, "Successfully generated the target image.");

                //Need to get the newly resized image and assign it to the instance
                dynamicImage = new DynamicImageItem
                {
                    ImageOptions = options,
                    LastModified = DateTimeOffset.UtcNow
                };

                dynamicImage.SetContent(ConvertBitmapSourceToByteArray(image.Image, options.Format));

                //Now add to the cache
                await m_DynamicImageCache.AddAsync(dynamicImage).ConfigureAwait(false);

                return dynamicImage;
            }
            catch (Exception exc) when (m_Logger.WriteError(exc, new { sourceLastModified, options }, returnValue: true))
            {
                throw new UDynamicImageException("An error has occurred during image resizing.", exc, options);
            }
        }
        #endregion

        #region Private Methods
        private ResizeMode GetResizeMode(DynamicResizeMode mode)
        {
            switch (mode)
            {
                case DynamicResizeMode.Fill:
                    return ResizeMode.Fill;
                case DynamicResizeMode.Uniform:
                    return ResizeMode.Uniform;
                case DynamicResizeMode.UniformFill:
                    return ResizeMode.UniformFill;
                case DynamicResizeMode.UseHeight:
                    return ResizeMode.UseHeight;
                case DynamicResizeMode.UseWidth:
                    return ResizeMode.UseWidth;
                default:
                    return default(ResizeMode);
            }
        }

        private SDynamicImageFormat GetImageFormat(UDynamicImageFormat format)
        {
            switch (format)
            {
                case UDynamicImageFormat.Bmp:
                    return SDynamicImageFormat.Bmp;
                case UDynamicImageFormat.Gif:
                    return SDynamicImageFormat.Gif;
                case UDynamicImageFormat.Jpeg:
                    return SDynamicImageFormat.Jpeg;
                case UDynamicImageFormat.Png:
                    return SDynamicImageFormat.Png;
                default:
                    return default(SDynamicImageFormat);
            }
        }

        private byte[] ConvertBitmapSourceToByteArray(BitmapSource source, UDynamicImageFormat imageFormat)
        {
            BitmapFrame frame = BitmapFrame.Create(source);

            BitmapEncoder encoder = null;

            switch (imageFormat)
            {
                case UDynamicImageFormat.Bmp:
                    encoder = new BmpBitmapEncoder();
                    break;
                case UDynamicImageFormat.Gif:
                    encoder = new GifBitmapEncoder();
                    break;
                case UDynamicImageFormat.Jpeg:
                    encoder = new JpegBitmapEncoder();
                    break;
                case UDynamicImageFormat.Png:
                    encoder = new PngBitmapEncoder();
                    break;
            }

            encoder.Frames.Add(frame);

            using (MemoryStream stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }
        #endregion
    }
}