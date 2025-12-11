using Dtos;

namespace Models;

/// <summary>
/// ViewModel base para checkout Web3/Blockchain
/// </summary>
public class Web3CheckoutViewModel
{
    public List<OrderItem> CartItems { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public string RecipientAddress { get; set; } = "";
    public string ChainId { get; set; } = "";
    public string Network { get; set; } = "";
    public bool IsTestnet { get; set; } = true;
    public string ExplorerUrl { get; set; } = "";
    
    // Campos opcionais para diferentes blockchains
    public string? UsdcContractAddress { get; set; }
    public long? UsdcAssetId { get; set; }
    public string? RpcUrl { get; set; }
}

// ==================== ARC VIEWMODELS ====================

/// <summary>
/// ViewModel específico para checkout Arc
/// </summary>
public class ArcCheckoutViewModel : Web3CheckoutViewModel
{
    public string UsdcNativeInfo { get; set; } = "USDC is used as native gas token";
    public decimal EstimatedGasFee { get; set; } = 0.01m; // ~$0.01 in USDC
    
    public ArcCheckoutViewModel()
    {
        Network = "Arc";
        ChainId = "5042002"; // Arc Testnet
        RpcUrl = "https://arc-testnet.drpc.org";
        ExplorerUrl = "https://testnet.arcscan.app";
    }
}

// ==================== ALGORAND VIEWMODELS ====================

/// <summary>
/// ViewModel específico para checkout Algorand
/// </summary>
public class AlgorandCheckoutViewModel : Web3CheckoutViewModel
{
    public string AlgodServer { get; set; } = "";
    public decimal MinimumBalance { get; set; } = 0.1m; // Minimum ALGO balance
    public string FaucetUrl { get; set; } = "";
    
    public AlgorandCheckoutViewModel()
    {
        Network = "Algorand";
        ExplorerUrl = "https://testnet.arcscan.app"; // ou algoexplorer
    }
}

// ==================== ETHEREUM VIEWMODELS ====================

/// <summary>
/// ViewModel para checkout Ethereum/EVM chains
/// </summary>
public class EthereumCheckoutViewModel : Web3CheckoutViewModel
{
    public decimal EstimatedGasFee { get; set; } = 5.0m; // Ethereum gas can be high
    
    public EthereumCheckoutViewModel()
    {
        Network = "Ethereum";
    }
}

// ==================== SUCCESS VIEWMODELS ====================

/// <summary>
/// ViewModel para página de sucesso Web3
/// </summary>
public class Web3SuccessViewModel
{
    public string OrderId { get; set; } = "";
    public string TransactionHash { get; set; } = "";
    public string Network { get; set; } = "";
    public string ExplorerUrl { get; set; } = "";
    public long? BlockNumber { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? GasFee { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    
    /// <summary>
    /// URL completa para visualizar transação no explorer
    /// </summary>
    public string TransactionUrl => $"{ExplorerUrl}/tx/{TransactionHash}";
}

// ==================== TRANSACTION VIEWMODELS ====================

/// <summary>
/// ViewModel para iniciar transação Web3
/// </summary>
public class InitiateWeb3TransactionViewModel
{
    public string OrderId { get; set; } = "";
    public string WalletAddress { get; set; } = "";
    public decimal Amount { get; set; }
    public string RecipientAddress { get; set; } = "";
    public string Network { get; set; } = "";
    public string? ContractAddress { get; set; }
    public long? AssetId { get; set; }
}

/// <summary>
/// ViewModel para status de transação
/// </summary>
public class TransactionStatusViewModel
{
    public string TransactionHash { get; set; } = "";
    public string Status { get; set; } = ""; // pending, confirmed, failed
    public long? BlockNumber { get; set; }
    public int Confirmations { get; set; }
    public DateTime? Timestamp { get; set; }
    public bool IsConfirmed { get; set; }
    public string Network { get; set; } = "";
}

// ==================== CART VIEWMODELS ====================

/// <summary>
/// ViewModel para página do carrinho
/// </summary>
public class CartViewModel
{
    public List<OrderItem> Items { get; set; } = new();
    public decimal SubTotal => Items.Sum(i => i.Price * i.Quantity);
    public decimal Tax { get; set; } = 0;
    public decimal Shipping { get; set; } = 0;
    public decimal Total => SubTotal + Tax + Shipping;
    public int ItemCount => Items.Sum(i => i.Quantity);
}

// ==================== CHECKOUT VIEWMODELS ====================

/// <summary>
/// ViewModel para checkout tradicional (PIX/Boleto/Card)
/// </summary>
public class CheckoutViewModel
{
    public List<OrderItem> CartItems { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = ""; // pix, boleto, card, usdc
    public string? Cpf { get; set; }
    
    // Campos para cartão
    public string? CardName { get; set; }
    public string? CardNumber { get; set; }
    public string? CardExpiry { get; set; }
    public string? CardCvv { get; set; }
}

/// <summary>
/// ViewModel para página de checkout unificada
/// </summary>
public class UnifiedCheckoutViewModel
{
    public List<OrderItem> CartItems { get; set; } = new();
    public decimal TotalAmount { get; set; }
    
    // Opções de pagamento disponíveis
    public bool AllowPix { get; set; } = true;
    public bool AllowBoleto { get; set; } = true;
    public bool AllowCard { get; set; } = true;
    public bool AllowUsdc { get; set; } = true;
    
    // Dados Web3 (se USDC selecionado)
    public Web3CheckoutViewModel? Web3Data { get; set; }
}
