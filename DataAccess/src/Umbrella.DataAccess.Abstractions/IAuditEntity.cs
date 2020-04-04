﻿namespace Umbrella.DataAccess.Abstractions
{
	/// <summary>
	/// Adds support to an entity type for storing it's unique Id, the id of the user it was both created by and last updated by, together with
	/// support for storing the date it was created and last updated.
	/// This is a convenience interface that implements <see cref="IEntity{T}"/>, <see cref="ICreatedDateAuditEntity"/>, <see cref="ICreatedUserAuditEntity{T}"/>, <see cref="IUpdatedDateAuditEntity"/> and <see cref="IUpdatedUserAuditEntity{T}"/>.
	/// </summary>
	/// <typeparam name="TEntityKey">The type of the user id.</typeparam>
	public interface IAuditEntity<TEntityKey> : IEntity<TEntityKey>, ICreatedDateAuditEntity, ICreatedUserAuditEntity<TEntityKey>, IUpdatedDateAuditEntity, IUpdatedUserAuditEntity<TEntityKey>
	{
	}
}