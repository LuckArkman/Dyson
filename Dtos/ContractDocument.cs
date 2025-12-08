using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dtos;

/// <summary>
/// Modelo de contrato deployado para armazenar no MongoDB
/// </summary>
public class ContractDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// ID do usuário dono do contrato
    /// </summary>
    [BsonElement("userId")]
    public string walletAndress { get; set; }

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
    /// Símbolo do token (se aplicável)
    /// </summary>
    [BsonElement("symbol")]
    public string Symbol { get; set; }

    /// <summary>
    /// Tipo de contrato: token, nft, marketplace, etc
    /// </summary>
    [BsonElement("contractType")]
    public string ContractType { get; set; }

    /// <summary>
    /// Blockchain onde foi deployado: sepolia, base-sepolia, etc
    /// </summary>
    [BsonElement("blockchain")]
    public string Blockchain { get; set; }

    /// <summary>
    /// Chain ID da blockchain
    /// </summary>
    [BsonElement("chainId")]
    public int ChainId { get; set; }

    /// <summary>
    /// Status do contrato: Active, Pending, Failed
    /// </summary>
    [BsonElement("status")]
    public string Status { get; set; } = "Active";

    /// <summary>
    /// Custo pago pelo deploy
    /// </summary>
    [BsonElement("deploymentCost")]
    public decimal DeploymentCost { get; set; }

    /// <summary>
    /// Gas usado no deploy (se disponível)
    /// </summary>
    [BsonElement("gasUsed")]
    public decimal GasUsed { get; set; }

    /// <summary>
    /// Hash da transação de deploy
    /// </summary>
    [BsonElement("transactionHash")]
    public string TransactionHash { get; set; }

    /// <summary>
    /// URL do block explorer
    /// </summary>
    [BsonElement("explorerUrl")]
    public string ExplorerUrl { get; set; }

    /// <summary>
    /// Se foi deployado via Factory ou simulação
    /// </summary>
    [BsonElement("deploymentMode")]
    public string DeploymentMode { get; set; } // "factory", "simulated", "api"

    /// <summary>
    /// Data de criação
    /// </summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Última atualização
    /// </summary>
    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Notas adicionais
    /// </summary>
    [BsonElement("notes")]
    public string Notes { get; set; }
}