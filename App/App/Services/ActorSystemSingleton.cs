using Akka.Actor;

namespace Services;

public class ActorSystemSingleton
{
    public ActorSystem? ActorSystem { get; set; }
}