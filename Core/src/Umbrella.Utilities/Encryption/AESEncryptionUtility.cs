﻿using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace Umbrella.Utilities.Encryption
{
	/// <summary>
	/// Utility to provide string encryption capabilities
	/// </summary>
	public class AesEncryptionUtility : EncryptionUtility<AesManaged>
	{
		#region Constructors		
		/// <summary>
		/// Initializes a new instance of the <see cref="AesEncryptionUtility"/> class.
		/// </summary>
		/// <param name="logger">The logger.</param>
		public AesEncryptionUtility(ILogger<AesEncryptionUtility> logger)
			: base(logger)
		{
		}
        #endregion
	}
}