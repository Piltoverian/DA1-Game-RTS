using Unity.Collections;
using UnityEngine;


public struct PlayerMatchDataAfterBanPick
{

    public int matchId;

    public int PlayerId;
    public FixedString64Bytes civId;

    public int spawnPointId;

    public PlayerMatchDataAfterBanPick(int playerId, FixedString64Bytes civId, int spawnPointId, int matchId)
    {
        PlayerId = playerId;
        this.civId = civId;
        this.spawnPointId = spawnPointId;
        this.matchId = matchId;
    }
}

public struct MatchRequestData// just a sketch update it later, this is match create request after ban pick phase, we can add more data to it later
{
    public int matchId;
    public int mapID;//replaced by enum or sth better in future 


    public int seed;//replaced by enum or sth better in future

    public int playerCount;

    public int typeOfMatch;//replaced by enum or sth better in future

    public NativeArray<PlayerMatchDataAfterBanPick> playerMatchDataAfterBanPick;

    public MatchRequestData(int matchId, int mapID, int playerCount, int typeOfMatch, int seed)
    {
        this.matchId = matchId;
        this.mapID = mapID;
        this.seed = seed;
        this.playerCount = playerCount;
        this.typeOfMatch = typeOfMatch;
        playerMatchDataAfterBanPick = new NativeArray<PlayerMatchDataAfterBanPick>(playerCount, Allocator.Persistent);
    }
}
