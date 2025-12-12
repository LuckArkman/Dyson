namespace Dtos;

public class WorkflowConnection
{
    public string SourceNodeId { get; set; }
    public string TargetNodeId { get; set; }
    public int SourceOutputIndex { get; set; }
}