using System;
using UnityEngine;

[CreateAssetMenu(menuName = "RTS/Terrain Theme")]
public class TerrainTheme : ScriptableObject
{
    public const int CanvasPixels = 128;
    public const int TierPixels = 32;
    public const float CameraPitch = 30;
    public const float CameraYaw = 45;

    [Header("Ground")]
    [InspectorName("Flat ground (mask 0)")]
    [Tooltip("top-0.png; 128x128 readable PNG. No raised corners.") ]
    public Texture2D topFlatMask0;
    [Tooltip("Optional flat-ground variations, with matching edges and lighting. Empty keeps the original theme.")]
    public Texture2D[] flatGroundVariants = Array.Empty<Texture2D>();
    [Tooltip("Presentation-only seed. Does not consume the terrain or resource generator RNG.")]
    public int groundVisualSeed = 40;
    [Min(2), Tooltip("Scale of coherent lush/sparse ground patches in grid cells.")]
    public float groundRegionCells = 16;
    [Min(0), Tooltip("Optional square continuous-material bank width. Two banks must each contain columns squared tiles.")]
    public int groundPatternColumns;
    [Tooltip("Smooth texture sampling for painted artwork. Original themes retain point filtering.")]
    public bool smoothTextureSampling;
    [Header("Ramps - raised corners")]
    [InspectorName("00 high (mask 1)")]
    [Tooltip("top-1.png; 128x128 readable PNG. Raised corners: 00.") ]
    public Texture2D topHigh00Mask1;
    [InspectorName("10 high (mask 2)")]
    [Tooltip("top-2.png; 128x128 readable PNG. Raised corners: 10.") ]
    public Texture2D topHigh10Mask2;
    [InspectorName("00 + 10 high (mask 3)")]
    [Tooltip("top-3.png; 128x128 readable PNG. Raised corners: 00 + 10.") ]
    public Texture2D topHigh00_10Mask3;
    [InspectorName("11 high (mask 4)")]
    [Tooltip("top-4.png; 128x128 readable PNG. Raised corners: 11.") ]
    public Texture2D topHigh11Mask4;
    [InspectorName("00 + 11 high (mask 5)")]
    [Tooltip("top-5.png; 128x128 readable PNG. Raised corners: 00 + 11.") ]
    public Texture2D topHigh00_11Mask5;
    [InspectorName("10 + 11 high (mask 6)")]
    [Tooltip("top-6.png; 128x128 readable PNG. Raised corners: 10 + 11.") ]
    public Texture2D topHigh10_11Mask6;
    [InspectorName("00 + 10 + 11 high (mask 7)")]
    [Tooltip("top-7.png; 128x128 readable PNG. Raised corners: 00 + 10 + 11.") ]
    public Texture2D topHigh00_10_11Mask7;
    [InspectorName("01 high (mask 8)")]
    [Tooltip("top-8.png; 128x128 readable PNG. Raised corners: 01.") ]
    public Texture2D topHigh01Mask8;
    [InspectorName("00 + 01 high (mask 9)")]
    [Tooltip("top-9.png; 128x128 readable PNG. Raised corners: 00 + 01.") ]
    public Texture2D topHigh00_01Mask9;
    [InspectorName("10 + 01 high (mask A)")]
    [Tooltip("top-A.png; 128x128 readable PNG. Raised corners: 10 + 01.") ]
    public Texture2D topHigh10_01MaskA;
    [InspectorName("00 + 10 + 01 high (mask B)")]
    [Tooltip("top-B.png; 128x128 readable PNG. Raised corners: 00 + 10 + 01.") ]
    public Texture2D topHigh00_10_01MaskB;
    [InspectorName("11 + 01 high (mask C)")]
    [Tooltip("top-C.png; 128x128 readable PNG. Raised corners: 11 + 01.") ]
    public Texture2D topHigh11_01MaskC;
    [InspectorName("00 + 11 + 01 high (mask D)")]
    [Tooltip("top-D.png; 128x128 readable PNG. Raised corners: 00 + 11 + 01.") ]
    public Texture2D topHigh00_11_01MaskD;
    [InspectorName("10 + 11 + 01 high (mask E)")]
    [Tooltip("top-E.png; 128x128 readable PNG. Raised corners: 10 + 11 + 01.") ]
    public Texture2D topHigh10_11_01MaskE;
    [Header("Top - all corners raised")]
    [InspectorName("00 + 10 + 11 + 01 high (mask F)")]
    [Tooltip("top-F.png; 128x128 readable PNG. Raised corners: 00 + 10 + 11 + 01.") ]
    public Texture2D topHigh00_10_11_01MaskF;

    [Header("Cliff -Z - endpoint masks")]
    [InspectorName("HighB (mask 4)")]
    [Tooltip("cliff-0-4.png; endpoint bits lowA/lowB/highB/highA. HighB.") ]
    public Texture2D cliffNegativeZHighBMask4;
    [InspectorName("HighA (mask 8)")]
    [Tooltip("cliff-0-8.png; endpoint bits lowA/lowB/highB/highA. HighA.") ]
    public Texture2D cliffNegativeZHighAMask8;
    [InspectorName("Full wall (mask C)")]
    [Tooltip("cliff-0-C.png; endpoint bits lowA/lowB/highB/highA. HighB + HighA.") ]
    public Texture2D cliffNegativeZHighBHighAMaskC;
    [InspectorName("LowA + HighB + HighA (mask D)")]
    [Tooltip("cliff-0-D.png; endpoint bits lowA/lowB/highB/highA. LowA + HighB + HighA.") ]
    public Texture2D cliffNegativeZLowAHighBHighAMaskD;
    [InspectorName("LowB + HighB + HighA (mask E)")]
    [Tooltip("cliff-0-E.png; endpoint bits lowA/lowB/highB/highA. LowB + HighB + HighA.") ]
    public Texture2D cliffNegativeZLowBHighBHighAMaskE;

    [Header("Cliff -X - endpoint masks")]
    [InspectorName("HighB (mask 4)")]
    [Tooltip("cliff-1-4.png; endpoint bits lowA/lowB/highB/highA. HighB.") ]
    public Texture2D cliffNegativeXHighBMask4;
    [InspectorName("HighA (mask 8)")]
    [Tooltip("cliff-1-8.png; endpoint bits lowA/lowB/highB/highA. HighA.") ]
    public Texture2D cliffNegativeXHighAMask8;
    [InspectorName("Full wall (mask C)")]
    [Tooltip("cliff-1-C.png; endpoint bits lowA/lowB/highB/highA. HighB + HighA.") ]
    public Texture2D cliffNegativeXHighBHighAMaskC;
    [InspectorName("LowA + HighB + HighA (mask D)")]
    [Tooltip("cliff-1-D.png; endpoint bits lowA/lowB/highB/highA. LowA + HighB + HighA.") ]
    public Texture2D cliffNegativeXLowAHighBHighAMaskD;
    [InspectorName("LowB + HighB + HighA (mask E)")]
    [Tooltip("cliff-1-E.png; endpoint bits lowA/lowB/highB/highA. LowB + HighB + HighA.") ]
    public Texture2D cliffNegativeXLowBHighBHighAMaskE;

    [Header("Mountain")]
    public Texture2D mountainArtwork;
    public Vector2 mountainSpritePivot = new Vector2(.5f, .15f);
    [Tooltip("Optional 16 canonical rock caps indexed by shared-corner height mask. Empty keeps legacy mountain art.")]
    public Texture2D[] mountainCaps = Array.Empty<Texture2D>();
    [Range(1,6)] public int mountainVisualTiers = 3;
    [Tooltip("Small non-colliding accents restricted to the resource-free mountain fringe.")]
    public Texture2D[] fringeDecorations = Array.Empty<Texture2D>();
    [Range(0,.2f)] public float fringeDecorationDensity = .06f;
    [Header("Material")]
    [Tooltip("Stock Universal Render Pipeline/Unlit shader; retained for player builds.")]
    public Shader stockUnlitShader;

    [Header("Optional offline authoring sources (not required for runtime)")]
    public Texture2D groundArtwork;
    public Texture2D rampArtwork;
    public Texture2D cliffArtwork;
    [Tooltip("Source PNG UVs with top-left origin; corners 00,10,11,01.")]
    public Vector2[] groundSpriteCorners;
    public Vector2[] rampSpriteCorners;
    [Tooltip("Source PNG UVs with top-left origin; lowA,lowB,highB,highA.")]
    public Vector2[] cliffSpriteCorners;

    public Texture2D GetTopSprite(int mask) => mask switch
    {
        0 => topFlatMask0,
        1 => topHigh00Mask1,
        2 => topHigh10Mask2,
        3 => topHigh00_10Mask3,
        4 => topHigh11Mask4,
        5 => topHigh00_11Mask5,
        6 => topHigh10_11Mask6,
        7 => topHigh00_10_11Mask7,
        8 => topHigh01Mask8,
        9 => topHigh00_01Mask9,
        10 => topHigh10_01MaskA,
        11 => topHigh00_10_01MaskB,
        12 => topHigh11_01MaskC,
        13 => topHigh00_11_01MaskD,
        14 => topHigh10_11_01MaskE,
        15 => topHigh00_10_11_01MaskF,
        _ => throw new ArgumentOutOfRangeException(nameof(mask), mask, "Top mask must be 0..15")
    };

    public Texture2D GetFlatGroundSprite(int x, int z)
    {
        if (flatGroundVariants == null || flatGroundVariants.Length == 0) return topFlatMask0;
        // Two banks (lush/sparse), with smoothly varying probability across broad patches.
        // A stable cell hash selects detail; no per-cell colour tint or directional flip.
        uint hash;
        unchecked
        {
            hash = (uint)x * 0x8da6b343u ^ (uint)z * 0xd8163841u ^ (uint)groundVisualSeed;
            hash ^= hash >> 16; hash *= 0x7feb352du; hash ^= hash >> 15; hash *= 0x846ca68bu; hash ^= hash >> 16;
        }
        float scale = Mathf.Max(2, groundRegionCells);
        float offset = (groundVisualSeed & 0xffff) * .03125f;
        float patch = Mathf.PerlinNoise(x / scale + offset, z / scale + 173.375f);
        if (groundPatternColumns > 0)
        {
            int columns = groundPatternColumns;
            long bank = (long)columns * columns;
            if (bank * 2 != flatGroundVariants.Length) throw new InvalidOperationException("Ground pattern requires two complete square material banks.");
            int Mod(int value) => (int)(((long)value % columns + columns) % columns);
            int index = Mod(x + (groundVisualSeed & 255)) + Mod(z + ((groundVisualSeed >> 8) & 255)) * columns;
            bool worn = (hash & 0xffff) / 65535f < Mathf.SmoothStep(.08f, .92f, patch);
            var patterned = flatGroundVariants[index + (worn ? (int)bank : 0)];
            return patterned ? patterned : topFlatMask0;
        }
        int split = (flatGroundVariants.Length + 1) / 2;
        bool sparse = flatGroundVariants.Length > 1 && (hash & 0xffff) / 65535f < Mathf.SmoothStep(.08f, .92f, patch);
        int start = sparse ? split : 0, count = sparse ? flatGroundVariants.Length - split : split;
        var texture = flatGroundVariants[start + (int)((hash >> 16) % (uint)count)];
        return texture ? texture : topFlatMask0;
    }

    public Texture2D GetWallSprite(int side, int mask)
    {
        ValidateWallKey(side, mask);
        return (side * 16 + mask) switch
        {
            0 => null,
            1 => null,
            2 => null,
            3 => null,
            4 => cliffNegativeZHighBMask4,
            5 => null,
            6 => null,
            7 => null,
            8 => cliffNegativeZHighAMask8,
            9 => null,
            10 => null,
            11 => null,
            12 => cliffNegativeZHighBHighAMaskC,
            13 => cliffNegativeZLowAHighBHighAMaskD,
            14 => cliffNegativeZLowBHighBHighAMaskE,
            15 => null,
            16 => null,
            17 => null,
            18 => null,
            19 => null,
            20 => cliffNegativeXHighBMask4,
            21 => null,
            22 => null,
            23 => null,
            24 => cliffNegativeXHighAMask8,
            25 => null,
            26 => null,
            27 => null,
            28 => cliffNegativeXHighBHighAMaskC,
            29 => cliffNegativeXLowAHighBHighAMaskD,
            30 => cliffNegativeXLowBHighBHighAMaskE,
            31 => null,
            _ => throw new ArgumentOutOfRangeException(nameof(mask))
        };
    }

    // Offline authoring uses the same named slots as runtime.
    public void SetTopSprite(int mask, Texture2D texture)
    {
        switch (mask)
        {
            case 0: topFlatMask0 = texture; break;
            case 1: topHigh00Mask1 = texture; break;
            case 2: topHigh10Mask2 = texture; break;
            case 3: topHigh00_10Mask3 = texture; break;
            case 4: topHigh11Mask4 = texture; break;
            case 5: topHigh00_11Mask5 = texture; break;
            case 6: topHigh10_11Mask6 = texture; break;
            case 7: topHigh00_10_11Mask7 = texture; break;
            case 8: topHigh01Mask8 = texture; break;
            case 9: topHigh00_01Mask9 = texture; break;
            case 10: topHigh10_01MaskA = texture; break;
            case 11: topHigh00_10_01MaskB = texture; break;
            case 12: topHigh11_01MaskC = texture; break;
            case 13: topHigh00_11_01MaskD = texture; break;
            case 14: topHigh10_11_01MaskE = texture; break;
            case 15: topHigh00_10_11_01MaskF = texture; break;
            default: throw new ArgumentOutOfRangeException(nameof(mask), mask, "Top mask must be 0..15");
        }
    }

    public void SetWallSprite(int side, int mask, Texture2D texture)
    {
        ValidateWallKey(side, mask);
        if (!IsVisibleWallMask(mask))
        {
            if (texture) throw new ArgumentException("Empty wall masks have no sprite field", nameof(texture));
            return;
        }
        switch (side * 16 + mask)
        {




            case 4: cliffNegativeZHighBMask4 = texture; break;



            case 8: cliffNegativeZHighAMask8 = texture; break;



            case 12: cliffNegativeZHighBHighAMaskC = texture; break;
            case 13: cliffNegativeZLowAHighBHighAMaskD = texture; break;
            case 14: cliffNegativeZLowBHighBHighAMaskE = texture; break;





            case 20: cliffNegativeXHighBMask4 = texture; break;



            case 24: cliffNegativeXHighAMask8 = texture; break;



            case 28: cliffNegativeXHighBHighAMaskC = texture; break;
            case 29: cliffNegativeXLowAHighBHighAMaskD = texture; break;
            case 30: cliffNegativeXLowBHighBHighAMaskE = texture; break;

        }
    }

    public static bool IsVisibleWallMask(int mask) => mask == 4 || mask == 8 || mask == 12 || mask == 13 || mask == 14;

    static void ValidateWallKey(int side, int mask)
    {
        if (side < 0 || side > 1) throw new ArgumentOutOfRangeException(nameof(side), side, "Wall side must be 0 (-Z) or 1 (-X)");
        if (mask < 0 || mask > 15) throw new ArgumentOutOfRangeException(nameof(mask), mask, "Wall mask must be 0..15");
    }

    public float ResolveTierRise(float cellSize, Vector3 cameraForward)
    {
        var expected = Quaternion.Euler(CameraPitch, CameraYaw, 0) * Vector3.forward;
        if (!(cellSize > 0) || Vector3.Distance(cameraForward.normalized, expected) > .001f)
            throw new InvalidOperationException("Canonical sprites require positive grid cell size and camera pitch30/yaw45");
        return cellSize * ((float)TierPixels / CanvasPixels) * Mathf.Sqrt(2) / Mathf.Cos(CameraPitch * Mathf.Deg2Rad);
    }
}
