using Akka.Actor;

namespace Records;

public record ValidationVote(Guid TaskId, int SubtaskIndex, bool IsValid, IActorRef Voter);