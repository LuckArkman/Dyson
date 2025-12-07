namespace Records;

public record ConsensusReached(Guid TaskId, List<SubtaskResult> ValidatedFragments);