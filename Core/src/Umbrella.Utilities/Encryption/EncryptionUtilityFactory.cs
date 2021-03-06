﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Umbrella.Utilities.Encryption.Abstractions;

namespace Umbrella.Utilities.Encryption
{
	// TODO: What was this used for?? Can't remember using it in a project. Must have done somewhere though!
	// Consider deleting this and using DI to configure stuff.
	[Obsolete]
    public class EncryptionUtilityFactory : IEncryptionUtilityFactory
    {
        #region Private Members
        private readonly ILoggerFactory m_LoggerFactory;
        #endregion

        #region Constructors
        public EncryptionUtilityFactory(ILoggerFactory loggerFactory)
        {
            m_LoggerFactory = loggerFactory;
        }
        #endregion

        #region Public Methods
        public T CreateEncryptionUtility<T>(string encryptionKey, string initializationVector)
            where T : IEncryptionUtility
        {
            ILogger logger = m_LoggerFactory.CreateLogger<T>();

            var instance = (T)Activator.CreateInstance(typeof(T), logger);
            instance.Initialize(encryptionKey, initializationVector);

            return instance;
        }
        #endregion
    }
}