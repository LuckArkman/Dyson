using Jint;
using Jint.Runtime;
using Microsoft.EntityFrameworkCore.Storage;

namespace Core;

public class CodeNodeExecutor : INodeExecutor
{
    // ...
    public Task<ExecutionResult> ExecuteAsync(...)
    {
        // ...
        
        // CONFIGURAÇÃO DE SEGURANÇA
        var engine = new Engine(cfg => cfg
                .LimitMemory(4_000_000) // Máximo 4MB de alocação
                .LimitRecursion(20)     // Evita StackOverflow
                .TimeoutInterval(TimeSpan.FromSeconds(2)) // Máximo 2s de execução
        );

        try 
        {
            // Bloqueia acesso a .NET Framework (System.IO, etc)
            // Apenas lógica pura JS é permitida
            
            // Passa inputs
            engine.SetValue("$input", inputData.ToJsonString());
            
            // Executa script
            var result = engine.Evaluate($"(function(){{ {script} }})()");
            
            // ... conversão de retorno ...
        }
        catch (TimeoutException)
        {
            throw new Exception("O script demorou muito para responder (Timeout de 2s).");
        }
        catch (MemoryLimitExceededException)
        {
            throw new Exception("O script consumiu muita memória.");
        }
        catch (JavaScriptException ex)
        {
            throw new Exception($"Erro no código JS: {ex.Message} (Linha {ex.Location.Start.Line})");
        }
    }
}