﻿namespace Umbrella.AppFramework.Shared.Constants
{
	/// <summary>
	/// Core HTTP Header names used by client applications developed using the Umbrella.AppFramework.
	/// </summary>
	public static class AppHttpHeaderName
	{
		/// <summary>
		/// The header containing a new auth token.
		/// </summary>
		public const string NewAuthToken = "X-Auth-Token";

		/// <summary>
		/// The header containing the app client ID. This is an ID that is unique to each client application instance.
		/// </summary>
		public const string AppClientId = "X-AppClientId";

		/// <summary>
		/// The header containing the version of the app that the client is running.
		/// </summary>
		public const string AppClientVersion = "X-AppClientVersion";

		/// <summary>
		/// The header containing the type of the app that the client is running, e.g. Web, iOS, Android, etc.
		/// </summary>
		public const string AppClientType = "X-AppClientType";
		
		/// <summary>
		/// The header containing a message generated by the server when it has been determined that the requesting client
		/// app is outdated and needs to be updated.
		/// </summary>
		public const string AppUpdateOptionalMessage = "X-AppUpdateOptionalMessage";

		/// <summary>
		/// The header containing the link to the App Store where updates can be sourced, e.g. App Store, Play Store, etc.
		/// </summary>
		public const string AppUpdateStoreLink = "X-AppUpdateStoreLink";
	}
}