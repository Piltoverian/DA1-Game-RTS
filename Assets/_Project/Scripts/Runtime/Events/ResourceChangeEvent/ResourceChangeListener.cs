using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class ResourceChangeListener : EventListener<ResourceChangeChannel,ResourceChangeEvent>
{
}

public class ResourceChangeEvent :IEvent
{
    public int playerId;
    public List<ResourcePair> value=new List<ResourcePair>();
}