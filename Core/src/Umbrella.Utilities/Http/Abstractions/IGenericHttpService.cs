﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Umbrella.Utilities.Http.Abstractions
{
	/// <summary>
	/// An opinionated generic HTTP service used to query remote endpoints that follow the same conventions.
	/// </summary>
	public interface IGenericHttpService
	{
		/// <summary>
		/// Deletes a resource from the server.
		/// </summary>
		/// <param name="url">The URL.</param>
		/// <param name="parameters">The parameters.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The result of the operation.</returns>
		Task<HttpCallResult> DeleteAsync(string url, IDictionary<string, string> parameters = null, CancellationToken cancellationToken = default);

		/// <summary>
		/// Gets a resource from the server.
		/// </summary>
		/// <typeparam name="TResult">The type of the result.</typeparam>
		/// <param name="url">The URL.</param>
		/// <param name="parameters">The parameters.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The result of the operation.</returns>
		Task<HttpCallResult<TResult>> GetAsync<TResult>(string url, IDictionary<string, string> parameters = null, CancellationToken cancellationToken = default);

		/// <summary>
		/// POSTS the resource to the server.
		/// </summary>
		/// <typeparam name="TItem">The type of the item.</typeparam>
		/// <typeparam name="TResult">The type of the result.</typeparam>
		/// <param name="url">The URL.</param>
		/// <param name="item">The item.</param>
		/// <param name="parameters">The parameters.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The result of the operation.</returns>
		Task<HttpCallResult<TResult>> PostAsync<TItem, TResult>(string url, TItem item, IDictionary<string, string> parameters = null, CancellationToken cancellationToken = default);

		/// <summary>
		/// PUTS the resource to the server.
		/// </summary>
		/// <typeparam name="TItem">The type of the item.</typeparam>
		/// <typeparam name="TResult">The type of the result.</typeparam>
		/// <param name="url">The URL.</param>
		/// <param name="item">The item.</param>
		/// <param name="parameters">The parameters.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The result of the operation.</returns>
		Task<HttpCallResult<TResult>> PutAsync<TItem, TResult>(string url, TItem item, IDictionary<string, string> parameters = null, CancellationToken cancellationToken = default);
	}
}