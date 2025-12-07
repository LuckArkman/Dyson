using Akka.Actor;

namespace Records;

public record NodeInfo(
    IActorRef ActorRef,
    string WalletAddress,
    string NetworkAddress,
    HashSet<string> Specializations // As áreas de expertise do nó
);