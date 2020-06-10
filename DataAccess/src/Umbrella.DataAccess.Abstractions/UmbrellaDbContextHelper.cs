﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Umbrella.DataAccess.Abstractions
{
	public class UmbrellaDbContextHelper : IUmbrellaDbContextHelper
	{
		protected ILogger Log { get; }
		protected Dictionary<object, Func<CancellationToken, Task>> PostSaveChangesSaveActionDictionary { get; } = new Dictionary<object, Func<CancellationToken, Task>>();

		#region Constructors
		public UmbrellaDbContextHelper(ILogger<UmbrellaDbContextHelper> logger)
		{
			Log = logger;
		}
		#endregion

		public virtual void RegisterPostSaveChangesAction(object entity, Func<CancellationToken, Task> wrappedAction)
		{
			PostSaveChangesSaveActionDictionary[entity] = wrappedAction;

			if (Log.IsEnabled(LogLevel.Debug))
				Log.WriteDebug(message: "Post save callback registered");
		}

		public virtual async Task ExecutePostSaveChangesActionsAsync(CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (Log.IsEnabled(LogLevel.Debug))
				Log.WriteDebug(new { StartPostSaveChangesActionsCount = PostSaveChangesSaveActionDictionary.Count }, "Started executing post save callbacks");

			// Firstly, create a copy of the callback dictionary and iterate over this
			var dicItem = PostSaveChangesSaveActionDictionary.ToDictionary(x => x.Key, x => x.Value);

			// Now clear the original dictionary so that if any of the callbacks makes a call to SaveChanges we don't end up
			// with infinite recursion.
			PostSaveChangesSaveActionDictionary.Clear();

			// There is the potential that if this code is being executed whilst
			// delegates are still being registered that this will throw up an error.
			// Realistically though I can't see this happening. Not worth building in locking
			// because of the overheads unless we encounter problems.
			foreach (var func in dicItem.Values)
			{
				Task? task = func?.Invoke(cancellationToken);

				if (task != null)
				{
					if (Log.IsEnabled(LogLevel.Debug))
						Log.WriteDebug(message: "Post save callback found to execute");

					await task.ConfigureAwait(false);
				}
			}

			if (Log.IsEnabled(LogLevel.Debug))
				Log.WriteDebug(new { EndPostSaveChangesActionsCount = PostSaveChangesSaveActionDictionary.Count }, "Finished executing post save callbacks");
		}

		public int SaveChanges(Func<int> baseSaveChanges)
		{
			try
			{
				if (Log.IsEnabled(LogLevel.Debug))
					Log.WriteDebug(message: "Started SaveChanges()");

				int result = baseSaveChanges();

				// Run this on a thread pool thread to ensure when this is executed where we have an available
				// SynchronizationContext that it does not cause deadlock
				var t = Task.Run(() => ExecutePostSaveChangesActionsAsync());
				t.Wait();

				if (Log.IsEnabled(LogLevel.Debug))
					Log.WriteDebug(message: "Finished SaveChanges()");

				return result;
			}
			catch (Exception exc) when (Log.WriteError(exc))
			{
				throw;
			}
		}

		public async Task<int> SaveChangesAsync(Func<CancellationToken, Task<int>> baseSaveChangesAsync, CancellationToken cancellationToken = default)
		{
			try
			{
				if (Log.IsEnabled(LogLevel.Debug))
					Log.WriteDebug(message: "Started SaveChangesAsync()");

				int result = await baseSaveChangesAsync(cancellationToken).ConfigureAwait(false);

				await ExecutePostSaveChangesActionsAsync(cancellationToken).ConfigureAwait(false);

				if (Log.IsEnabled(LogLevel.Debug))
					Log.WriteDebug(message: "Finished SaveChangesAsync()");

				return result;
			}
			catch (Exception exc) when (Log.WriteError(exc))
			{
				throw;
			}
		}
	}
}