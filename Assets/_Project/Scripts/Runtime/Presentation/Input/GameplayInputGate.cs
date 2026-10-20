using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Presentation-only arbitration. Does not create a placer or issue ECS commands.</summary>
public static class GameplayInputGate
{
    private static EventSystem cachedEventSystem;
    private static PointerEventData pointer;
    private static readonly List<RaycastResult> hits = new();
    private static PointerInputSuppression suppression;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        cachedEventSystem = null;
        pointer = null;
        hits.Clear();
        suppression = default;
    }

    public static bool IsEditingText
    {
        get
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return false;
            var tmp = selected.GetComponentInParent<TMP_InputField>();
            if (tmp != null && tmp.isFocused) return true;
            var legacy = selected.GetComponentInParent<InputField>();
            return legacy != null && legacy.isFocused;
        }
    }

    public static bool PlacementOwnsPointer
    {
        get
        {
            var mouse = Mouse.current;
            bool held = mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed);
            return BuildingPlacer.IsPlacementActive || suppression.IsBlocked(Time.frameCount, held);
        }
    }

    public static bool BlocksWorldInput => !Application.isFocused || IsEditingText || PlacementOwnsPointer;

    public static void SuppressWorldInputUntilRelease() => suppression.Block(Time.frameCount);

    public static bool IsPointerOverUI()
    {
        var current = EventSystem.current;
        var mouse = Mouse.current;
        if (current == null || mouse == null) return false;
        if (cachedEventSystem != current || pointer == null)
        {
            cachedEventSystem = current;
            pointer = new PointerEventData(current);
        }

        // IsPointerOverGameObject can contain the previous EventSystem update's position.
        // Query current geometry, including from FixedUpdate / an early camera Update.
        pointer.Reset();
        pointer.pointerId = -1;
        pointer.position = mouse.position.ReadValue();
        hits.Clear();
        current.RaycastAll(pointer, hits);
        bool overUI = false;
        foreach (var hit in hits)
        {
            if (hit.module is GraphicRaycaster) { overUI = true; break; }
        }
        hits.Clear();
        return overUI;
    }
}

// Kept independent of Unity state so the cancellation/hold policy can be verified separately.
internal struct PointerInputSuppression
{
    private bool active;
    private int blockedFrame;

    public void Block(int frame)
    {
        active = true;
        blockedFrame = frame;
    }

    public bool IsBlocked(int frame, bool anyButtonHeld)
    {
        if (!active) return false;
        if (frame == blockedFrame || anyButtonHeld) return true;
        active = false;
        return false;
    }
}
