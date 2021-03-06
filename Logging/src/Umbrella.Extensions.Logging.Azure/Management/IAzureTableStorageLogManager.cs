﻿using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Umbrella.Extensions.Logging.Azure.Configuration;
using Umbrella.Extensions.Logging.Azure.Management.Configuration;

namespace Umbrella.Extensions.Logging.Azure.Management
{
    public interface IAzureTableStorageLogManager
    {
        Task<(List<AzureTableStorageLogDataSource> Items, int TotalCount)> FindAllDataSourceByOptionsAsync(AzureTableStorageLogSearchOptions options, CancellationToken cancellationToken = default);
        Task<(List<AzureTableStorageLogTable> Items, int TotalCount)> FindAllLogTableByTablePrefixAndOptionsAsync(string tablePrefix, AzureTableStorageLogSearchOptions options, CancellationToken cancellationToken = default);
        Task<(AzureTableStorageLogAppenderType? AppenderType, List<TableEntity> Items, int TotalCount)> FindAllTableEntityByOptionsAsync(string tablePrefix, string tableName, AzureTableStorageLogSearchOptions options, CancellationToken cancellationToken = default);
        Task<AzureTableStorageLogDeleteOperationResult> DeleteTableByNameAsync(string tableName, CancellationToken cancellationToken = default);
        Task ClearTableNameCacheAsync(string tablePrefix, CancellationToken cancellationToken = default);
    }
}