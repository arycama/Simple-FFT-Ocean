using UnityEditor;

[CustomEditor(typeof(Ocean))]
public class OceanEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var resolutionProperty = serializedObject.FindProperty("resolution");
        var resolution = resolutionProperty.intValue;

        using (var changed = new EditorGUI.ChangeCheckScope())
        {
            base.OnInspectorGUI();

            if(changed.changed)
            {
                var ocean = target as Ocean;
                if(ocean.enabled)
                {
                    serializedObject.ApplyModifiedProperties();

                    if (resolutionProperty.intValue != resolution)
                    {
                        // If resolution changed, we need to rebuild all the tables and textures
                        ocean.Cleanup();
                        ocean.ReInitialize();
                    }
                    else
                    {
                        // Otherwise just recalculate some data
                        ocean.Recalculate();
                    }
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
