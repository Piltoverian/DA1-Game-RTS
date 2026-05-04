using System.Collections.Generic;
using UnityEngine;

public enum PlayerContextUpdateType
{
   ResourceUpdate,
   All
}

public enum Age
{
    Industrial,
    Modern,
    Future
}
[System.Serializable]
public struct ResourcePair
{
    public ResourceType Type;
    public float Amount;
   public ResourcePair(ResourceType type, float amount)
   {
      Type = type;
      Amount = amount;
   }
}

[System.Serializable]
public struct PlayerContext : Context
{
   private int PlayerId;
   private int CIVILIZATION_ID;
   private List<ResourcePair> Resources;
   private Age age;
   private bool ageUpdateFlag;
   private bool resourceUpdateFlag;

   public PlayerContext(int playerId, int civilizationId, List<ResourcePair> resources, Age age)
   {
      PlayerId = playerId;
      CIVILIZATION_ID = civilizationId;
      Resources = resources;
      this.age = age;
      ageUpdateFlag = false;
      resourceUpdateFlag = false;
    }

   public void OnContextUpdate()
   {
      
   }
}


