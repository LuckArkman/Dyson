using Application.Models;
using Dtos;

namespace Interfaces;

/// <summary>
/// Interface para autenticação Web3
/// </summary>
public interface IWeb3AuthService
{
    /// <summary>
    /// Gera um nonce único para assinatura
    /// </summary>
    string GenerateNonce(string walletAddress);

    /// <summary>
    /// Verifica se a assinatura é válida
    /// </summary>
    bool VerifySignature(string message, string signature, string walletAddress);

    /// <summary>
    /// Autentica ou registra usuário via carteira
    /// </summary>
    Task<User> AuthenticateWithWallet(string walletAddress, string signature, string message);

    /// <summary>
    /// Vincula carteira a usuário existente
    /// </summary>
    Task<bool> LinkWalletToUser(string userId, string walletAddress, string signature, string message);

    /// <summary>
    /// Busca usuário pelo endereço da carteira
    /// </summary>
    Task<User?> GetUserByWalletAddress(string walletAddress);

    /// <summary>
    /// Valida endereço Ethereum
    /// </summary>
    bool IsValidEthereumAddress(string address);

    /// <summary>
    /// Gera mensagem para assinatura
    /// </summary>
    string GenerateSignatureMessage(string walletAddress, string action = "login");
}