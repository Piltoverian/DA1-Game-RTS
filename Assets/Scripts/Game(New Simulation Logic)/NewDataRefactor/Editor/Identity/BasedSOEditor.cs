using UnityEditor;

namespace RTS.DataEditor
{
    [CustomEditor(typeof(BasedSO), true)]
    [CanEditMultipleObjects]
    public sealed class BasedSOEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                bool readOnly = property.propertyPath == "m_Script"
                    || property.propertyPath == nameof(BasedSO.ID);
                using (new EditorGUI.DisabledScope(readOnly))
                    EditorGUILayout.PropertyField(property, true);
            }
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox(
                "Assign a missing Base ID using Assign Missing IDs on its GameDataRegistry. Existing IDs are preserved.",
                MessageType.Info);
        }
    }
}
