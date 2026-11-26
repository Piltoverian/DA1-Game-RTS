using UnityEditor;
using UnityEngine;

public static class NetworkingProtocolChecks
{
    [MenuItem("Tools/RTS/Check Networking Protocol")]
    public static void Run()
    {
        MultiplayerNetworkingChecks.Run();
        SeedNetworkingChecks.Run();
        RejoinNetworkingChecks.Run();
        RejoinWorldLifetimeChecks.Run();
        IncrementalBuildChecks.Run();
        NetworkBuildPlacementChecks.Run();
        ProtocolTimingChecks.Run();
        WorldCatalogChecks.Run();
        NeutralMemoryChecks.Run();
        TerrainStagingChecks.Run();
        Debug.Log("Networking protocol checks passed.");
    }
}
