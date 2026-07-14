// PlenusTheme.cs — light/dark theme swap, matching the design's theme toggle.
// Add ThemedGraphic to any Image/TMP_Text you want recolored, pick its Role,
// then call PlenusTheme.SetDark(true/false) from your theme toggle button.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum ThemeRole
{
    PhoneBg, Card, CardAlt, TextPrimary, TextSecondary, InputBg, Border
}

public static class PlenusTheme
{
    public static bool IsDark { get; private set; }
    public static event Action OnThemeChanged;

    public static void SetDark(bool dark)
    {
        IsDark = dark;
        OnThemeChanged?.Invoke();
    }
    public static void Toggle() => SetDark(!IsDark);

    public static Color Get(ThemeRole role)
    {
        switch (role)
        {
            case ThemeRole.PhoneBg:       return IsDark ? PlenusColors.DarkBg            : PlenusColors.LightBg;
            case ThemeRole.Card:          return IsDark ? PlenusColors.DarkCard          : PlenusColors.LightCard;
            case ThemeRole.CardAlt:       return IsDark ? PlenusColors.DarkCardAlt       : PlenusColors.LightCardAlt;
            case ThemeRole.TextPrimary:   return IsDark ? PlenusColors.DarkTextPrimary   : PlenusColors.LightTextPrimary;
            case ThemeRole.TextSecondary: return IsDark ? PlenusColors.DarkTextSecondary : PlenusColors.LightTextSecondary;
            case ThemeRole.InputBg:       return IsDark ? PlenusColors.DarkInputBg       : PlenusColors.LightInputBg;
            case ThemeRole.Border:        return IsDark ? PlenusColors.DarkBorder        : PlenusColors.LightBorder;
            default: return Color.magenta;
        }
    }
}

// Attach to an Image or TMP_Text. It recolors itself whenever the theme changes.
[DisallowMultipleComponent]
public class ThemedGraphic : MonoBehaviour
{
    public ThemeRole role = ThemeRole.Card;

    Graphic _graphic;   // Image, RawImage, or TMP all derive from Graphic

    void Awake()  => _graphic = GetComponent<Graphic>();
    void OnEnable() { PlenusTheme.OnThemeChanged += Apply; Apply(); }
    void OnDisable() => PlenusTheme.OnThemeChanged -= Apply;

    public void Apply()
    {
        if (_graphic == null) _graphic = GetComponent<Graphic>();
        if (_graphic != null) _graphic.color = PlenusTheme.Get(role);
    }
}
