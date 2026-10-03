using UnityEditor;
using UnityEngine;

namespace SealGugu.Editor
{
    [CustomEditor(typeof(GameCredits))]
    public sealed class GameCreditsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("海豹咕咕 · V14.0.0", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("每位組員可直接在下方輸入自己的姓名與分工。展開「名單」後按 + 新增一列，儲存後進入 Play Mode 即可預覽；打包會使用這份名單。", MessageType.Info);
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("group"), new GUIContent("小組"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("entries"), new GUIContent("名單"), true);
            serializedObject.ApplyModifiedProperties();
        }

        [MenuItem("Tools/海豹咕咕/編輯 CREDITS")]
        public static void SelectCredits()
        {
            var credits = AssetDatabase.LoadAssetAtPath<GameCredits>("Assets/Resources/GameCredits.asset");
            if (credits == null)
            {
                credits = ScriptableObject.CreateInstance<GameCredits>();
                AssetDatabase.CreateAsset(credits, "Assets/Resources/GameCredits.asset");
                AssetDatabase.SaveAssets();
            }
            Selection.activeObject = credits;
            EditorGUIUtility.PingObject(credits);
        }
    }
}
