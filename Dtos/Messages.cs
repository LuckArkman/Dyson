using Akka.Actor;

namespace Dtos;

public class Messages 
{
    // Todos os records de mensagem aqui dentro
    public record ProcessTaskRequest(string Prompt, Guid TaskId);
    public record StartConsensusRound(Guid TaskId, string[] Subtasks, IActorRef Worker, List<object> SelectedNodes);
    public record ProcessSubtask(Guid TaskId, int SubtaskIndex, string Content);
    
    public record SubtaskResult(Guid TaskId, int SubtaskIndex, string Fragment, IActorRef Node);
    public record RequestValidation(Guid TaskId, SubtaskResult FragmentToValidate);
    public record ValidationVote(Guid TaskId, int SubtaskIndex, bool IsValid, IActorRef Voter);
    public record ConsensusReached(Guid TaskId, List<SubtaskResult> ValidatedFragments);
    public record ConsensusFailed(Guid TaskId, string Reason);
    // etc.
}