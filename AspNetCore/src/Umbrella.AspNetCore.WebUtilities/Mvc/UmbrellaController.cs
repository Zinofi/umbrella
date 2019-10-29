﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Umbrella.AspNetCore.WebUtilities.Mvc
{
	// TODO: Review the legacy controller
	/// <summary>
	/// Serves as the base class for all MVC controllers.
	/// </summary>
	public abstract class UmbrellaController : Controller
	{
		#region Protected Properties		
		/// <summary>
		/// Gets the logger.
		/// </summary>
		protected ILogger Log { get; }
		#endregion

		#region Constructors		
		/// <summary>
		/// Initializes a new instance of the <see cref="UmbrellaController"/> class.
		/// </summary>
		/// <param name="logger">The logger.</param>
		public UmbrellaController(ILogger logger)
		{
			Log = logger;
		}
		#endregion
	}
}