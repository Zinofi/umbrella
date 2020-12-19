﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Pagination;
using Umbrella.Utilities.Data.Sorting;
using Umbrella.Utilities.Http.Abstractions;

namespace Umbrella.DataAccess.Remote.Abstractions
{
	// TODO: Create a read-only remote repo?
	// TODO: More generic overloads to avoid this issues.

	/// <summary>
	/// A generic repository used to query and update a remote resource.
	/// </summary>
	/// <typeparam name="TItem">The type of the item.</typeparam>
	/// <typeparam name="TIdentifier">The type of the identifier.</typeparam>
	/// <typeparam name="TSlimItem">The type of the slim item.</typeparam>
	/// <typeparam name="TPaginatedResultModel">The type of the paginated result model.</typeparam>
	/// <typeparam name="TCreateItem">The type of the create item.</typeparam>
	/// <typeparam name="TCreateResult">The type of the create result.</typeparam>
	/// <typeparam name="TUpdateItem">The type of the update item.</typeparam>
	/// <typeparam name="TUpdateResult">The type of the update result.</typeparam>
	public interface IGenericRemoteRepository<TItem, TIdentifier, TSlimItem, TPaginatedResultModel, TCreateItem, TCreateResult, TUpdateItem, TUpdateResult>
		where TItem : class, IRemoteItem<TIdentifier>
		where TIdentifier : IEquatable<TIdentifier>
		where TSlimItem : class, IRemoteItem<TIdentifier>
		where TUpdateItem : class, IRemoteItem<TIdentifier>
		where TPaginatedResultModel : PaginatedResultModel<TSlimItem>
	{
		/// <summary>
		/// Creates the specified resource on the remote server.
		/// </summary>
		/// <param name="item">The item.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="sanitize">if set to <c>true</c> sanitizes the <paramref name="item"/> before saving.</param>
		/// <param name="validate">if set to <c>true</c> validated the <paramref name="item"/> before saving.</param>
		/// <returns>The result of the remote operation.</returns>
		Task<(IHttpCallResult<TCreateResult> result, IReadOnlyCollection<ValidationResult> validationResults)> CreateAsync(TCreateItem item, CancellationToken cancellationToken = default, bool sanitize = true, bool validate = true);

		/// <summary>
		/// Deletes the specified resource from the remote server.
		/// </summary>
		/// <param name="id">The identifier.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The result of the remote operation.</returns>
		Task<IHttpCallResult> DeleteAsync(TIdentifier id, CancellationToken cancellationToken = default);

		/// <summary>
		/// Determines if the specified resource exists on the remote server.
		/// </summary>
		/// <param name="id">The identifier.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The result of the remote operation.</returns>
		Task<IHttpCallResult<bool>> ExistsByIdAsync(TIdentifier id, CancellationToken cancellationToken = default);

		/// <summary>
		/// Finds all slim results of <typeparamref name="TSlimItem"/> on the server using the specified parameters.
		/// </summary>
		/// <param name="pageNumber">The page number.</param>
		/// <param name="pageSize">Size of the page.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="sorters">The sorters.</param>
		/// <param name="filters">The filters.</param>
		/// <param name="filterCombinator">The filter combinator.</param>
		/// <returns>The result of the remote operation.</returns>
		Task<IHttpCallResult<TPaginatedResultModel>> FindAllSlimAsync(int pageNumber = 0, int pageSize = 20, CancellationToken cancellationToken = default, IEnumerable<SortExpressionDescriptor>? sorters = null, IEnumerable<FilterExpressionDescriptor>? filters = null, FilterExpressionCombinator filterCombinator = FilterExpressionCombinator.Or);

		/// <summary>
		/// Finds the specified resource on the remote server.
		/// </summary>
		/// <param name="id">The identifier.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The result of the remote operation.</returns>
		Task<IHttpCallResult<TItem>> FindByIdAsync(TIdentifier id, CancellationToken cancellationToken = default);

		/// <summary>
		/// Finds the total count of <typeparamref name="TItem"/> that exist on the remote server.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The result of the remote operation.</returns>
		Task<IHttpCallResult<int>> FindTotalCountAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Updates the specified resource on the remote server.
		/// </summary>
		/// <param name="item">The item.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="sanitize">if set to <c>true</c> sanitizes the <paramref name="item"/> before saving.</param>
		/// <param name="validate">if set to <c>true</c> validated the <paramref name="item"/> before saving.</param>
		/// <returns>The result of the remote operation.</returns>
		Task<(IHttpCallResult<TUpdateResult> result, IReadOnlyCollection<ValidationResult> validationResults)> UpdateAsync(TUpdateItem item, CancellationToken cancellationToken = default, bool sanitize = true, bool validate = true);
	}
}