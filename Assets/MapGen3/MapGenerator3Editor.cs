using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

//this script is to add a button in unity interface

[CustomEditor(typeof(MapGenerator3))]
public class MapGenerator3Editor : Editor
{
    public override void OnInspectorGUI() {
        MapGenerator3 mapGen = (MapGenerator3)target;

        //draw map if any changes are made
        if (DrawDefaultInspector())
        {
            if (mapGen.autoUpdate)
            {
                mapGen.DrawMapInEditor();
            }
        }

        if (GUILayout.Button("Draw"))
        {
            mapGen.DrawMapInEditor();
        }

        if (GUILayout.Button("Simulate"))
        {
            mapGen.Simulate();
        }
    }
}
