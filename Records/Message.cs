using System.Text.Json.Serialization;
using System;

namespace Records;

[JsonDerivedType(typeof(JoinRequest), typeDiscriminator: "join_request")]
[JsonDerivedType(typeof(JoinResponse), typeDiscriminator: "join_response")]
[JsonDerivedType(typeof(ForwardJoinRequest), typeDiscriminator: "forward_join_request")]
[JsonDerivedType(typeof(PingRequest), typeDiscriminator: "ping_request")]
[JsonDerivedType(typeof(PongResponse), typeDiscriminator: "pong_response")]
[JsonDerivedType(typeof(GossipSyncRequest), typeDiscriminator: "gossip_sync_request")]
[JsonDerivedType(typeof(GossipSyncResponse), typeDiscriminator: "gossip_sync_response")]
[JsonDerivedType(typeof(AuthRequest), typeDiscriminator: "auth_request")]
[JsonDerivedType(typeof(AuthResponse), typeDiscriminator: "auth_response")]
[JsonDerivedType(typeof(HelloRequest), typeDiscriminator: "hello_request")]

public abstract record _Message
{
    [JsonConstructor] 
    public _Message() {} 
    public _Message(Guid correlationId)
    {
        CorrelationId = correlationId;
    }

    public Guid CorrelationId { get; set; } 
}

// A definição de HelloRequest no final do arquivo parece estar correta:
public record HelloRequest : _Message
{
    public string Content { get; set; }
    
    [JsonConstructor]
    public HelloRequest(Guid correlationId, string content) : base(correlationId)
    {
        Content = content;
    }
    
    // Construtor adicional para facilitar criação manual
    public HelloRequest(string content) : base(Guid.NewGuid())
    {
        Content = content;
    }
}