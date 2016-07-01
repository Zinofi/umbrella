﻿using log4net.Core;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Umbrella.Extensions.Logging.Log4Net.Azure
{
    public class AzureLoggingServerEventEntity : TableEntity
    {
        public DateTime EventTimeStamp { get; set; }
        public StackFrameItem[] StackFrames { get; set; }
        public string ClassName { get; set; }
        public string Exception { get; set; }
        public string FileName { get; set; }
        public string Level { get; set; }
        public string LineNumber { get; set; }
        public string Location { get; set; }
        public string Message { get; set; }
        public string MethodName { get; set; }
        public string Properties { get; set; }
        public string ThreadName { get; set; }

        public AzureLoggingServerEventEntity(LoggingEvent e, string partitionKey)
        {
            Level = e.Level.ToString();

            //Write additional properties into a single table column
            var sb = new StringBuilder(e.Properties.Count);

            foreach (DictionaryEntry entry in e.Properties)
            {
                sb.AppendLine($"{entry.Key}:{entry.Value}");
            }

            Properties = sb.ToString();
            Message = e.RenderedMessage + Environment.NewLine + e.GetExceptionString();
            ThreadName = e.ThreadName;
            EventTimeStamp = e.TimeStamp;
            Location = e.LocationInformation.FullInfo;
            ClassName = e.LocationInformation.ClassName;
            FileName = e.LocationInformation.FileName;
            LineNumber = e.LocationInformation.LineNumber;
            MethodName = e.LocationInformation.MethodName;
            StackFrames = e.LocationInformation.StackFrames;

            if (e.ExceptionObject != null)
                Exception = e.ExceptionObject.ToString();

            PartitionKey = partitionKey;

            //The row key will be the current date and time in a format that will ensure items are ordered
            //in ascending date order. GUID on the end is to ensure the RowKey is unique where the datetime string clashes with another RowKey.
            RowKey = DateTime.UtcNow.ToString("yyyy-MM-dd hh:mm:ss.ffffff") + "-" + Guid.NewGuid().ToString();
        }
    }
}