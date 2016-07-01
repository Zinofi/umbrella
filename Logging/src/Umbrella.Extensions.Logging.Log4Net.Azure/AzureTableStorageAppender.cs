﻿using log4net;
using log4net.Appender;
using log4net.Core;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Umbrella.Extensions.Logging.Log4Net.Azure.Configuration;
using Umbrella.Utilities;
using Umbrella.Utilities.Extensions;

namespace Umbrella.Extensions.Logging.Log4Net.Azure
{
    public class AzureTableStorageAppender : BufferingAppenderSkeleton
    {
        #region Private Members
        private AzureTableStorageLogAppenderOptions m_Config;
        private CloudStorageAccount m_Account;
        private CloudTableClient m_Client;
        private bool m_LogErrorsToConsole;
        #endregion

        #region Overridden Methods
        protected override void Append(LoggingEvent loggingEvent)
        {
            base.Append(loggingEvent);
        }

        protected override void Append(LoggingEvent[] loggingEvents)
        {
            base.Append(loggingEvents);
        }

        protected override void SendBuffer(LoggingEvent[] events)
        {
            if (m_LogErrorsToConsole)
                Console.WriteLine("SendBuffer started.");

            try
            {
                if (m_Config == null)
                    throw new ApplicationException($"The log4net {nameof(AzureTableStorageAppender)} with name: {Name} has not been initialized. The {nameof(InitializeAppender)} must be called from your code before the log appender is first used.");

                //Get the table we need to write stuff to and create it if needed
                CloudTable table = m_Client.GetTableReference($"{m_Config.TablePrefix}xxxxxx{DateTime.UtcNow.ToString("yyyyxMMxdd")}");
                table.CreateIfNotExists();

                string partitionKey = $"{DateTime.Now.Hour}-Hours";

                foreach (var batch in events.Split(100))
                {
                    var batchOperation = new TableBatchOperation();

                    foreach (var item in batch.Select(x => GetLogEntity(x, partitionKey)))
                    {
                        if (item != null)
                            batchOperation.Insert(item);
                    }

                    table.ExecuteBatch(batchOperation);
                }
            }
            catch (StorageException exc)
            {
                if (m_LogErrorsToConsole)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(exc.Message);
                    Console.WriteLine($"HttpStatusCode: {exc.RequestInformation.HttpStatusCode}, ErrorCode: {exc.RequestInformation.ExtendedErrorInformation.ErrorCode}, ErrorMessage: {exc.RequestInformation.ExtendedErrorInformation.ErrorMessage}");
                    Console.WriteLine($"AdditionalDetails: {exc.RequestInformation.ExtendedErrorInformation.AdditionalDetails.ToJsonString()}");
                    Console.ResetColor();
                }

                throw;
            }
        }
        #endregion

        #region Public Methods
        public void InitializeAppender(AzureTableStorageLogAppenderOptions options, string connectionString, bool logErrorsToConsole)
        {
            Guard.ArgumentNotNull(options, nameof(options));
            Guard.ArgumentNotNull(options.AppenderType, nameof(options.AppenderType));
            Guard.ArgumentNotNullOrWhiteSpace(connectionString, nameof(connectionString));
            Guard.ArgumentNotNullOrWhiteSpace(options.Name, nameof(options.Name));
            Guard.ArgumentNotNullOrWhiteSpace(options.TablePrefix, nameof(options.TablePrefix));

            m_Config = options;

            //Get both the account and create the table client here.
            //We will get a reference to the table when we need to write to it as the name of the table needs to change
            //to reflect the current date.
            m_Account = CloudStorageAccount.Parse(connectionString);
            m_Client = m_Account.CreateCloudTableClient();

            m_LogErrorsToConsole = logErrorsToConsole;

            ActivateOptions();
        }

        public static void InitializeAllAppenders(AzureTableStorageLoggingOptions options)
        {
            Guard.ArgumentNotNull(options, nameof(options));

            foreach (var appender in LogManager.GetAllRepositories().SelectMany(x => x.GetAppenders().OfType<AzureTableStorageAppender>()))
            {
                var config = options.Appenders.SingleOrDefault(x => x.Name == appender.Name);

                if (config == null)
                    throw new ApplicationException($"Configuration cannot be found for appender {appender.Name}");

                appender.InitializeAppender(config, options.ConnectionString, options.LogErrorsToConsole);
            }
        }
        #endregion

        #region Private Methods
        private ITableEntity GetLogEntity(LoggingEvent e, string partitionKey)
        {
            switch (m_Config.AppenderType)
            {
                case AzureTableStorageLogAppenderType.Client:
                    return new AzureLoggingClientEventEntity(e, partitionKey);
                case AzureTableStorageLogAppenderType.Server:
                    return new AzureLoggingServerEventEntity(e, partitionKey);
            }

            return null;
        } 
        #endregion
    }
}