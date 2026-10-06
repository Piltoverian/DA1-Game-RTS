using UnityEngine;

[CreateAssetMenu(menuName = "RTS/Terrain Theme")]
public class TerrainTheme : ScriptableObject
{
    [Header("Cliffs")]
    public Sprite cliffStraight;
    public Sprite cliffOuterCorner;
    public Sprite cliffInnerCorner;

    [Header("Ramps")]
    public Sprite rampSingle;
    public Sprite rampLeft;
    public Sprite rampMiddle;
    public Sprite rampRight;

    [Header("Ground")]
    public Sprite groundGrass;
    public Sprite groundSoil;

    [Header("Rendering")]
    public Material material;
    public Color mountainColor = new Color(.396f, .42f, .396f);

    // Compiler IDs remain stable; designers assign sprites by name in the Inspector.
    public const int TileCount = 9;
    public Sprite GetSprite(int tile) => tile switch
    {
        0 => Require(cliffStraight, nameof(cliffStraight)),
        1 => Require(cliffOuterCorner, nameof(cliffOuterCorner)),
        2 => Require(cliffInnerCorner, nameof(cliffInnerCorner)),
        3 => Require(rampSingle, nameof(rampSingle)),
        4 => Require(rampLeft, nameof(rampLeft)),
        5 => Require(rampMiddle, nameof(rampMiddle)),
        6 => Require(rampRight, nameof(rampRight)),
        7 => Require(groundGrass, nameof(groundGrass)),
        8 => Require(groundSoil, nameof(groundSoil)),
        _ => throw new System.ArgumentOutOfRangeException(nameof(tile), tile, "Unknown terrain tile")
    };

    Sprite Require(Sprite sprite, string field)
    {
        if (!sprite) throw new System.InvalidOperationException($"Terrain theme '{name}' is missing sprite '{field}'");
        return sprite;
    }
}
