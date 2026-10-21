namespace Simulator.InterVpos;

// InterVpos facade handler'ları (3DS) — host Wolverine'de IncludeType. MaviBank/SarıBank çağırır.
public static class InterVposRegistration
{
    public static void IncludeInterVposHandlers(this WolverineOptions opts)
    {
        opts.Discovery.IncludeType(typeof(ThreeDInitiate.ThreeDInitiateHandler));
        opts.Discovery.IncludeType(typeof(ThreeDComplete.ThreeDCompleteHandler));
        opts.Discovery.IncludeType(typeof(ThreeDModelComplete.ThreeDModelCompleteHandler));
    }
}
