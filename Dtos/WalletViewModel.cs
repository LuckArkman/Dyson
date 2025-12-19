namespace Dtos;

/// <summary>
/// ViewModel para a página Wallet com todas as informações necessárias
/// </summary>
public class WalletViewModel
{
    /// <summary>
    /// Endereço da carteira Web3 do usuário
    /// </summary>
    public string WalletAddress { get; set; }
    
    /// <summary>
    /// Saldo total disponível de tokens (Balance é usado pelo código antigo)
    /// </summary>
    public decimal Balance { get; set; }
    
    /// <summary>
    /// Saldo disponível para trading (igual a Balance)
    /// </summary>
    public decimal TokenBalance
    {
        get => Balance;
        set => Balance = value;
    }

    /// <summary>
    /// Tokens bloqueados em staking
    /// </summary>
    public decimal StakedBalance { get; set; }
    
    /// <summary>
    /// Histórico de transações
    /// </summary>
    public List<TransactionDocument> History { get; set; }
    
    /// <summary>
    /// Preço atual do token em BRL
    /// </summary>
    public decimal CurrentTokenPrice { get; set; }
    
    /// <summary>
    /// Valor total da carteira em BRL (Balance * CurrentTokenPrice)
    /// </summary>
    public decimal TokenValue
    {
        get => Balance * CurrentTokenPrice;
        set => Balance = value / CurrentTokenPrice;
    }

    /// <summary>
    /// Construtor padrão inicializando lista de histórico
    /// </summary>
    public WalletViewModel()
    {
        History = new List<TransactionDocument>();
        WalletAddress = string.Empty;
    }
}