// ScreenManager.cs — reproduces the design's screen-switching (its <sc-if nav==...> logic).
// In the design, exactly one screen is visible at a time based on a "nav" string.
// Here: put every full-screen panel under one parent, assign them below, and call Show().
//
// Setup in Unity:
//   1. Create empty "ScreenRoot" under your Canvas, stretch to full size.
//   2. Add each screen as a child panel (Login, Menu, Config, ...), each stretched full.
//   3. Add this component to ScreenRoot and drag each panel into the matching field.
//   4. Wire buttons: onClick -> ScreenManager.Show(Screen.Menu), etc.

using System;
using System.Collections.Generic;
using UnityEngine;

public class ScreenManager : MonoBehaviour
{
    public enum Screen
    {
        Login, Menu, Configuration, Ranked, CreateLobby, JoinLobby,
        Profile, Avatar, Rules, Initiative, Match
    }

    [Serializable]
    public struct ScreenPanel
    {
        public Screen screen;
        public GameObject panel;
    }

    public List<ScreenPanel> panels = new List<ScreenPanel>();
    public Screen startScreen = Screen.Login;

    Screen _previous;

    void Awake() => Show(startScreen);

    public void Show(Screen target)
    {
        foreach (var p in panels)
            if (p.panel) p.panel.SetActive(p.screen == target);
    }

    // For the "?" rules button that must return to whatever screen opened it.
    public void ShowRemember(Screen target)
    {
        // record current, then show target
        foreach (var p in panels)
            if (p.panel && p.panel.activeSelf) { _previous = p.screen; break; }
        Show(target);
    }

    public void Back() => Show(_previous);

    // Convenience wrappers so UI Buttons can call them with no arguments.
    public void ShowLogin()       => Show(Screen.Login);
    public void ShowMenu()        => Show(Screen.Menu);
    public void ShowConfig()      => Show(Screen.Configuration);
    public void ShowRanked()      => Show(Screen.Ranked);
    public void ShowCreateLobby() => Show(Screen.CreateLobby);
    public void ShowJoinLobby()   => Show(Screen.JoinLobby);
    public void ShowProfile()     => Show(Screen.Profile);
    public void ShowAvatar()      => Show(Screen.Avatar);
    public void ShowRules()       => ShowRemember(Screen.Rules);
    public void ShowInitiative()  => Show(Screen.Initiative);
    public void ShowMatch()       => Show(Screen.Match);
}
