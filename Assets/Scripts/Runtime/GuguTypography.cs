using System;
using UnityEngine;

namespace SealGugu
{
    public static class GuguTypography
    {
        public static string Family { get; private set; }
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
