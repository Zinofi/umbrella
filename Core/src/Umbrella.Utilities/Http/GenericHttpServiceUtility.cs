﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Sorting;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Http.Abstractions;
using Umbrella.Utilities.Http.Constants;

namespace Umbrella.Utilities.Http
{
	/// <summary>
	/// A utility class used to support querying HTTP services.
	/// </summary>
	public class GenericHttpServiceUtility : IGenericHttpServiceUtility
	{
		private readonly ILogger<GenericHttpServiceUtility> _logger;

		/// <summary>
		/// Initializes a new instance of the <see cref="GenericHttpServiceUtility"/> class.
		/// </summary>
		/// <param name="logger">The logger.</param>
		public GenericHttpServiceUtility(ILogger<GenericHttpServiceUtility> logger)
		{
			_logger = logger;
		}

		/// <inheritdoc />
		public IDictionary<string, string> CreateSearchQueryParameters<TItem>(int pageNumber = 0, int pageSize = 20, IEnumerable<SortExpression<TItem>> sorters = null, IEnumerable<FilterExpression<TItem>> filters = null, FilterExpressionCombinator filterCombinator = FilterExpressionCombinator.Or)
			=> CreateSearchQueryParameters(pageNumber, pageSize, sorters?.ToSortExpressionDescriptors(), filters?.ToFilterExpressionDescriptors(), filterCombinator);

		/// <inheritdoc />
		public IDictionary<string, string> CreateSearchQueryParameters(int pageNumber = 0, int pageSize = 20, IEnumerable<SortExpressionDescriptor> sorters = null, IEnumerable<FilterExpressionDescriptor> filters = null, FilterExpressionCombinator filterCombinator = FilterExpressionCombinator.Or)
		{
			try
			{
				var parameters = new Dictionary<string, string>();

				int sorterCount = sorters?.Count() ?? 0;
				int filterCount = filters?.Count() ?? 0;

				if ((pageNumber > 0 && pageSize > 0) || sorterCount > 0 || filterCount > 0)
				{
					if (pageNumber > 0 && pageSize > 0)
					{
						parameters.Add("pageNumber", pageNumber.ToString());
						parameters.Add("pageSize", pageSize.ToString());
					}

					if (sorterCount > 0)
						parameters.Add("sorters", UmbrellaStatics.SerializeJson(sorters));

					if (filterCount > 0)
					{
						parameters.Add("filters", UmbrellaStatics.SerializeJson(filters));
						parameters.Add("filterCombinator", filterCombinator.ToString());
					}
				}

				return parameters;
			}
			catch (Exception exc) when (_logger.WriteError(exc, new { pageNumber, pageSize, sorters, filters, filterCombinator }, returnValue: true))
			{
				throw new UmbrellaException("There has been a problem creating the query parameters from the search parameters.", exc);
			}
		}

		/// <inheritdoc />
		public string GetUrlWithParmeters(string url, IDictionary<string, string> parameters)
			=> parameters?.Count > 0 ? QueryHelpers.AddQueryString(url, parameters) : url;

		/// <inheritdoc />
		public async Task<HttpProblemDetails> GetProblemDetailsAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
		{
			string defaultMessage = response.StatusCode switch
			{
				HttpStatusCode.Unauthorized => HttpServiceMessages.DefaultUnauthorizedErrorMessage,
				HttpStatusCode.Forbidden => HttpServiceMessages.DefaultForbiddenErrorMessage,
				HttpStatusCode.InternalServerError => HttpServiceMessages.DefaultServerErrorMessage,
				_ => HttpServiceMessages.DefaultUnknownErrorMessage
			};

			HttpContentHeaders headers = response.Content.Headers;

			if (headers.Count() == 0 || headers.ContentType?.MediaType?.Equals("application/problem+json", StringComparison.OrdinalIgnoreCase) != true)
				return new HttpProblemDetails { Title = "Error", Detail = defaultMessage };

			return await response.Content.ReadFromJsonAsync<HttpProblemDetails>(cancellationToken: cancellationToken).ConfigureAwait(false);
		}
	}
}