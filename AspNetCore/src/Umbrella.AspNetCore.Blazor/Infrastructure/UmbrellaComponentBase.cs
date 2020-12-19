﻿using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Umbrella.AppFramework.Security.Abstractions;
using Umbrella.AppFramework.Utilities.Constants;
using Umbrella.AspNetCore.Blazor.Components.Dialog.Abstractions;
using Umbrella.AspNetCore.Blazor.Extensions;
using Umbrella.Utilities.Http;

namespace Umbrella.AspNetCore.Blazor.Infrastructure
{
	/// <summary>
	/// A base component to be used with Blazor components which contain commonly used functionality.
	/// </summary>
	/// <seealso cref="ComponentBase" />
	public abstract class UmbrellaComponentBase : ComponentBase
	{
		[Inject]
		private ILoggerFactory LoggerFactory { get; set; } = null!;

		/// <summary>
		/// Gets the navigation manager.
		/// </summary>
		/// <remarks>
		/// Useful extension methods can be found inside <see cref="NavigationManagerExtensions"/>.
		/// </remarks>
		[Inject]
		protected NavigationManager Navigation { get; private set; } = null!;

		/// <summary>
		/// Gets the dialog utility.
		/// </summary>
		[Inject]
		protected IUmbrellaDialogUtility DialogUtility { get; private set; } = null!;

		/// <summary>
		/// Gets the authentication helper.
		/// </summary>
		[Inject]
		protected IAppAuthHelper AuthHelper { get; private set; } = null!;

		/// <summary>
		/// Gets the logger.
		/// </summary>
		protected ILogger Logger { get; private set; } = null!;

		/// <inheritdoc />
		protected override void OnInitialized()
		{
			base.OnInitialized();

			Logger = LoggerFactory.CreateLogger(GetType());
		}

		/// <summary>
		/// Gets the claims principal for the current user.
		/// </summary>
		/// <returns>The claims principal.</returns>
		protected async Task<ClaimsPrincipal> GetClaimsPrincipalAsync() => await AuthHelper.GetCurrentClaimsPrincipalAsync();

		/// <summary>
		/// Shows the problem details error message. If this does not exist, the error message defaults to <see cref="DialogDefaults.UnknownErrorMessage"/>.
		/// </summary>
		/// <param name="problemDetails">The problem details.</param>
		/// <param name="title">The title.</param>
		protected async Task ShowProblemDetailsErrorMessageAsync(HttpProblemDetails? problemDetails, string title = "Error")
			=> await DialogUtility.ShowDangerMessageAsync(problemDetails?.Detail ?? DialogDefaults.UnknownErrorMessage, title);
	}
}