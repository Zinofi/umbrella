﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Umbrella.FileSystem.Abstractions;
using Umbrella.FileSystem.AzureStorage;
using Umbrella.FileSystem.Disk;
using Umbrella.Utilities;
using Umbrella.Utilities.Compilation;
using Umbrella.Utilities.Integration.NewtonsoftJson;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;
using Xunit;

namespace Umbrella.FileSystem.Test
{
	public class UmbrellaFileProviderTest
	{
#if AZUREDEVOPS
        private static readonly string StorageConnectionString = Environment.GetEnvironmentVariable("StorageConnectionString");
#else
		private const string StorageConnectionString = "UseDevelopmentStorage=true";
#endif

		private const string TestFileName = "aspnet-mvc-logo.png";
		private static string s_BaseDirectory;

		private static string BaseDirectory
		{
			get
			{
				if (string.IsNullOrEmpty(s_BaseDirectory))
				{
					string baseDirectory = AppContext.BaseDirectory.ToLowerInvariant();
					int indexToEndAt = baseDirectory.IndexOf($@"\bin\{DebugUtility.BuildConfiguration}\netcoreapp3.0");
					s_BaseDirectory = baseDirectory.Remove(indexToEndAt, baseDirectory.Length - indexToEndAt);
				}

				return s_BaseDirectory;
			}
		}

		public static List<IUmbrellaFileProvider> Providers = new List<IUmbrellaFileProvider>
		{
			CreateAzureBlobFileProvider(),
			CreateDiskFileProvider()
		};

		public static List<string> PathsToTest = new List<string>
		{
			$"~/images/{TestFileName}",
			$"/images/{TestFileName}",
			$"/_ images/{TestFileName}",
			$@"\images\{TestFileName}",
			$@"\images/{TestFileName}"
		};

		public static List<object[]> ProvidersMemberData = Providers.Select(x => new object[] { x }).ToList();
		public static List<object[]> PathsToTestMemberData = PathsToTest.Select(x => new object[] { x }).ToList();

		public static List<object[]> ProvidersAndPathsMemberData = new List<object[]>();

		static UmbrellaFileProviderTest()
		{
			foreach (var provider in Providers)
			{
				foreach (string path in PathsToTest)
				{
					ProvidersAndPathsMemberData.Add(new object[] { provider, path });
				}
			}

			UmbrellaJsonIntegration.Initialize();
		}

		[Theory]
		[MemberData(nameof(ProvidersAndPathsMemberData))]
		public async Task CreateAsync_FromPath(IUmbrellaFileProvider provider, string path)
		{
			IUmbrellaFileInfo file = await provider.CreateAsync(path);

			CheckPOCOFileType(provider, file);
			Assert.Equal(-1, file.Length);
			Assert.Null(file.LastModified);
			Assert.Equal(TestFileName, file.Name);
			Assert.Equal("image/png", file.ContentType);
		}

		[Theory]
		[MemberData(nameof(ProvidersMemberData))]
		public async Task CreateAsync_FromVirtualPath_Write_DeleteFile(IUmbrellaFileProvider provider)
		{
			string physicalPath = $@"{BaseDirectory}\{TestFileName}";

			byte[] bytes = File.ReadAllBytes(physicalPath);

			IUmbrellaFileInfo file = await provider.CreateAsync($"~/images/{TestFileName}");

			Assert.True(file.IsNew);

			await file.WriteFromByteArrayAsync(bytes);

			CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

			//Cleanup
			await provider.DeleteAsync(file);
		}

		[Theory]
		[MemberData(nameof(ProvidersAndPathsMemberData))]
		public async Task CreateAsync_Write_ReadBytes_DeleteFile(IUmbrellaFileProvider provider, string path)
		{
			string physicalPath = $@"{BaseDirectory}\{TestFileName}";

			byte[] bytes = File.ReadAllBytes(physicalPath);

			//Create the file
			IUmbrellaFileInfo file = await provider.CreateAsync(path);

			Assert.True(file.IsNew);

			await file.WriteFromByteArrayAsync(bytes);

			CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

			bytes = await file.ReadAsByteArrayAsync();

			CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

			//Cleanup
			await provider.DeleteAsync(file);
		}

		[Theory]
		[MemberData(nameof(ProvidersMemberData))]
		public async Task CreateAsync_Write_GetAsync_ReadBytes_DeleteFile(IUmbrellaFileProvider provider)
		{
			string physicalPath = $@"{BaseDirectory}\{TestFileName}";

			byte[] bytes = File.ReadAllBytes(physicalPath);

			IUmbrellaFileInfo file = await provider.CreateAsync($"/images/{TestFileName}");

			Assert.True(file.IsNew);

			await file.WriteFromByteArrayAsync(bytes);

			CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

			//Get the file
			IUmbrellaFileInfo retrievedFile = await provider.GetAsync($"/images/{TestFileName}");

			Assert.NotNull(retrievedFile);

			CheckWrittenFileAssertions(provider, retrievedFile, bytes.Length, TestFileName);

			await file.ReadAsByteArrayAsync();
			Assert.Equal(bytes.Length, retrievedFile.Length);

			//Cleanup
			await provider.DeleteAsync(file);
		}

		[Theory]
		[MemberData(nameof(ProvidersAndPathsMemberData))]
		public async Task CreateAsync_Write_ReadStream_DeleteFile(IUmbrellaFileProvider provider, string path)
		{
			string physicalPath = $@"{BaseDirectory}\{TestFileName}";

			byte[] bytes = File.ReadAllBytes(physicalPath);

			//Create the file
			IUmbrellaFileInfo file = await provider.CreateAsync(path);

			Assert.True(file.IsNew);

			await file.WriteFromByteArrayAsync(bytes);

			CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

			using (var ms = new MemoryStream())
			{
				await file.WriteToStreamAsync(ms);
				bytes = ms.ToArray();
			}

			CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

			//Cleanup
			await provider.DeleteAsync(file);
		}

		[Theory]
		[MemberData(nameof(ProvidersMemberData))]
		public async Task CreateAsync_Write_GetAsync_ReadStream_DeleteFile(IUmbrellaFileProvider provider)
		{
			string physicalPath = $@"{BaseDirectory}\{TestFileName}";

			byte[] bytes = File.ReadAllBytes(physicalPath);

			IUmbrellaFileInfo file = await provider.CreateAsync($"/images/{TestFileName}");

			Assert.True(file.IsNew);

			await file.WriteFromByteArrayAsync(bytes);

			CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

			//Get the file
			IUmbrellaFileInfo retrievedFile = await provider.GetAsync($"/images/{TestFileName}");

			Assert.NotNull(retrievedFile);

			CheckWrittenFileAssertions(provider, retrievedFile, bytes.Length, TestFileName);

			byte[] retrievedBytes;

			using (var ms = new MemoryStream())
			{
				await file.WriteToStreamAsync(ms);
				retrievedBytes = ms.ToArray();
			}

			Assert.Equal(bytes.Length, retrievedBytes.Length);

			//Cleanup
			await provider.DeleteAsync(file);
		}

		[Theory]
		[MemberData(nameof(ProvidersMemberData))]
		public async Task GetAsync_NotExists(IUmbrellaFileProvider provider)
		{
			IUmbrellaFileInfo retrievedFile = await provider.GetAsync($"/images/doesnotexist.jpg");

			Assert.Null(retrievedFile);
		}

		[Theory]
		[MemberData(nameof(ProvidersMemberData))]
		public async Task CreateAsync_GetAsync(IUmbrellaFileProvider provider)
		{
			string path = "/images/createbutnowrite.jpg";
			var file = await provider.CreateAsync(path);

			Assert.True(file.IsNew);

			file = await provider.GetAsync(path);

			//Should fail as not writing to the file won't push it to blob storage
			Assert.Null(file);
		}

		[Theory]
		[MemberData(nameof(ProvidersMemberData))]
		public async Task CreateAsync_ExistsAsync(IUmbrellaFileProvider provider)
		{
			string path = "/images/createbutnowrite.jpg";
			var file = await provider.CreateAsync(path);

			Assert.True(file.IsNew);

			bool exists = await provider.ExistsAsync(path);

			//Should be false as not calling write shouldn't create the blob
			Assert.False(exists);
		}

		[Theory]
		[MemberData(nameof(ProvidersMemberData))]
		public async Task CreateAsync_Write_ExistsAsync_DeletePath(IUmbrellaFileProvider provider)
		{
			string physicalPath = $@"{BaseDirectory}\{TestFileName}";

			byte[] bytes = File.ReadAllBytes(physicalPath);

			string subpath = $"/images/{TestFileName}";

			IUmbrellaFileInfo file = await provider.CreateAsync(subpath);

			Assert.True(file.IsNew);

			await file.WriteFromByteArrayAsync(bytes);

			CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

			bool exists = await provider.ExistsAsync(subpath);

			Assert.True(exists);

			//Cleanup
			bool deleted = await provider.DeleteAsync(subpath);

			Assert.True(deleted);
		}

		[Theory]
		[MemberData(nameof(ProvidersMemberData))]
		public async Task SaveAsyncBytes_GetAsync_DeletePath(IUmbrellaFileProvider provider)
		{
			string physicalPath = $@"{BaseDirectory}\{TestFileName}";

			byte[] bytes = File.ReadAllBytes(physicalPath);

			string subpath = $"/images/{TestFileName}";

			var fileInfo = await provider.SaveAsync(subpath, bytes);

			CheckWrittenFileAssertions(provider, fileInfo, bytes.Length, TestFileName);

			fileInfo = await provider.GetAsync(subpath);

			CheckWrittenFileAssertions(provider, fileInfo, bytes.Length, TestFileName);

			//Cleanup
			bool deleted = await provider.DeleteAsync(subpath);

			Assert.True(deleted);
		}

		[Theory]
		[MemberData(nameof(ProvidersMemberData))]
		public async Task SaveAsyncStream_GetAsync_DeletePath(IUmbrellaFileProvider provider)
		{
			string physicalPath = $@"{BaseDirectory}\{TestFileName}";

			Stream stream = File.OpenRead(physicalPath);

			string subpath = $"/images/{TestFileName}";

			var fileInfo = await provider.SaveAsync(subpath, stream);

			CheckWrittenFileAssertions(provider, fileInfo, (int)stream.Length, TestFileName);

			fileInfo = await provider.GetAsync(subpath);

			CheckWrittenFileAssertions(provider, fileInfo, (int)stream.Length, TestFileName);

			//Cleanup
			bool deleted = await provider.DeleteAsync(subpath);

			Assert.True(deleted);
		}

		[Theory]
		[MemberData(nameof(ProvidersMemberData))]
		public async Task SaveAsyncBytes_ExistsAsync_DeletePath(IUmbrellaFileProvider provider)
		{
			string physicalPath = $@"{BaseDirectory}\{TestFileName}";

			byte[] bytes = File.ReadAllBytes(physicalPath);

			string subpath = $"/images/{TestFileName}";

			var fileInfo = await provider.SaveAsync(subpath, bytes);

			CheckWrittenFileAssertions(provider, fileInfo, bytes.Length, TestFileName);

			bool exists = await provider.ExistsAsync(subpath);

			Assert.True(exists);

			//Cleanup
			bool deleted = await provider.DeleteAsync(subpath);

			Assert.True(deleted);
		}

		[Theory]
		[MemberData(nameof(ProvidersMemberData))]
		public async Task SaveAsyncStream_ExistsAsync_DeletePath(IUmbrellaFileProvider provider)
		{
			string physicalPath = $@"{BaseDirectory}\{TestFileName}";

			Stream stream = File.OpenRead(physicalPath);

			string subpath = $"/images/{TestFileName}";

			var fileInfo = await provider.SaveAsync(subpath, stream);

			CheckWrittenFileAssertions(provider, fileInfo, (int)stream.Length, TestFileName);

			bool exists = await provider.ExistsAsync(subpath);

			Assert.True(exists);

			//Cleanup
			bool deleted = await provider.DeleteAsync(subpath);

			Assert.True(deleted);
		}

		[Theory]
		[MemberData(nameof(ProvidersMemberData))]
		public async Task CopyAsync_FromPath_NotExists(IUmbrellaFileProvider provider) => await Assert.ThrowsAsync<UmbrellaFileNotFoundException>(async () =>
																						{
																							await provider.CopyAsync("~/images/notexists.jpg", "~/images/willfail.png");
																						});

		[Theory]
		[MemberData(nameof(ProvidersMemberData))]
		public async Task CopyAsync_FromFileBytes_NotExists(IUmbrellaFileProvider provider) =>
			//Should be a file system exception with a file not found exception inside
			await Assert.ThrowsAsync<UmbrellaFileNotFoundException>(async () =>
			{
				string physicalPath = $@"{BaseDirectory}\{TestFileName}";

				byte[] bytes = File.ReadAllBytes(physicalPath);

				string subpath = $"/images/{TestFileName}";

				var fileInfo = await provider.SaveAsync(subpath, bytes);

				await provider.DeleteAsync(fileInfo);

				//At this point the file will not exist
				await provider.CopyAsync(fileInfo, "~/images/willfail.jpg");
			});

		[Theory]
		[MemberData(nameof(ProvidersMemberData))]
		public async Task CopyAsync_FromFileStream_NotExists(IUmbrellaFileProvider provider) =>
			//Should be a file system exception with a file not found exception inside
			await Assert.ThrowsAsync<UmbrellaFileNotFoundException>(async () =>
			{
				string physicalPath = $@"{BaseDirectory}\{TestFileName}";

				Stream stream = File.OpenRead(physicalPath);

				string subpath = $"/images/{TestFileName}";

				var fileInfo = await provider.SaveAsync(subpath, stream);

				await provider.DeleteAsync(fileInfo);

				//At this point the file will not exist
				await provider.CopyAsync(fileInfo, "~/images/willfail.jpg");
			});

		//[Theory]
		//[MemberData(nameof(ProvidersMemberData))]
		//public async Task CopyAsync_InvalidSourceType(IUmbrellaFileProvider provider)
		//{
		//    //TODO: Test that files coming from one provider cannot be used with a different one.
		//    //Maybe look at building this in somehow?
		//}

		[Theory]
		[MemberData(nameof(ProvidersMemberData))]
		public async Task CreateAsync_Write_CopyAsync_FromPath_ToPath(IUmbrellaFileProvider provider)
		{
			//Arrange
			string physicalPath = $@"{BaseDirectory}\{TestFileName}";

			byte[] bytes = File.ReadAllBytes(physicalPath);

			string subpath = $"/images/{TestFileName}";

			IUmbrellaFileInfo file = await provider.CreateAsync(subpath);

			Assert.True(file.IsNew);

			await file.WriteFromByteArrayAsync(bytes);

			CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

			//Act
			var copy = await provider.CopyAsync(subpath, "/images/copy.png");

			//Assert
			Assert.Equal(bytes.Length, copy.Length);

			//Read the file into memory and cache for our comparison
			await copy.ReadAsByteArrayAsync(cacheContents: true);

			CheckWrittenFileAssertions(provider, copy, bytes.Length, "copy.png");

			//Cleanup
			await provider.DeleteAsync(file);
			await provider.DeleteAsync(copy);
		}

		[Theory]
		[MemberData(nameof(ProvidersMemberData))]
		public async Task CreateAsync_Write_CopyAsync_FromFile_ToPath(IUmbrellaFileProvider provider)
		{
			//Arrange
			string physicalPath = $@"{BaseDirectory}\{TestFileName}";

			byte[] bytes = File.ReadAllBytes(physicalPath);

			string subpath = $"/images/{TestFileName}";

			IUmbrellaFileInfo file = await provider.CreateAsync(subpath);

			Assert.True(file.IsNew);

			await file.WriteFromByteArrayAsync(bytes);

			CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

			//Act
			var copy = await provider.CopyAsync(file, "/images/copy.png");

			//Assert
			Assert.Equal(bytes.Length, copy.Length);

			//Read the file into memory and cache for our comparison
			await copy.ReadAsByteArrayAsync(cacheContents: true);

			CheckWrittenFileAssertions(provider, copy, bytes.Length, "copy.png");

			//Cleanup
			await provider.DeleteAsync(file);
			await provider.DeleteAsync(copy);
		}

		[Theory]
		[MemberData(nameof(ProvidersMemberData))]
		public async Task CreateAsync_Write_CopyAsync_FromFile_ToFile(IUmbrellaFileProvider provider)
		{
			//Arrange
			string physicalPath = $@"{BaseDirectory}\{TestFileName}";

			byte[] bytes = File.ReadAllBytes(physicalPath);

			string subpath = $"/images/{TestFileName}";

			IUmbrellaFileInfo file = await provider.CreateAsync(subpath);

			Assert.True(file.IsNew);

			await file.WriteFromByteArrayAsync(bytes);

			CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

			//Create the copy file
			var copy = await provider.CreateAsync("/images/copy.png");

			//Act
			await provider.CopyAsync(file, copy);

			//Assert
			Assert.Equal(bytes.Length, copy.Length);

			//Read the file into memory and cache for our comparison
			await copy.ReadAsByteArrayAsync(cacheContents: true);

			CheckWrittenFileAssertions(provider, copy, bytes.Length, "copy.png");

			//Cleanup
			await provider.DeleteAsync(file);
			await provider.DeleteAsync(copy);
		}

		[Theory]
		[MemberData(nameof(ProvidersMemberData))]
		public async Task CreateAsync_CopyAsync(IUmbrellaFileProvider provider) => await Assert.ThrowsAsync<UmbrellaFileNotFoundException>(async () =>
																				 {
																					 // Should fail because you can't copy a new file
																					 var fileInfo = await provider.CreateAsync("~/images/testimage.jpg");

																					 var copy = await provider.CopyAsync(fileInfo, "~/images/copy.jpg");
																				 });

		[Theory]
		[MemberData(nameof(ProvidersMemberData))]
		public async Task DeleteAsync_NotExists(IUmbrellaFileProvider provider)
		{
			//Should fail silently
			bool deleted = await provider.DeleteAsync("/images/notexists.jpg");

			Assert.True(deleted);
		}

		[Theory]
		[MemberData(nameof(ProvidersMemberData))]
		public async Task Set_Get_MetadataValueAsync(IUmbrellaFileProvider provider)
		{
			// Arrange
			string physicalPath = $@"{BaseDirectory}\{TestFileName}";

			byte[] bytes = File.ReadAllBytes(physicalPath);

			string subpath = $"/images/{TestFileName}";

			IUmbrellaFileInfo file = await provider.CreateAsync(subpath);

			Assert.True(file.IsNew);

			await file.WriteFromByteArrayAsync(bytes);

			CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

			// Act
			await file.SetMetadataValueAsync("FirstName", "Richard");
			await file.SetMetadataValueAsync("LastName", "Edwards");

			// Assert
			file = await provider.GetAsync(subpath);

			Assert.False(file.IsNew);
			Assert.Equal("Richard", await file.GetMetadataValueAsync<string>("FirstName"));
			Assert.Equal("Edwards", await file.GetMetadataValueAsync<string>("LastName"));

			// Cleanup
			await provider.DeleteAsync(file);
		}

		private static IUmbrellaFileProvider CreateAzureBlobFileProvider()
		{
			var logger = new Mock<ILogger<UmbrellaAzureBlobStorageFileProvider>>();

			var loggerFactory = new Mock<ILoggerFactory>();
			loggerFactory.Setup(x => x.CreateLogger(typeof(UmbrellaAzureBlobStorageFileProvider).FullName)).Returns(logger.Object);

			var mimeTypeUtility = new Mock<IMimeTypeUtility>();
			mimeTypeUtility.Setup(x => x.GetMimeType(It.Is<string>(y => !string.IsNullOrEmpty(y) && y.Trim().ToLowerInvariant().EndsWith("png")))).Returns("image/png");
			mimeTypeUtility.Setup(x => x.GetMimeType(It.Is<string>(y => !string.IsNullOrEmpty(y) && y.Trim().ToLowerInvariant().EndsWith("jpg")))).Returns("image/jpg");

			var genericTypeConverter = new Mock<IGenericTypeConverter>();
			genericTypeConverter.Setup(x => x.Convert(It.IsAny<string>(), (string)null, null)).Returns<string, string, Func<string, string>>((x, y, z) => x);

			var options = new UmbrellaAzureBlobStorageFileProviderOptions
			{
				StorageConnectionString = StorageConnectionString
			};

			return new UmbrellaAzureBlobStorageFileProvider(loggerFactory.Object, mimeTypeUtility.Object, genericTypeConverter.Object, options);
		}

		private static IUmbrellaFileProvider CreateDiskFileProvider()
		{
			var logger = new Mock<ILogger<UmbrellaDiskFileProvider>>();

			var loggerFactory = new Mock<ILoggerFactory>();
			loggerFactory.Setup(x => x.CreateLogger(typeof(UmbrellaDiskFileProvider).FullName)).Returns(logger.Object);

			var mimeTypeUtility = new Mock<IMimeTypeUtility>();
			mimeTypeUtility.Setup(x => x.GetMimeType(It.Is<string>(y => !string.IsNullOrEmpty(y) && y.Trim().ToLowerInvariant().EndsWith("png")))).Returns("image/png");
			mimeTypeUtility.Setup(x => x.GetMimeType(It.Is<string>(y => !string.IsNullOrEmpty(y) && y.Trim().ToLowerInvariant().EndsWith("jpg")))).Returns("image/jpg");

			var genericTypeConverter = new Mock<IGenericTypeConverter>();
			genericTypeConverter.Setup(x => x.Convert(It.IsAny<string>(), (string)null, null)).Returns<string, string, Func<string, string>>((x, y, z) => x);

			var options = new UmbrellaDiskFileProviderOptions
			{
				RootPhysicalPath = BaseDirectory
			};

			return new UmbrellaDiskFileProvider(loggerFactory.Object, mimeTypeUtility.Object, genericTypeConverter.Object, options);
		}

		private void CheckWrittenFileAssertions(IUmbrellaFileProvider provider, IUmbrellaFileInfo file, int length, string fileName)
		{
			CheckPOCOFileType(provider, file);
			Assert.False(file.IsNew);
			Assert.Equal(length, file.Length);
			Assert.Equal(DateTimeOffset.UtcNow.Date, file.LastModified.Value.UtcDateTime.Date);
			Assert.Equal(fileName, file.Name);
			Assert.Equal("image/png", file.ContentType);
		}

		private void CheckPOCOFileType(IUmbrellaFileProvider provider, IUmbrellaFileInfo file)
		{
			switch (provider)
			{
				case UmbrellaAzureBlobStorageFileProvider azureProvider:
					Assert.IsType<UmbrellaAzureBlobStorageFileInfo>(file);
					break;
				case UmbrellaDiskFileProvider diskProvider:
					Assert.IsType<UmbrellaDiskFileInfo>(file);
					break;
				default:
					throw new Exception("Unsupported provider.");
			}
		}
	}
}