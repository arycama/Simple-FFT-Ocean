using UnityEditor;

[CustomEditor(typeof(Ocean))]
public class OceanEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (var changed = new EditorGUI.ChangeCheckScope())
        {
            base.OnInspectorGUI();

            if(changed.changed && EditorApplication.isPlaying)
            {
                var ocean = target as Ocean;
                if(ocean.enabled)
                {
                    ocean.Recalculate();
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
