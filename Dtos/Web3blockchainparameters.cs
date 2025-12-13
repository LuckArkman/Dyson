namespace Dtos;

/// <summary>
/// Parâmetros base para todos os blocos
/// </summary>
public abstract class BaseNodeParameters
{
    public string Description { get; set; }
    public bool Enabled { get; set; } = true;
}

// ==================== WEB3 & BLOCKCHAIN ====================

/// <summary>
/// Parâmetros para conexão com blockchain
/// </summary>
public class BlockchainConnectParameters : BaseNodeParameters
{
    public string Network { get; set; } // Ethereum, BNB, Polygon, Solana
    public string RpcUrl { get; set; }
    public string ChainId { get; set; }
    public string ApiKey { get; set; }
}

/// <summary>
/// Parâmetros para gerenciamento de carteira digital
/// </summary>
public class WalletManagerParameters : BaseNodeParameters
{
    public string WalletAddress { get; set; }
    public string PrivateKey { get; set; } // Deve ser criptografado
    public bool ScanTokens { get; set; } = true;
    public bool ScanNFTs { get; set; } = false;
}

/// <summary>
/// Parâmetros para leitura de contratos inteligentes
/// </summary>
public class SmartContractReadParameters : BaseNodeParameters
{
    public string ContractAddress { get; set; }
    public string ABI { get; set; }
    public string Method { get; set; }
    public Dictionary<string, object> MethodParameters { get; set; } = new();
}

/// <summary>
/// Parâmetros para envio de transações
/// </summary>
public class TransactionSendParameters : BaseNodeParameters
{
    public string To { get; set; }
    public string Value { get; set; }
    public string GasLimit { get; set; }
    public string Data { get; set; }
    public bool WaitForConfirmation { get; set; } = true;
}

/// <summary>
/// Parâmetros para obtenção de preços de criptomoedas
/// </summary>
public class CryptoPriceDataParameters : BaseNodeParameters
{
    public string Symbol { get; set; }
    public string Currency { get; set; } = "USD";
    public string Interval { get; set; } // 1m, 5m, 1h, 1d
    public string Source { get; set; } // CoinGecko, Binance, CoinMarketCap
}

/// <summary>
/// Parâmetros para monitoramento de liquidez em DEXs
/// </summary>
public class DEXLiquidityParameters : BaseNodeParameters
{
    public string DexName { get; set; } // Uniswap, PancakeSwap, SushiSwap
    public string Pair { get; set; }
    public string Pool { get; set; }
}

/// <summary>
/// Parâmetros para detecção de whales
/// </summary>
public class WhaleDetectorParameters : BaseNodeParameters
{
    public decimal MinAmount { get; set; }
    public List<string> Tokens { get; set; } = new();
    public string AlertChannel { get; set; } // Telegram, Discord, Email
}

/// <summary>
/// Parâmetros para geração de sinais de trading
/// </summary>
public class TradingSignalParameters : BaseNodeParameters
{
    public string Strategy { get; set; } // SMA, RSI, MACD, Bollinger
    public List<string> Indicators { get; set; } = new();
    public string Timeframe { get; set; }
}

/// <summary>
/// Parâmetros para trading automatizado
/// </summary>
public class AutoTradingParameters : BaseNodeParameters
{
    public string Exchange { get; set; }
    public string Pair { get; set; }
    public decimal Amount { get; set; }
    public decimal? StopLoss { get; set; }
    public decimal? TakeProfit { get; set; }
    public string OrderType { get; set; } // Market, Limit
}

/// <summary>
/// Parâmetros para rebalanceamento de portfolio
/// </summary>
public class PortfolioRebalanceParameters : BaseNodeParameters
{
    public Dictionary<string, decimal> Allocations { get; set; } = new();
    public string Frequency { get; set; } // Daily, Weekly, Monthly
    public decimal Threshold { get; set; } = 5m; // Percentual
}

/// <summary>
/// Parâmetros para análise de risco
/// </summary>
public class RiskAnalyzerParameters : BaseNodeParameters
{
    public List<string> Metrics { get; set; } = new(); // VaR, Sharpe, Volatility
    public int TimeWindow { get; set; } = 30; // Dias
    public string AlertLevel { get; set; } // Low, Medium, High
}

/// <summary>
/// Parâmetros para detecção de anomalias em blockchain
/// </summary>
public class BlockchainAnomalyParameters : BaseNodeParameters
{
    public string PatternType { get; set; }
    public decimal Sensitivity { get; set; } = 0.8m;
    public string Notification { get; set; }
}

/// <summary>
/// Parâmetros para predição de tendências
/// </summary>
public class TrendPredictorParameters : BaseNodeParameters
{
    public string Model { get; set; } // LSTM, Prophet, ARIMA
    public List<string> Features { get; set; } = new();
    public int Horizon { get; set; } // Dias para frente
}