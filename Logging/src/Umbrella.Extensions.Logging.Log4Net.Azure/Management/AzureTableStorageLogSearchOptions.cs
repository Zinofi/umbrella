﻿using System;
using System.Collections.Generic;
using System.Text;
using Umbrella.Utilities.Enumerations;

namespace Umbrella.Extensions.Logging.Log4Net.Azure.Management
{
    public class AzureTableStorageLogSearchOptions
    {
        public string SortProperty { get; set; }
        public SortDirection SortDirection { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public Dictionary<string, string> Filters { get; set; }
    }
}