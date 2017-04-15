﻿using System;

namespace Umbrella.DynamicImage.Abstractions
{
    public struct DynamicImageOptions
    {
        public static readonly DynamicImageOptions Empty = new DynamicImageOptions();

        public int Width { get; set; }
        public int Height { get; set; }
        public DynamicResizeMode ResizeMode { get; set; }
        public DynamicImageFormat Format { get; set; }
        /// <summary>
        /// This represents the source path and could be a physical path or URL.
        /// </summary>
        public string SourcePath { get; set; }

        public DynamicImageOptions(string path, int width, int height, DynamicResizeMode resizeMode, DynamicImageFormat format)
        {
            SourcePath = path;
            Width = width;
            Height = height;
            ResizeMode = resizeMode;
            Format = format;
        }

        public override bool Equals(object obj)
        {
            DynamicImageOptions compareOptions = (DynamicImageOptions)obj;

            return SourcePath.Equals(compareOptions.SourcePath, StringComparison.OrdinalIgnoreCase)
                && Width == compareOptions.Width
                && Height == compareOptions.Height
                && ResizeMode == compareOptions.ResizeMode
                && Format == compareOptions.Format;
        }

        public override int GetHashCode() => ToString().GetHashCode();

        public override string ToString()
            => $"{nameof(SourcePath)}: {SourcePath}, {nameof(Width)}: {Width}, {nameof(Height)}: {Height}, {nameof(ResizeMode)}: {ResizeMode}, {nameof(Format)}: {Format}";

        public static bool operator ==(DynamicImageOptions item1, DynamicImageOptions item2) => item1.Equals(item2);
        public static bool operator !=(DynamicImageOptions item1, DynamicImageOptions item2) => !item1.Equals(item2);
        public static bool IsEmpty(DynamicImageOptions options) => options == Empty;

        public static explicit operator DynamicImageMapping(DynamicImageOptions options)
        {
            return new DynamicImageMapping
            {
                Format = options.Format,
                Height = options.Height,
                ResizeMode = options.ResizeMode,
                Width = options.Width
            };
        }
    }
}