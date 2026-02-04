using gameManagerModule;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
public enum InputStatus
{
    Held = 0,
    Released = 1,
    JustPress = 2,
}
public class FixedUpdateInputTracker : IFixedUpdateModule, IUpdateModule
{
    List<TrackerPair> trackerList = new List<TrackerPair>();
    private static FixedUpdateInputTracker Instance;
    IDisposable anyButtonSub;
    public void AwakeModule()
    {
        anyButtonSub = InputSystem.onAnyButtonPress.Call(OnAnyPress);
    }

    public void OnGameStart()
    {

    }
    public void FixedUpdateModule()
    {
        foreach (TrackerPair pair in trackerList)
        {
            bool currentcontrolstate = pair.hardwarePressed;

            if (currentcontrolstate && !pair.pressedLastFixed)
            {
                pair.status = InputStatus.JustPress;
            }
            else if (currentcontrolstate && pair.pressedLastFixed)
            {
                pair.status = InputStatus.Held;
            }
            else
            {
                pair.status = InputStatus.Released;
            }

            pair.pressedLastFixed = currentcontrolstate;
        }
    }
    public bool IsJustPress(InputAction inputAction)
    {
        InputControl control = FromActionToControl(inputAction);
        if (control == null)
        {
            Debug.LogWarning("this input has not been bind yet");
            return false;
        }
        TrackerPair pairforcontrol = trackerList.Find(controlR => control == controlR.Control);
        if (pairforcontrol == null)
        {
            Debug.LogWarning("this action has not been pressed yet");
            return false;
        }
        if (pairforcontrol.status == InputStatus.JustPress)
        {
            return true;
        }
        return false;
    }

    public bool IsHolding(InputAction inputAction)
    {
        InputControl control = FromActionToControl(inputAction);
        if (control == null)
        {
            Debug.LogWarning("this input has not been bind yet");
            return false;
        }
        TrackerPair pairforcontrol = trackerList.Find(controlR => control == controlR.Control);
        if (pairforcontrol == null)
        {
            Debug.LogWarning("this action has not been pressed yet");
            return false;
        }
        if (pairforcontrol.status == InputStatus.Held)
        {
            return true;
        }
        return false;
    }

    public bool IsReleased(InputAction inputAction)
    {
        InputControl control = FromActionToControl(inputAction);
        if (control == null)
        {
            Debug.LogWarning("this input has not been bind yet");
            return true;
        }
        TrackerPair pairforcontrol = trackerList.Find(controlR => control == controlR.Control);
        if (pairforcontrol == null)
        {
            Debug.LogWarning("this action has not been pressed yet");
            return true;
        }
        if (pairforcontrol.status == InputStatus.Released)
        {
            return true;
        }
        return false;
    }

    InputControl FromActionToControl(InputAction inputAction)
    {
        foreach (var control in inputAction.controls)
        {
            if (control != null)
            {
                if (control.device is Mouse || control.device is Keyboard)
                {
                    return control;
                }
            }
        }
        return null;
    }

    public void UpdateModule()
    {
        foreach (var tracker in trackerList)
        {
            tracker.hardwarePressed = tracker.Control.IsPressed();
        }
    }
    void OnAnyPress(InputControl control)
    {
        if (control.device is not Mouse && control.device is not Keyboard)
            return;

        if (trackerList.Find(p => p.Control == control) == null)
        {
            trackerList.Add(new TrackerPair(control));
        }
    }

    public bool IsJustPress(InputControl control)
    {
        TrackerPair trackerPair = trackerList.Find(p => p.Control == control);
        if (trackerPair == null)
            return false;
        else if (trackerPair.status == InputStatus.JustPress)
        {
            return true;
        }
        return false;
    }

    public bool IsHolding(InputControl control)
    {
        TrackerPair trackerPair = trackerList.Find(p => p.Control == control);
        if (trackerPair == null)
            return false;
        else if (trackerPair.status == InputStatus.Held)
        {
            return true;
        }
        return false;
    }

    public bool IsReleased(InputControl control)
    {
        TrackerPair trackerPair = trackerList.Find(p => p.Control == control);
        if (trackerPair == null)
            return true;
        else if (trackerPair.status == InputStatus.Released)
        {
            return true;
        }
        return false;
    }

}


public class TrackerPair
{
    public InputControl Control;
    public InputStatus status;
    public bool hardwarePressed;
    public bool pressedLastFixed;

    public TrackerPair(InputControl inputControl)
    {
        this.Control = inputControl;
    }
}