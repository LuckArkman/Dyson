using Akka.Actor;

namespace Records;

public record StartConsensusRound(Guid TaskId, string[] Subtasks, IActorRef Worker);