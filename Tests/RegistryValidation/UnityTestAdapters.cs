// Standalone validation-only adapters. These are outside Assets and never enter the game.
// They do NOT simulate Unity serialization, destroyed objects, OnValidate or baking.
using System;

// Current SO sources also declare WeaponDefinitionBlob with an Entity field.
// Validation never uses that field; these adapters only allow linking those sources.
namespace Unity.Entities { public struct Entity { } }
// Text adapter only; does not simulate native FixedString layout.
namespace Unity.Collections
{
    public struct FixedString64Bytes
    {
        private readonly string value; public FixedString64Bytes(string value) { this.value = value; } public override string ToString() => value ?? string.Empty;
    }
}
namespace NUnit.Framework { }

namespace UnityEngine
{
    public class Object { public string name; }
    public class ScriptableObject : Object { }
    public class GameObject : Object { }
    public class Sprite : Object { }
    public struct Vector2 { public float x, y; }
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string fileName, menuName;
        public int order;
    }
    public sealed class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string value) { } }
    public sealed class SerializeField : Attribute { }
}

// Shape used by the validator; production ResourcePair also implements IBufferElementData.
public struct ResourcePair
{
    public ResourceType Type;
    public float Amount;
    public ResourcePair(ResourceType type, float amount) { Type = type; Amount = amount; }
}
