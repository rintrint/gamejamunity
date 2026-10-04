using System;
using UnityEngine;

namespace SealGugu
{
    public static class GuguTypography
    {
        public static string Family { get; private set; }
        // Passive labels keep their authored color even when Unity selects a hover state.
        // Apply this per draw pass so shadows and outlines retain their own tint, too.
        public static void SetLabelColor(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        public static Font Create() {
            var installed=Font.GetOSInstalledFontNames();
            foreach(string preferred in new[]{"Microsoft JhengHei","Microsoft JhengHei UI"})
                if(Array.Exists(installed,n=>string.Equals(n,preferred,StringComparison.OrdinalIgnoreCase))){
                    Family=preferred;Debug.Log("GUGU_FONT "+Family);
                    return Font.CreateDynamicFontFromOSFont(preferred,24);
                }
            Family="Noto Sans TC (system JhengHei unavailable)";
            Debug.LogWarning("Microsoft JhengHei is not installed; using bundled sans-serif fallback.");
            return Resources.Load<Font>("Fonts/NotoSansTC");
        }
    }
}
