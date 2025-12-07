namespace Records;

public record NodeStatusDto(
    string Id, 
    string Address, 
    int KnownPeersCount,
    List<string> KnownPeers
);