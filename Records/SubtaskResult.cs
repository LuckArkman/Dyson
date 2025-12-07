using Akka.Actor;

namespace Records;

public record SubtaskResult(Guid TaskId, int SubtaskIndex, string Fragment, IActorRef Node);