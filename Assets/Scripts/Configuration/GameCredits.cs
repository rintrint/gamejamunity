using System.Collections.Generic;
using UnityEngine;

namespace SealGugu
{
    /// <summary>Editable team credits; no code changes are needed to add names.</summary>
    [CreateAssetMenu(fileName = "GameCredits", menuName = "海豹咕咕/製作名單")]
    public sealed class GameCredits : ScriptableObject
    {
        [Tooltip("製作小組名稱")]
        public string group = "第二組";

        [Tooltip("每一格可填入姓名與分工。按 + 新增；遊戲會依順序顯示。")]
        public List<string> entries = new List<string> { "內容待補" };
    }
}
