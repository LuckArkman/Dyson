using Microsoft.AspNetCore.Mvc;
using Services;

namespace Controllers;

[Route("approve")]
public class ApprovalController : Controller
{
    [HttpGet("{token}")]
    public async Task<IActionResult> Approve(string token)
    {
        // 1. Busca a suspensão
        var suspension = await _db.Suspensions.Find(x => x.Id == token).FirstOrDefaultAsync();
        
        // 2. Retoma a Engine (Carrega o estado salvo e continua do próximo nó)
        _backgroundJobClient.Enqueue<WorkflowEngine>(x => x.ResumeWorkflow(suspension));

        return View("ApprovalSuccess");
    }
}