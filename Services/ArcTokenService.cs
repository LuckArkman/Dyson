using System.Numerics;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.ContractHandlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Services;

/// <summary>
/// Serviço para interagir com o contrato ARC-20 na testnet
/// Contrato: 0xDD7Fb93DC67D5715BbF55bAc41d7c9202d8951A7
/// Explorer: https://testnet.arcscan.app/
/// </summary>
public class ArcTokenService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ArcTokenService> _logger;
    private readonly Web3 _web3;
    private readonly Contract _contract;
    private readonly Account _ownerAccount;
    
    // Endereço do contrato ARC-20 na testnet
    private const string CONTRACT_ADDRESS = "0xDD7Fb93DC67D5715BbF55bAc41d7c9202d8951A7";
    
    // ABI simplificada do ERC-20/ARC-20 (funções essenciais)
    private const string CONTRACT_ABI = @"[
        {
            ""constant"": true,
            ""inputs"": [{""name"": ""_owner"", ""type"": ""address""}],
            ""name"": ""balanceOf"",
            ""outputs"": [{""name"": ""balance"", ""type"": ""uint256""}],
            ""type"": ""function""
        },
        {
            ""constant"": false,
            ""inputs"": [
                {""name"": ""_to"", ""type"": ""address""},
                {""name"": ""_value"", ""type"": ""uint256""}
            ],
            ""name"": ""transfer"",
            ""outputs"": [{""name"": """", ""type"": ""bool""}],
            ""type"": ""function""
        },
        {
            ""constant"": true,
            ""inputs"": [],
            ""name"": ""totalSupply"",
            ""outputs"": [{""name"": """", ""type"": ""uint256""}],
            ""type"": ""function""
        },
        {
            ""constant"": true,
            ""inputs"": [],
            ""name"": ""decimals"",
            ""outputs"": [{""name"": """", ""type"": ""uint8""}],
            ""type"": ""function""
        },
        {
            ""constant"": true,
            ""inputs"": [],
            ""name"": ""symbol"",
            ""outputs"": [{""name"": """", ""type"": ""string""}],
            ""type"": ""function""
        },
        {
            ""constant"": true,
            ""inputs"": [],
            ""name"": ""name"",
            ""outputs"": [{""name"": """", ""type"": ""string""}],
            ""type"": ""function""
        },
        {
            ""anonymous"": false,
            ""inputs"": [
                {""indexed"": true, ""name"": ""from"", ""type"": ""address""},
                {""indexed"": true, ""name"": ""to"", ""type"": ""address""},
                {""indexed"": false, ""name"": ""value"", ""type"": ""uint256""}
            ],
            ""name"": ""Transfer"",
            ""type"": ""event""
        }
    ]";

    public ArcTokenService(
        IConfiguration configuration,
        ILogger<ArcTokenService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        // Obter configurações do appsettings.json
        var rpcUrl = _configuration["ArcNetwork:RpcUrl"] ?? "https://testnet-rpc.arcscan.app";
        var privateKey = _configuration["ArcNetwork:PrivateKey"] 
            ?? throw new InvalidOperationException("Private key não configurada em ArcNetwork:PrivateKey");

        // Criar conta e Web3
        _ownerAccount = new Account(privateKey);
        _web3 = new Web3(_ownerAccount, rpcUrl);
        
        // Criar instância do contrato
        _contract = _web3.Eth.GetContract(CONTRACT_ABI, CONTRACT_ADDRESS);
        
        _logger.LogInformation("ArcTokenService inicializado. Contrato: {Contract}", CONTRACT_ADDRESS);
        _logger.LogInformation("Conta do sistema: {Account}", _ownerAccount.Address);
    }

    /// <summary>
    /// Obtém o saldo de tokens de um endereço
    /// </summary>
    public async Task<decimal> GetBalanceAsync(string address)
    {
        try
        {
            var balanceOfFunction = _contract.GetFunction("balanceOf");
            var balance = await balanceOfFunction.CallAsync<BigInteger>(address);
            
            // Converter de Wei para tokens (assumindo 18 decimais)
            var decimals = await GetDecimalsAsync();
            var balanceDecimal = Web3.Convert.FromWei(balance, decimals);
            
            _logger.LogInformation("Saldo de {Address}: {Balance} tokens", address, balanceDecimal);
            return balanceDecimal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter saldo de {Address}", address);
            throw;
        }
    }

    /// <summary>
    /// Transfere tokens para um endereço
    /// </summary>
    public async Task<string> TransferTokensAsync(string toAddress, decimal amount, string reason = "Reward")
    {
        try
        {
            var decimals = await GetDecimalsAsync();
            var amountInWei = Web3.Convert.ToWei(amount, decimals);
            
            var transferFunction = _contract.GetFunction("transfer");
            
            // Estimar gas
            var gasEstimate = await transferFunction.EstimateGasAsync(
                _ownerAccount.Address,
                null,
                null,
                toAddress,
                amountInWei
            );

            // Executar transferência
            var receipt = await transferFunction.SendTransactionAndWaitForReceiptAsync(
                _ownerAccount.Address,
                gasEstimate,
                null,
                null,
                toAddress,
                amountInWei
            );

            if (receipt.Status.Value == 1)
            {
                _logger.LogInformation(
                    "Transferência bem-sucedida! TxHash: {TxHash}, Para: {To}, Valor: {Amount} tokens, Motivo: {Reason}",
                    receipt.TransactionHash,
                    toAddress,
                    amount,
                    reason
                );
                
                return receipt.TransactionHash;
            }
            else
            {
                _logger.LogError("Transação falhou. TxHash: {TxHash}", receipt.TransactionHash);
                throw new Exception($"Transação falhou: {receipt.TransactionHash}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao transferir {Amount} tokens para {Address}", amount, toAddress);
            throw;
        }
    }

    /// <summary>
    /// Obtém o total supply do token
    /// </summary>
    public async Task<decimal> GetTotalSupplyAsync()
    {
        try
        {
            var totalSupplyFunction = _contract.GetFunction("totalSupply");
            var totalSupply = await totalSupplyFunction.CallAsync<BigInteger>();
            
            var decimals = await GetDecimalsAsync();
            return Web3.Convert.FromWei(totalSupply, decimals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter total supply");
            throw;
        }
    }

    /// <summary>
    /// Obtém informações do token
    /// </summary>
    public async Task<TokenInfo> GetTokenInfoAsync()
    {
        try
        {
            var nameFunction = _contract.GetFunction("name");
            var symbolFunction = _contract.GetFunction("symbol");
            var decimalsFunction = _contract.GetFunction("decimals");
            var totalSupplyFunction = _contract.GetFunction("totalSupply");

            var name = await nameFunction.CallAsync<string>();
            var symbol = await symbolFunction.CallAsync<string>();
            var decimals = await decimalsFunction.CallAsync<byte>();
            var totalSupply = await totalSupplyFunction.CallAsync<BigInteger>();

            return new TokenInfo
            {
                Name = name,
                Symbol = symbol,
                Decimals = decimals,
                TotalSupply = Web3.Convert.FromWei(totalSupply, decimals),
                ContractAddress = CONTRACT_ADDRESS
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter informações do token");
            throw;
        }
    }

    /// <summary>
    /// Obtém o número de decimais do token
    /// </summary>
    private async Task<int> GetDecimalsAsync()
    {
        var decimalsFunction = _contract.GetFunction("decimals");
        var decimals = await decimalsFunction.CallAsync<byte>();
        return decimals;
    }

    /// <summary>
    /// Verifica se um endereço é válido
    /// </summary>
    public bool IsValidAddress(string address)
    {
        try
        {
            return address.StartsWith("0x") && address.Length == 42;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Obtém o histórico de transações de um endereço usando eventos Transfer
    /// CORRIGIDO: Await em cada Task separadamente
    /// </summary>
    public async Task<List<TransferEvent>> GetTransferHistoryAsync(string address, ulong fromBlock = 0)
    {
        try
        {
            var transferEvent = _contract.GetEvent("Transfer");
            var decimals = await GetDecimalsAsync();
            
            var blockFrom = new BlockParameter(fromBlock);
            var blockTo = BlockParameter.CreateLatest();
            
            // Buscar transações recebidas (to = address)
            var filterReceived = transferEvent.CreateFilterInput(
                filterTopic1: null,           // from = qualquer um
                filterTopic2: new[] { address },  // to = address específico
                fromBlock: blockFrom,
                toBlock: blockTo
            );

            // Buscar transações enviadas (from = address)
            var filterSent = transferEvent.CreateFilterInput(
                filterTopic1: new[] { address },  // from = address específico
                filterTopic2: null,               // to = qualquer um
                fromBlock: blockFrom,
                toBlock: blockTo
            );

            // CORREÇÃO: Await em cada Task separadamente
            var receivedLogs = await transferEvent.GetAllChangesAsync<TransferEventDTO>(filterReceived);
            var sentLogs = await transferEvent.GetAllChangesAsync<TransferEventDTO>(filterSent);

            var transfers = new List<TransferEvent>();

            // Processar recebimentos
            foreach (var log in receivedLogs)
            {
                transfers.Add(new TransferEvent
                {
                    From = log.Event.From,
                    To = log.Event.To,
                    Amount = Web3.Convert.FromWei(log.Event.Value, decimals),
                    TransactionHash = log.Log.TransactionHash,
                    BlockNumber = log.Log.BlockNumber.Value,
                    Type = "Received"
                });
            }

            // Processar envios
            foreach (var log in sentLogs)
            {
                transfers.Add(new TransferEvent
                {
                    From = log.Event.From,
                    To = log.Event.To,
                    Amount = Web3.Convert.FromWei(log.Event.Value, decimals),
                    TransactionHash = log.Log.TransactionHash,
                    BlockNumber = log.Log.BlockNumber.Value,
                    Type = "Sent"
                });
            }

            return transfers.OrderByDescending(t => t.BlockNumber).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter histórico de transferências de {Address}", address);
            throw;
        }
    }

    /// <summary>
    /// Obtém o preço estimado de gas para uma transferência
    /// </summary>
    public async Task<GasEstimate> EstimateTransferGasAsync(string toAddress, decimal amount)
    {
        try
        {
            var decimals = await GetDecimalsAsync();
            var amountInWei = Web3.Convert.ToWei(amount, decimals);
            
            var transferFunction = _contract.GetFunction("transfer");
            var gasEstimate = await transferFunction.EstimateGasAsync(
                _ownerAccount.Address,
                null,
                null,
                toAddress,
                amountInWei
            );

            var gasPrice = await _web3.Eth.GasPrice.SendRequestAsync();
            var totalCostWei = gasEstimate.Value * gasPrice.Value;
            var totalCostEth = Web3.Convert.FromWei(totalCostWei);

            return new GasEstimate
            {
                GasLimit = gasEstimate.Value,
                GasPrice = gasPrice.Value,
                TotalCostWei = totalCostWei,
                TotalCostEth = totalCostEth
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao estimar gas");
            throw;
        }
    }
}

// DTOs e classes auxiliares

public class TokenInfo
{
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public byte Decimals { get; set; }
    public decimal TotalSupply { get; set; }
    public string ContractAddress { get; set; } = string.Empty;
}

public class TransferEvent
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string TransactionHash { get; set; } = string.Empty;
    public BigInteger BlockNumber { get; set; }
    public string Type { get; set; } = string.Empty;
}

public class GasEstimate
{
    public BigInteger GasLimit { get; set; }
    public BigInteger GasPrice { get; set; }
    public BigInteger TotalCostWei { get; set; }
    public decimal TotalCostEth { get; set; }
}

// DTO para decodificar eventos Transfer
[Event("Transfer")]
public class TransferEventDTO : IEventDTO
{
    [Parameter("address", "from", 1, true)]
    public string From { get; set; } = string.Empty;

    [Parameter("address", "to", 2, true)]
    public string To { get; set; } = string.Empty;

    [Parameter("uint256", "value", 3, false)]
    public BigInteger Value { get; set; }
}