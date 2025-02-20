#nullable enable

using System;

namespace RemReplicate;

public abstract record Record {
    public Guid Id { get; set; } = Guid.NewGuid();

    public string GetRecordType() {
        return Replicator.GetEntityTypeFromTypeOfRecord(GetType());
    }
}