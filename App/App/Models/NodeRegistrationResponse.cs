namespace Models;

public record NodeRegistrationResponse(string NodeJwt, IEnumerable<string> InitialPeers);