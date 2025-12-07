namespace Records;

public record NodeRegistrationResponse(string NodeJwt, IEnumerable<string> InitialPeers);