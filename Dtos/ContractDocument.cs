using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dtos;

/// <summary>
/// Modelo de contrato deployado usando thirdweb 4.0 API
/// Armazenado no MongoDB para rastreamento e gerenciamento
/// </summary>
public class ContractDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Endereço da wallet do usuário dono do contrato
    /// </summary>
    [BsonElement("walletAddress")]
    public string WalletAddress { get; set; }

    /// <summary>
    /// ID do usuário no sistema (referência)
    /// </summary>
    [BsonElement("userId")]
    public string UserId { get; set; }

    // === INFORMAÇÕES BÁSICAS DO CONTRATO ===
    
    /// <summary>
    /// Endereço do contrato na blockchain
    /// </summary>
    [BsonElement("contractAddress")]
    public string ContractAddress { get; set; }

    /// <summary>
    /// Nome do contrato
    /// </summary>
    [BsonElement("contractName")]
    public string ContractName { get; set; }

    /// <summary>
    /// Símbolo do token (se aplicável - ERC-20/721/1155)
    /// </summary>
    [BsonElement("symbol")]
    public string Symbol { get; set; }

    /// <summary>
    /// Descrição do contrato
    /// </summary>
    [BsonElement("description")]
    public string Description { get; set; }

    // === TIPO E CATEGORIA ===
    
    /// <summary>
    /// Tipo de contrato: erc20, erc721, erc1155, nft, marketplace, dao, custom
    /// </summary>
    [BsonElement("contractType")]
    public string ContractType { get; set; }

    /// <summary>
    /// Categoria do contrato para organização
    /// </summary>
    [BsonElement("category")]
    public string Category { get; set; }

    // === INFORMAÇÕES DA BLOCKCHAIN ===
    
    /// <summary>
    /// Blockchain onde foi deployado: ethereum, base, sepolia, base-sepolia, polygon, arbitrum, optimism
    /// </summary>
    [BsonElement("blockchain")]
    public string Blockchain { get; set; }

    /// <summary>
    /// Chain ID da blockchain
    /// </summary>
    [BsonElement("chainId")]
    public int ChainId { get; set; }

    /// <summary>
    /// É uma testnet?
    /// </summary>
    [BsonElement("isTestnet")]
    public bool IsTestnet { get; set; }

    // === STATUS E DEPLOYMENT ===
    
    /// <summary>
    /// Status do contrato: active, pending, failed, paused, archived
    /// </summary>
    [BsonElement("status")]
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Modo de deployment: api, factory, simulated
    /// </summary>
    [BsonElement("deploymentMode")]
    public string DeploymentMode { get; set; }

    /// <summary>
    /// Versão da API do thirdweb usada
    /// </summary>
    [BsonElement("apiVersion")]
    public string ApiVersion { get; set; } = "v1";

    // === CUSTOS E TRANSAÇÃO ===
    
    /// <summary>
    /// Custo pago pelo deploy (em token nativo da rede)
    /// </summary>
    [BsonElement("deploymentCost")]
    public decimal DeploymentCost { get; set; }

    /// <summary>
    /// Gas usado no deploy
    /// </summary>
    [BsonElement("gasUsed")]
    public decimal GasUsed { get; set; }

    /// <summary>
    /// Preço do gas (em Gwei)
    /// </summary>
    [BsonElement("gasPrice")]
    public decimal GasPrice { get; set; }

    /// <summary>
    /// Hash da transação de deploy
    /// </summary>
    [BsonElement("transactionHash")]
    public string TransactionHash { get; set; }

    /// <summary>
    /// Número do bloco onde foi deployado
    /// </summary>
    [BsonElement("blockNumber")]
    public long BlockNumber { get; set; }

    // === RECURSOS DO THIRDWEB 4.0 ===
    
    /// <summary>
    /// Se o contrato usa Account Abstraction (Smart Wallet)
    /// </summary>
    [BsonElement("usesAccountAbstraction")]
    public bool UsesAccountAbstraction { get; set; }

    /// <summary>
    /// Se tem gas sponsorship habilitado
    /// </summary>
    [BsonElement("hasGasSponsorship")]
    public bool HasGasSponsorship { get; set; }

    /// <summary>
    /// Se tem session keys configuradas
    /// </summary>
    [BsonElement("hasSessionKeys")]
    public bool HasSessionKeys { get; set; }

    /// <summary>
    /// Se foi criado com pool de liquidez (para ERC-20)
    /// </summary>
    [BsonElement("hasLiquidityPool")]
    public bool HasLiquidityPool { get; set; }

    /// <summary>
    /// Endereço do pool de liquidez (Uniswap V3)
    /// </summary>
    [BsonElement("liquidityPoolAddress")]
    public string LiquidityPoolAddress { get; set; }

    /// <summary>
    /// Se suporta pagamentos x402
    /// </summary>
    [BsonElement("supportsX402")]
    public bool SupportsX402 { get; set; }

    // === METADADOS E CONFIGURAÇÕES ===
    
    /// <summary>
    /// Supply total (para tokens fungíveis)
    /// </summary>
    [BsonElement("totalSupply")]
    public decimal? TotalSupply { get; set; }

    /// <summary>
    /// Decimais do token (geralmente 18)
    /// </summary>
    [BsonElement("decimals")]
    public int? Decimals { get; set; }

    /// <summary>
    /// URL do contrato no block explorer
    /// </summary>
    [BsonElement("explorerUrl")]
    public string ExplorerUrl { get; set; }

    /// <summary>
    /// URL da API do thirdweb para este contrato
    /// </summary>
    [BsonElement("thirdwebApiUrl")]
    public string ThirdwebApiUrl { get; set; }

    /// <summary>
    /// Metadados adicionais do contrato (JSON)
    /// </summary>
    [BsonElement("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();

    // === ANALYTICS ===
    
    /// <summary>
    /// Número de transações no contrato
    /// </summary>
    [BsonElement("transactionCount")]
    public int TransactionCount { get; set; }

    /// <summary>
    /// Número de holders únicos (para tokens)
    /// </summary>
    [BsonElement("uniqueHolders")]
    public int UniqueHolders { get; set; }

    /// <summary>
    /// Volume total transacionado
    /// </summary>
    [BsonElement("totalVolume")]
    public decimal TotalVolume { get; set; }

    /// <summary>
    /// Última interação com o contrato
    /// </summary>
    [BsonElement("lastInteraction")]
    public DateTime? LastInteraction { get; set; }

    // === SEGURANÇA E VERIFICAÇÃO ===
    
    /// <summary>
    /// Se o contrato está verificado no explorer
    /// </summary>
    [BsonElement("isVerified")]
    public bool IsVerified { get; set; }

    /// <summary>
    /// Se passou por auditoria de segurança
    /// </summary>
    [BsonElement("isAudited")]
    public bool IsAudited { get; set; }

    /// <summary>
    /// Pontuação de segurança (0-100)
    /// </summary>
    [BsonElement("securityScore")]
    public int SecurityScore { get; set; }

    // === DATAS ===
    
    /// <summary>
    /// Data de criação do registro
    /// </summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Última atualização
    /// </summary>
    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Data do deployment na blockchain
    /// </summary>
    [BsonElement("deployedAt")]
    public DateTime? DeployedAt { get; set; }

    // === NOTAS E LOGS ===
    
    /// <summary>
    /// Notas do usuário sobre o contrato
    /// </summary>
    [BsonElement("notes")]
    public string Notes { get; set; }

    /// <summary>
    /// Log de eventos importantes
    /// </summary>
    [BsonElement("eventLog")]
    public List<ContractEvent> EventLog { get; set; } = new();

    /// <summary>
    /// Tags para organização
    /// </summary>
    [BsonElement("tags")]
    public List<string> Tags { get; set; } = new();
}