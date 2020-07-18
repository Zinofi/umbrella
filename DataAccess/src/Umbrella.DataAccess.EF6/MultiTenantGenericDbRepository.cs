﻿using System;
using Microsoft.Extensions.Logging;
using Umbrella.DataAccess.Abstractions;
using Umbrella.Utilities.Context.Abstractions;
using Umbrella.Utilities.Data.Abstractions;

namespace Umbrella.DataAccess.EF6
{
	/// <summary>
	/// Serves as the base class for multi-tenant repositories which provide CRUD access to entities stored in a database accessed using Entity Framework 6.
	/// </summary>
	/// <typeparam name="TEntity">The type of the entity.</typeparam>
	/// <typeparam name="TDbContext">The type of the database context.</typeparam>
	public abstract class MultiTenantGenericDbRepository<TEntity, TDbContext> : MultiTenantGenericDbRepository<TEntity, TDbContext, RepoOptions>
		where TEntity : class, IEntity<int>
		where TDbContext : UmbrellaDbContext
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MultiTenantGenericDbRepository{TEntity, TDbContext}"/> class.
		/// </summary>
		/// <param name="dbContext">The database context.</param>
		/// <param name="userAuditDataFactory">The user audit data factory.</param>
		/// <param name="logger">The logger.</param>
		/// <param name="lookupNormalizer">The lookup normalizer.</param>
		/// <param name="entityValidator">The entity validator.</param>
		/// <param name="dbContextHelper">The database context helper.</param>
		/// <param name="dbAppTenantSessionContext">The database application tenant session context.</param>
		public MultiTenantGenericDbRepository(
			TDbContext dbContext,
			ICurrentUserIdAccessor<int> userAuditDataFactory,
			ILogger logger,
			ILookupNormalizer lookupNormalizer,
			IEntityValidator entityValidator,
			IUmbrellaDbContextHelper dbContextHelper,
			DbAppTenantSessionContext<int> dbAppTenantSessionContext)
			: base(dbContext, userAuditDataFactory, logger, lookupNormalizer, entityValidator, dbContextHelper, dbAppTenantSessionContext)
		{
		}
	}

	/// <summary>
	/// Serves as the base class for multi-tenant repositories which provide CRUD access to entities stored in a database accessed using Entity Framework 6.
	/// </summary>
	/// <typeparam name="TEntity">The type of the entity.</typeparam>
	/// <typeparam name="TDbContext">The type of the database context.</typeparam>
	/// <typeparam name="TRepoOptions">The type of the repo options.</typeparam>
	public abstract class MultiTenantGenericDbRepository<TEntity, TDbContext, TRepoOptions> : MultiTenantGenericDbRepository<TEntity, TDbContext, TRepoOptions, int>
		where TEntity : class, IEntity<int>
		where TDbContext : UmbrellaDbContext
		where TRepoOptions : RepoOptions, new()
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MultiTenantGenericDbRepository{TEntity, TDbContext, TRepoOptions}"/> class.
		/// </summary>
		/// <param name="dbContext">The database context.</param>
		/// <param name="userAuditDataFactory">The user audit data factory.</param>
		/// <param name="logger">The logger.</param>
		/// <param name="lookupNormalizer">The lookup normalizer.</param>
		/// <param name="entityValidator">The entity validator.</param>
		/// <param name="dbContextHelper">The database context helper.</param>
		/// <param name="dbAppTenantSessionContext">The database application tenant session context.</param>
		public MultiTenantGenericDbRepository(
			TDbContext dbContext,
			ICurrentUserIdAccessor<int> userAuditDataFactory,
			ILogger logger,
			ILookupNormalizer lookupNormalizer,
			IEntityValidator entityValidator,
			IUmbrellaDbContextHelper dbContextHelper,
			DbAppTenantSessionContext<int> dbAppTenantSessionContext)
			: base(dbContext, userAuditDataFactory, logger, lookupNormalizer, entityValidator, dbContextHelper, dbAppTenantSessionContext)
		{
		}
	}

	/// <summary>
	/// Serves as the base class for multi-tenant repositories which provide CRUD access to entities stored in a database accessed using Entity Framework 6.
	/// </summary>
	/// <typeparam name="TEntity">The type of the entity.</typeparam>
	/// <typeparam name="TDbContext">The type of the database context.</typeparam>
	/// <typeparam name="TRepoOptions">The type of the repo options.</typeparam>
	/// <typeparam name="TEntityKey">The type of the entity key.</typeparam>
	public abstract class MultiTenantGenericDbRepository<TEntity, TDbContext, TRepoOptions, TEntityKey> : MultiTenantGenericDbRepository<TEntity, TDbContext, TRepoOptions, TEntityKey, int>
		where TEntity : class, IEntity<TEntityKey>
		where TDbContext : UmbrellaDbContext
		where TRepoOptions : RepoOptions, new()
		where TEntityKey : IEquatable<TEntityKey>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MultiTenantGenericDbRepository{TEntity, TDbContext, TRepoOptions, TEntityKey}"/> class.
		/// </summary>
		/// <param name="dbContext">The database context.</param>
		/// <param name="userAuditDataFactory">The user audit data factory.</param>
		/// <param name="logger">The logger.</param>
		/// <param name="lookupNormalizer">The lookup normalizer.</param>
		/// <param name="entityValidator">The entity validator.</param>
		/// <param name="dbContextHelper">The database context helper.</param>
		/// <param name="dbAppTenantSessionContext">The database application tenant session context.</param>
		public MultiTenantGenericDbRepository(
			TDbContext dbContext,
			ICurrentUserIdAccessor<int> userAuditDataFactory,
			ILogger logger,
			ILookupNormalizer lookupNormalizer,
			IEntityValidator entityValidator,
			IUmbrellaDbContextHelper dbContextHelper,
			DbAppTenantSessionContext<int> dbAppTenantSessionContext)
			: base(dbContext, userAuditDataFactory, logger, lookupNormalizer, entityValidator, dbContextHelper, dbAppTenantSessionContext)
		{
		}
	}

	/// <summary>
	/// Serves as the base class for multi-tenant repositories which provide CRUD access to entities stored in a database accessed using Entity Framework 6.
	/// </summary>
	/// <typeparam name="TEntity">The type of the entity.</typeparam>
	/// <typeparam name="TDbContext">The type of the database context.</typeparam>
	/// <typeparam name="TRepoOptions">The type of the repo options.</typeparam>
	/// <typeparam name="TEntityKey">The type of the entity key.</typeparam>
	/// <typeparam name="TUserAuditKey">The type of the user audit key.</typeparam>
	public abstract class MultiTenantGenericDbRepository<TEntity, TDbContext, TRepoOptions, TEntityKey, TUserAuditKey> : MultiTenantGenericDbRepository<TEntity, TDbContext, TRepoOptions, TEntityKey, int, int>
		where TEntity : class, IEntity<TEntityKey>
		where TDbContext : UmbrellaDbContext
		where TRepoOptions : RepoOptions, new()
		where TEntityKey : IEquatable<TEntityKey>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MultiTenantGenericDbRepository{TEntity, TDbContext, TRepoOptions, TEntityKey, TUserAuditKey}"/> class.
		/// </summary>
		/// <param name="dbContext">The database context.</param>
		/// <param name="userAuditDataFactory">The user audit data factory.</param>
		/// <param name="logger">The logger.</param>
		/// <param name="lookupNormalizer">The lookup normalizer.</param>
		/// <param name="entityValidator">The entity validator.</param>
		/// <param name="dbContextHelper">The database context helper.</param>
		/// <param name="dbAppTenantSessionContext">The database application tenant session context.</param>
		public MultiTenantGenericDbRepository(
			TDbContext dbContext,
			ICurrentUserIdAccessor<int> userAuditDataFactory,
			ILogger logger,
			ILookupNormalizer lookupNormalizer,
			IEntityValidator entityValidator,
			IUmbrellaDbContextHelper dbContextHelper,
			DbAppTenantSessionContext<int> dbAppTenantSessionContext)
			: base(dbContext, userAuditDataFactory, logger, lookupNormalizer, entityValidator, dbContextHelper, dbAppTenantSessionContext)
		{
		}
	}

	/// <summary>
	/// Serves as the base class for multi-tenant repositories which provide CRUD access to entities stored in a database accessed using Entity Framework 6.
	/// </summary>
	/// <typeparam name="TEntity">The type of the entity.</typeparam>
	/// <typeparam name="TDbContext">The type of the database context.</typeparam>
	/// <typeparam name="TRepoOptions">The type of the repo options.</typeparam>
	/// <typeparam name="TEntityKey">The type of the entity key.</typeparam>
	/// <typeparam name="TUserAuditKey">The type of the user audit key.</typeparam>
	/// <typeparam name="TAppTenantKey">The type of the application tenant key.</typeparam>
	public abstract class MultiTenantGenericDbRepository<TEntity, TDbContext, TRepoOptions, TEntityKey, TUserAuditKey, TAppTenantKey> : GenericDbRepository<TEntity, TDbContext, TRepoOptions, TEntityKey, TUserAuditKey>
		where TEntity : class, IEntity<TEntityKey>
		where TDbContext : UmbrellaDbContext
		where TRepoOptions : RepoOptions, new()
		where TEntityKey : IEquatable<TEntityKey>
		where TUserAuditKey : IEquatable<TUserAuditKey>
		where TAppTenantKey : IEquatable<TAppTenantKey>
	{
		/// <summary>
		/// Gets the application tenant session context.
		/// </summary>
		protected DbAppTenantSessionContext<TAppTenantKey> AppTenantSessionContext { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="MultiTenantGenericDbRepository{TEntity, TDbContext, TRepoOptions, TEntityKey, TUserAuditKey, TAppTenantKey}"/> class.
		/// </summary>
		/// <param name="dbContext">The database context.</param>
		/// <param name="currentUserIdAccessor">The current user identifier accessor.</param>
		/// <param name="logger">The logger.</param>
		/// <param name="lookupNormalizer">The lookup normalizer.</param>
		/// <param name="entityValidator">The entity validator.</param>
		/// <param name="dbContextHelper">The database context helper.</param>
		/// <param name="dbAppTenantSessionContext">The database application tenant session context.</param>
		public MultiTenantGenericDbRepository(
			TDbContext dbContext,
			ICurrentUserIdAccessor<TUserAuditKey> currentUserIdAccessor,
			ILogger logger,
			ILookupNormalizer lookupNormalizer,
			IEntityValidator entityValidator,
			IUmbrellaDbContextHelper dbContextHelper,
			DbAppTenantSessionContext<TAppTenantKey> dbAppTenantSessionContext)
			: base(dbContext, logger, lookupNormalizer, currentUserIdAccessor, dbContextHelper, entityValidator)
		{
			AppTenantSessionContext = dbAppTenantSessionContext;
		}

		/// <inheritdoc />
		protected override void PreSaveWork(TEntity entity, bool addToContext, bool forceAdd, out bool isNew)
		{
			if (entity is IAppTenantEntity<TAppTenantKey> tenantEntity && AppTenantSessionContext.IsAuthenticated)
				tenantEntity.AppTenantId = AppTenantSessionContext.AppTenantId;

			base.PreSaveWork(entity, addToContext, forceAdd, out isNew);
		}
	}
}