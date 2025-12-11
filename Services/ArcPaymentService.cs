using System.Numerics;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Dtos;
using Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Repositories;

namespace Services;

/// <summary>
/// Serviço de pagamento usando Arc blockchain da Circle
/// Arc usa USDC nativo como gas token e oferece finalidade sub-segundo
/// Documentação: https://docs.arc.network
/// </summary>
public class ArcPaymentService : IPaymentGateway
{
    private readonly ILogger<ArcPaymentService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IRepositorio<Order> _orderRepo;
    private readonly Web3 _web3;
    private readonly string _recipientAddress;
    private readonly bool _isTestnet;

    // USDC é o token nativo em Arc (usado para gas e pagamentos)
    private const string USDC_NATIVE = "0x0000000000000000000000000000000000000000"; // Native token

    public ArcPaymentService(
        IConfiguration configuration,
        ILogger<ArcPaymentService> logger,
        IRepositorio<Order> orderRepo)
    {
        _logger = logger;
        _configuration = configuration;
        _orderRepo = orderRepo;

        // Configurações Arc
        var rpcUrl = configuration["Arc:RpcUrl"] ?? "https://arc-testnet.drpc.org";
        _recipientAddress = configuration["Arc:RecipientAddress"] 
            ?? throw new ArgumentNullException("Arc:RecipientAddress");
        _isTestnet = bool.Parse(configuration["Arc:IsTestnet"] ?? "true");

        // Inicializar Web3 (Arc é EVM-compatible)
        _web3 = new Web3(rpcUrl);
        
        _logger.LogInformation($"Arc Payment Service inicializado - Testnet: {_isTestnet}");
    }

    public async Task<PaymentResponse> CreatePaymentAsync(Order order, string payerDocument, string paymentMethod)
    {
        try
        {
            _logger.LogInformation($"Criando pagamento Arc para Order {order.Id}");

            // Arc aguarda transação do usuário via MetaMask
            return new PaymentResponse
            {
                Success = true,
                TransactionId = order.Id,
                Message = "Aguardando pagamento USDC via MetaMask",
                Details = new PaymentDetails
                {
                    PaymentMethod = "USDC",
                    Status = "pending",
                    RecipientAddress = _recipientAddress,
                    Network = _isTestnet ? "Arc Testnet" : "Arc Mainnet"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar pagamento Arc");
            return new PaymentResponse
            {
                Success = false,
                Message = $"Erro interno: {ex.Message}"
            };
        }
    }

    public async Task<PaymentInfo?> GetPaymentAsync(string transactionId)
    {
        try
        {
            // Buscar ordem no banco
            var order = await _orderRepo.GetByIdAsync(transactionId);
            
            if (order == null)
            {
                return new PaymentInfo
                {
                    Success = false,
                    Message = "Pedido não encontrado"
                };
            }

            // Se tiver hash de transação, verificar na blockchain
            if (!string.IsNullOrEmpty(order.BlockchainTxHash))
            {
                var receipt = await _web3.Eth.Transactions.GetTransactionReceipt
                    .SendRequestAsync(order.BlockchainTxHash);
                
                if (receipt != null)
                {
                    return new PaymentInfo
                    {
                        Success = true,
                        Amount = order.TotalAmount,
                        Status = order.Status,
                        TransactionId = order.BlockchainTxHash,
                        Details = new PaymentDetails
                        {
                            PaymentMethod = "USDC",
                            Status = order.Status.ToLower(),
                            BlockNumber = receipt.BlockNumber.Value.ToString(),
                            ConfirmedAt = order.BlockchainConfirmedAt,
                            GasFee = order.GasFee
                        }
                    };
                }
            }

            return new PaymentInfo
            {
                Success = true,
                Amount = order.TotalAmount,
                Status = order.Status,
                TransactionId = transactionId,
                Details = new PaymentDetails
                {
                    PaymentMethod = "USDC",
                    Status = order.Status.ToLower()
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar pagamento");
            return new PaymentInfo
            {
                Success = false,
                Message = $"Erro ao consultar: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Verifica uma transação na blockchain Arc
    /// </summary>
    public async Task<TransactionReceipt?> GetTransactionReceiptAsync(string txHash)
    {
        try
        {
            var receipt = await _web3.Eth.Transactions.GetTransactionReceipt
                .SendRequestAsync(txHash);
            return receipt;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao consultar transação {txHash}");
            return null;
        }
    }

    /// <summary>
    /// Verifica se uma transferência de USDC foi realizada corretamente
    /// Em Arc, USDC é usado como gas, então é uma transferência nativa
    /// </summary>
    public async Task<bool> VerifyUsdcTransferAsync(
        string txHash,
        string expectedReceiver,
        decimal expectedAmount)
    {
        try
        {
            var receipt = await GetTransactionReceiptAsync(txHash);
            
            if (receipt == null)
            {
                _logger.LogWarning($"Transação não encontrada: {txHash}");
                return false;
            }

            // Verificar se transação foi bem-sucedida
            if (receipt.Status.Value != 1)
            {
                _logger.LogError($"Transação falhou: {txHash}");
                return false;
            }

            // Obter detalhes da transação
            var transaction = await _web3.Eth.Transactions.GetTransactionByHash
                .SendRequestAsync(txHash);

            if (transaction == null)
            {
                _logger.LogError($"Detalhes da transação não encontrados: {txHash}");
                return false;
            }

            // Verificar destinatário
            if (transaction.To?.ToLower() != expectedReceiver.ToLower())
            {
                _logger.LogWarning(
                    $"Destinatário incorreto. Esperado: {expectedReceiver}, Recebido: {transaction.To}");
                return false;
            }

            // Verificar valor (USDC tem 6 decimais em Arc)
            var valueInUsdc = Web3.Convert.FromWei(transaction.Value.Value, 6);
            var difference = Math.Abs(valueInUsdc - expectedAmount);
            
            if (difference > 0.01m) // Tolerância de 1 centavo
            {
                _logger.LogWarning(
                    $"Valor incorreto. Esperado: {expectedAmount}, Recebido: {valueInUsdc}");
                return false;
            }

            _logger.LogInformation(
                $"Transferência verificada: {valueInUsdc} USDC para {transaction.To}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar transferência");
            return false;
        }
    }

    /// <summary>
    /// Confirma um pagamento após verificação na blockchain
    /// </summary>
    public async Task<bool> ConfirmPaymentAsync(string orderId, string txHash)
    {
        try
        {
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null)
            {
                _logger.LogError($"Ordem não encontrada: {orderId}");
                return false;
            }

            // Verificar a transação
            var verified = await VerifyUsdcTransferAsync(
                txHash,
                _recipientAddress,
                order.TotalAmount
            );

            if (!verified)
            {
                _logger.LogError($"Falha na verificação da transação: {txHash}");
                return false;
            }

            // Obter detalhes da transação
            var receipt = await GetTransactionReceiptAsync(txHash);
            if (receipt == null)
                return false;

            var transaction = await _web3.Eth.Transactions.GetTransactionByHash
                .SendRequestAsync(txHash);

            // Calcular gas fee (em USDC, não ETH!)
            var gasFee = CalculateGasFee(receipt, transaction);

            // Atualizar ordem
            order.Status = "Paid";
            order.BlockchainTxHash = txHash;
            order.BlockchainNetwork = _isTestnet ? "Arc Testnet" : "Arc Mainnet";
            order.BlockNumber = (long)receipt.BlockNumber.Value;
            order.BlockchainConfirmedAt = DateTime.UtcNow;
            order.GasFee = gasFee;
            order.UpdatedAt = DateTime.UtcNow;

            await _orderRepo.UpdateAsync(orderId, order);

            _logger.LogInformation($"Pagamento confirmado para ordem {orderId}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao confirmar pagamento");
            return false;
        }
    }

    /// <summary>
    /// Calcula a taxa de gas em USDC (não em ETH!)
    /// Arc usa USDC como gas token, então o cálculo é diferente
    /// </summary>
    public decimal CalculateGasFee(TransactionReceipt receipt, Transaction transaction)
    {
        try
        {
            // Gas usado
            var gasUsed = receipt.GasUsed.Value;
            
            // Gas price (em gwei/USDC)
            var gasPrice = transaction.GasPrice.Value;
            
            // Total em wei (ou micro-USDC)
            var totalWei = gasUsed * gasPrice;
            
            // Converter para USDC (6 decimais)
            var gasFeeUsdc = Web3.Convert.FromWei(totalWei, 6);
            
            return gasFeeUsdc;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao calcular gas fee");
            return 0;
        }
    }

    /// <summary>
    /// Obtém o saldo USDC de uma conta
    /// </summary>
    public async Task<decimal> GetUsdcBalanceAsync(string address)
    {
        try
        {
            var balance = await _web3.Eth.GetBalance.SendRequestAsync(address);
            
            // Converter de Wei para USDC (6 decimais)
            var balanceUsdc = Web3.Convert.FromWei(balance.Value, 6);
            
            return balanceUsdc;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao consultar saldo de {address}");
            return 0;
        }
    }

    /// <summary>
    /// Obtém informações da rede Arc
    /// </summary>
    public async Task<ArcNetworkInfo> GetNetworkInfoAsync()
    {
        try
        {
            var chainId = await _web3.Eth.ChainId.SendRequestAsync();
            var blockNumber = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var gasPrice = await _web3.Eth.GasPrice.SendRequestAsync();

            return new ArcNetworkInfo
            {
                ChainId = (long)chainId.Value,
                BlockNumber = (long)blockNumber.Value,
                GasPrice = Web3.Convert.FromWei(gasPrice.Value, 6),
                IsTestnet = _isTestnet,
                NetworkName = _isTestnet ? "Arc Testnet" : "Arc Mainnet"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter informações da rede");
            return null;
        }
    }

    /// <summary>
    /// Estima o gas para uma transação
    /// </summary>
    public async Task<decimal> EstimateGasFeeAsync(string from, string to, decimal amount)
    {
        try
        {
            var amountWei = Web3.Convert.ToWei(amount, 6);
            
            var gasEstimate = await _web3.Eth.TransactionManager.EstimateGasAsync(
                new TransactionInput
                {
                    From = from,
                    To = to,
                    Value = new HexBigInteger(amountWei)
                }
            );

            var gasPrice = await _web3.Eth.GasPrice.SendRequestAsync();
            var totalGas = gasEstimate.Value * gasPrice.Value;
            
            return Web3.Convert.FromWei(totalGas, 6);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao estimar gas");
            return 0.001m; // Retorna estimativa padrão
        }
    }

    /// <summary>
    /// Verifica o status de uma transação (pending ou confirmed)
    /// </summary>
    public async Task<string> GetTransactionStatusAsync(string txHash)
    {
        try
        {
            var receipt = await GetTransactionReceiptAsync(txHash);
            
            if (receipt == null)
                return "pending";
            
            return receipt.Status.Value == 1 ? "confirmed" : "failed";
        }
        catch
        {
            return "unknown";
        }
    }
}