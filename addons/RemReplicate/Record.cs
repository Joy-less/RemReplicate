#nullable enable
#pragma warning disable IDE0130

using System;
using ImmediateReflection;

namespace RemReplicate;

public abstract record Record {
    public Guid Id { get; set; } = Guid.NewGuid();

    public string GetRecordType() {
        return Replicator.GetEntityTypeFromTypeOfRecord(this.GetImmediateType());
    }
}