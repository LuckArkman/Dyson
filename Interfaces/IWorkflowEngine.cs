using Dtos;

namespace Interfaces;

public interface IWorkflowEngine
{
    Task<string> RunWorkflowAsync(SmartAgent agent, string initialPayload = "{}");
}