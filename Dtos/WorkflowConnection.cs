namespace Dtos;

public class WorkflowConnection
{
    public string SourceNodeId { get; set; }
    public string TargetNodeId { get; set; }
    
    // Novas propriedades para mapear o JS
    public string SourceOutput { get; set; } // Mapeia outputKey
    public string TargetInput { get; set; }  // Mapeia conn.output
}