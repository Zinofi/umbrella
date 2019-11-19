﻿using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Umbrella.FileSystem.Abstractions;
using Umbrella.FileSystem.AzureStorage.Extensions;
using Umbrella.Utilities;
using Umbrella.Utilities.Extensions;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.FileSystem.AzureStorage
{
	public class UmbrellaAzureBlobStorageFileProvider : UmbrellaAzureBlobStorageFileProvider<UmbrellaAzureBlobStorageFileProviderOptions>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="UmbrellaAzureBlobStorageFileProvider"/> class.
		/// </summary>
		/// <param name="loggerFactory">The logger factory.</param>
		/// <param name="mimeTypeUtility">The MIME type utility.</param>
		/// <param name="genericTypeConverter">The generic type converter.</param>
		public UmbrellaAzureBlobStorageFileProvider(
			ILoggerFactory loggerFactory,
			IMimeTypeUtility mimeTypeUtility,
			IGenericTypeConverter genericTypeConverter)
			: base(loggerFactory, mimeTypeUtility, genericTypeConverter)
		{
		}
	}

	public class UmbrellaAzureBlobStorageFileProvider<TOptions> : UmbrellaFileProvider<UmbrellaAzureBlobStorageFileInfo, UmbrellaAzureBlobStorageFileProviderOptions>, IUmbrellaAzureBlobStorageFileProvider, IDisposable
		where TOptions : UmbrellaAzureBlobStorageFileProviderOptions
	{
		#region Private Static Members
		private static readonly char[] _directorySeparatorArray = new[] { '/', '\\' };
		#endregion

		#region Private Members
		private readonly SemaphoreSlim _containerCacheLock = new SemaphoreSlim(1, 1);
		#endregion

		#region Protected Properties		
		/// <summary>
		/// Gets the storage account.
		/// </summary>
		protected CloudStorageAccount StorageAccount { get; set; }

		/// <summary>
		/// Gets the BLOB client.
		/// </summary>
		protected CloudBlobClient BlobClient { get; set; }

		/// <summary>
		/// Gets the container resolution cache.
		/// </summary>
		protected ConcurrentDictionary<string, bool> ContainerResolutionCache { get; set; }
		#endregion

		#region Constructors		
		/// <summary>
		/// Initializes a new instance of the <see cref="UmbrellaAzureBlobStorageFileProvider{TOptions}"/> class.
		/// </summary>
		/// <param name="loggerFactory">The logger factory.</param>
		/// <param name="mimeTypeUtility">The MIME type utility.</param>
		/// <param name="genericTypeConverter">The generic type converter.</param>
		public UmbrellaAzureBlobStorageFileProvider(ILoggerFactory loggerFactory,
			IMimeTypeUtility mimeTypeUtility,
			IGenericTypeConverter genericTypeConverter)
			: base(loggerFactory.CreateLogger<UmbrellaAzureBlobStorageFileProvider>(), loggerFactory, mimeTypeUtility, genericTypeConverter)
		{
		}
		#endregion

		#region IUmbrellaAzureBlobStorageFileProvider Members		
		/// <summary>
		/// Clears the storage container resolution cache.
		/// </summary>
		/// <exception cref="UmbrellaFileSystemException">There has been a problem clearing the container resolution cache.</exception>
		public void ClearContainerResolutionCache()
		{
			try
			{
				ContainerResolutionCache?.Clear();
			}
			catch (Exception exc) when (Log.WriteError(exc, returnValue: true))
			{
				throw new UmbrellaFileSystemException("There has been a problem clearing the container resolution cache.", exc);
			}
		}
		#endregion

		#region Overridden Methods
		public override void InitializeOptions(IUmbrellaFileProviderOptions options)
		{
			base.InitializeOptions(options);

			StorageAccount = CloudStorageAccount.Parse(Options.StorageConnectionString);
			BlobClient = StorageAccount.CreateCloudBlobClient();

			if (Options.CacheContainerResolutions)
				ContainerResolutionCache = new ConcurrentDictionary<string, bool>();
		}

		protected override async Task<IUmbrellaFileInfo> GetFileAsync(string subpath, bool isNew, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Guard.ArgumentNotNullOrWhiteSpace(subpath, nameof(subpath));

			//TODO: Need to keep a list of incoming container names and map these to cleaned names so that we can throw an
			//exception in cases where two different incoming names map to the same cleaned name which could potentially
			//cause issues with unintentionally overwriting files that have the same name

			//TODO: Use a regex here to only allow the characters permitted by Azure which are:
			//Length: between 3 and 63 chars
			//Lowercase letters
			//Numbers
			//Hyphens
			//Can't contain consecutive hyphens
			//Begin and end with a letter or number
			StringBuilder pathBuilder = new StringBuilder(subpath)
				.Trim(' ', '\\', '/', '~', '_', ' ')
				.Replace("_", "")
				.Replace(" ", "");

			string cleanedPath = pathBuilder.ToString();

			if (Log.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
				Log.WriteDebug(new { subpath, cleanedPath });

			string[] parts = cleanedPath.Split(_directorySeparatorArray, StringSplitOptions.RemoveEmptyEntries);

			// TODO: Should extend this to allow for nested folder structures
			if (parts.Length != 2)
				throw new UmbrellaFileSystemException($"The value for {nameof(subpath)} must contain exactly 2 segments, i.e. folder name and file name. The folder name is used as the blob storage container name. The invalid value is {subpath}");

			string containerName = parts[0].ToLowerInvariant();
			string fileName = parts[1];

			if (!await CheckFileAccessAsync(containerName, fileName, cancellationToken))
				throw new UmbrellaFileAccessDeniedException(subpath);

			CloudBlobContainer container = BlobClient.GetContainerReference(containerName);

			if (ContainerResolutionCache != null && !ContainerResolutionCache.ContainsKey(containerName))
			{
				await _containerCacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);

				try
				{
					if (ContainerResolutionCache != null && !ContainerResolutionCache.ContainsKey(containerName))
					{
						await container.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);

						// The value can be anything here but we need to use ConcurrentDictioary because there isn't a ConcurrentHashSet type.
						// Could implement our own locking mechanism around a HashSet but not worth it. Maybe consider in the future or see TODO above.
						ContainerResolutionCache.TryAdd(containerName, true);
					}
				}
				finally
				{
					_containerCacheLock.Release();
				}
			}

			CloudBlockBlob blob = container.GetBlockBlobReference(fileName);

			// The call to ExistsAsync should force the properties of the blob to be populated
			if (!isNew && !await blob.ExistsAsync(cancellationToken).ConfigureAwait(false))
				return null;

			return new UmbrellaAzureBlobStorageFileInfo(FileInfoLoggerInstance, MimeTypeUtility, GenericTypeConverter, subpath, this, blob, isNew);
		}
		#endregion

		#region Protected Methods
		protected virtual Task<bool> CheckFileAccessAsync(string containerName, string fileName, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			return Task.FromResult(true);
		}
		#endregion

		#region IDisposable Support
		private bool _isDisposed = false; // To detect redundant calls

		/// <summary>
		/// Releases unmanaged and - optionally - managed resources.
		/// </summary>
		/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (!_isDisposed)
			{
				if (disposing)
				{
					_containerCacheLock.Dispose();
				}

				_isDisposed = true;
			}
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose() =>
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		#endregion
	}
}