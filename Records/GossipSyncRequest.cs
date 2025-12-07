namespace Records;

public record GossipSyncRequest(Guid CorrelationId, List<string> KnownPeers) : _Message;