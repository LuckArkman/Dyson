namespace Records;

public record GossipSyncResponse(Guid CorrelationId, List<string> KnownPeers) : _Message;