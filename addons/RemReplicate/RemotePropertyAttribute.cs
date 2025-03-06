#nullable enable

using System;

namespace RemReplicate;

/// <summary>
/// Marks the property in an <see cref="Entity"/> for remote replication.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class RemotePropertyAttribute : Attribute;