// PlenusColors.cs — auto-generated from the Plenus mobile UI design.
// Every value is the sRGB/hex conversion of the design's oklch() colors.
// Usage:  someImage.color = PlenusColors.PrimaryRed;
using UnityEngine;

public static class PlenusColors
{
    // Helper: hex string -> UnityEngine.Color
    static Color H(string hex){ ColorUtility.TryParseHtmlString(hex, out var c); return c; }

    // ---- Light theme ----
    public static readonly Color LightBg = H("#F6EDE0");
    public static readonly Color LightCard = H("#F9F4EE");
    public static readonly Color LightCardAlt = H("#E7DCD0");
    public static readonly Color LightTextPrimary = H("#362C24");
    public static readonly Color LightTextSecondary = H("#6C6158");
    public static readonly Color LightInputBg = H("#E7DCD0");
    public static readonly Color LightBorder = H("#E0D6CA");

    // ---- Dark theme ----
    public static readonly Color DarkBg = H("#201914");
    public static readonly Color DarkCard = H("#2F2722");
    public static readonly Color DarkCardAlt = H("#3B342E");
    public static readonly Color DarkTextPrimary = H("#EFEBE4");
    public static readonly Color DarkTextSecondary = H("#ABA39B");
    public static readonly Color DarkInputBg = H("#342C26");
    public static readonly Color DarkBorder = H("#4E463F");

    // ---- Accents ----
    public static readonly Color PrimaryRed = H("#D05F43");
    public static readonly Color PrimaryRedPress = H("#9D381F");
    public static readonly Color Green = H("#418E47");
    public static readonly Color GreenPress = H("#1E6626");
    public static readonly Color ReadyGreen = H("#38853E");
    public static readonly Color AccentLink = H("#833F27");
    public static readonly Color AccentLinkHover = H("#742400");
    public static readonly Color LogoutRed = H("#C74C3D");
    public static readonly Color StarGold = H("#EAB532");

    // ---- Board colors ----
    public static readonly Color BoardGreen = H("#80CD82");
    public static readonly Color BoardRed = H("#F47B74");
    public static readonly Color BoardYellow = H("#F1D35D");
    public static readonly Color BoardBlue = H("#79B0E8");
    public static readonly Color BoardOrange = H("#FBA962");
    public static readonly Color BoardBlack = H("#505869");
    public static readonly Color CellMarked = H("#DBD7D0");
    public static readonly Color PendingRing = H("#0072D5");
    public static readonly Color DiePip = H("#362C24");

    // ---- Avatar swatches ----
    public static readonly Color Avatar1 = H("#CB764E");
    public static readonly Color Avatar2 = H("#47944C");
    public static readonly Color Avatar3 = H("#488ACB");
    public static readonly Color Avatar4 = H("#C99500");
    public static readonly Color Avatar5 = H("#C74B47");
    public static readonly Color Avatar6 = H("#7B63A3");
    public static readonly Color Avatar7 = H("#E1707C");
    public static readonly Color Avatar8 = H("#009E98");
    public static readonly Color Avatar9 = H("#B1A93A");
    public static readonly Color Avatar10 = H("#887769");
    public static readonly Color Avatar11 = H("#CD6AAF");
    public static readonly Color Avatar12 = H("#5D646F");

    // ---- Rank tiers ----
    public static readonly Color WoodIII = H("#8C6D58");
    public static readonly Color WoodII = H("#9E7B64");
    public static readonly Color WoodI = H("#B0896F");
    public static readonly Color SilverII = H("#ABB2BB");
    public static readonly Color SilverI = H("#C2C8CF");
    public static readonly Color Gold = H("#E3AE28");
    public static readonly Color Diamond = H("#1EBDE3");
    public static readonly Color Aurelune = H("#BD5ED2");

    // Board color lookup by die-color name (green/red/yellow/blue/orange/black)
    public static Color Board(string colorName){ switch(colorName){
        case "green": return BoardGreen;
        case "red": return BoardRed;
        case "yellow": return BoardYellow;
        case "blue": return BoardBlue;
        case "orange": return BoardOrange;
        case "black": return BoardBlack;
        default: return Color.magenta; } }
}