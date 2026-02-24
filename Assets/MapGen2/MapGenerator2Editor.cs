using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

//this script is to add a button in unity interface

[CustomEditor(typeof(MapGenerator2))]
public class MapGenerator2Editor : Editor
{
    bool showInitialSettings = false;
    bool showBlur = false;
    bool showSlip = false;
    bool showWind = false;
    bool showDebug = false;

    public override void OnInspectorGUI() {
        MapGenerator2 mapGen = (MapGenerator2)target;

        //draw map if any changes are made
        if (DrawDefaultInspector())
        {
            if (mapGen.autoUpdate)
            {
                mapGen.Initialize();
            }

        }


        //EditorGUI.BeginChangeCheck();


        showInitialSettings = EditorGUILayout.ToggleLeft("Initial Settings", showInitialSettings);
        EditorGUI.indentLevel++;
        if (showInitialSettings)
        {
            //GUI collapse stuff
            mapGen.bedrockInitial = (MapGenerator2.initialSettings)EditorGUILayout.EnumPopup("Bedrock height", mapGen.bedrockInitial);
            EditorGUI.indentLevel++;
            if (mapGen.bedrockInitial == MapGenerator2.initialSettings.Constant)
            {
                mapGen.bedrockInitialHeight = EditorGUILayout.FloatField("Height", mapGen.bedrockInitialHeight);
            }
            else if (mapGen.bedrockInitial == MapGenerator2.initialSettings.Random)
            {
                mapGen.bedrockMinHeight = EditorGUILayout.FloatField("Min", mapGen.bedrockMinHeight);
                mapGen.bedrockMaxHeight = EditorGUILayout.FloatField("Max", mapGen.bedrockMaxHeight);
            }
            else if (mapGen.bedrockInitial == MapGenerator2.initialSettings.Noise)
            {
                mapGen.bedrockSeed = EditorGUILayout.IntField("Seed", mapGen.bedrockSeed);
                mapGen.bedrockScale = EditorGUILayout.FloatField("Scale", mapGen.bedrockScale);
                mapGen.bedrockOctaves = EditorGUILayout.IntSlider("Octaves", mapGen.bedrockOctaves, 1, 20);
                mapGen.bedrockPersistence = EditorGUILayout.Slider("Persistence", mapGen.bedrockPersistence, 0, 1);
                mapGen.bedrockLacunarity = EditorGUILayout.FloatField("Lacunarity", mapGen.bedrockLacunarity);
            }
            EditorGUI.indentLevel--;

                
            //Sand
            mapGen.sandInitial = (MapGenerator2.initialSettings)EditorGUILayout.EnumPopup("Sand height", mapGen.sandInitial);
            EditorGUI.indentLevel++;
            if (mapGen.sandInitial == MapGenerator2.initialSettings.Constant)
            {
                mapGen.sandInitialHeight = EditorGUILayout.FloatField("Height", mapGen.sandInitialHeight);
            }
            else if (mapGen.sandInitial == MapGenerator2.initialSettings.Random)
            {
                mapGen.sandMinHeight = EditorGUILayout.FloatField("Min", mapGen.sandMinHeight);
                mapGen.sandMaxHeight = EditorGUILayout.FloatField("Max", mapGen.sandMaxHeight);
            }
            else if (mapGen.sandInitial == MapGenerator2.initialSettings.Noise)
            {
                mapGen.sandSeed = EditorGUILayout.IntField("Seed", mapGen.sandSeed);
                mapGen.sandScale = EditorGUILayout.FloatField("Scale", mapGen.sandScale);
                mapGen.sandOctaves = EditorGUILayout.IntSlider("Octaves", mapGen.sandOctaves, 1, 20);
                mapGen.sandPersistence = EditorGUILayout.Slider("Persistence", mapGen.sandPersistence, 0, 1);
                mapGen.sandLacunarity = EditorGUILayout.FloatField("Lacunarity", mapGen.sandLacunarity);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;



        //blur
        showBlur = EditorGUILayout.ToggleLeft("Blur", showBlur);
        EditorGUI.indentLevel++;
        if (showBlur)
        {
            mapGen.blurType = (MapGenerator2.blurSettings)EditorGUILayout.EnumPopup("Blur Type", mapGen.blurType);
            EditorGUI.indentLevel++;
            if (mapGen.blurType == MapGenerator2.blurSettings.Blur)
            {
                mapGen.blurStrength = EditorGUILayout.FloatField("Blur Strength", mapGen.blurStrength);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;



        //slip
        showSlip = EditorGUILayout.ToggleLeft("Slip", showSlip);
        EditorGUI.indentLevel++;
        if (showSlip)
        {
            mapGen.slipOrder = (MapGenerator2.slipOrders)EditorGUILayout.EnumPopup("Slip Order", mapGen.slipOrder);
            mapGen.crestRoughness = EditorGUILayout.Slider("Crest Roughness", mapGen.crestRoughness, 0, 1);
            mapGen.slipIterations = EditorGUILayout.IntSlider("Slip Iterations", mapGen.slipIterations, 0, 100);
            mapGen.angleOfRepose = EditorGUILayout.FloatField("Angle of Repose", mapGen.angleOfRepose);
            mapGen.spreadAmount = EditorGUILayout.FloatField("Spread Amount", mapGen.spreadAmount);
        }
        EditorGUI.indentLevel--;



        //wind
        showWind = EditorGUILayout.ToggleLeft("Wind", showWind);
        EditorGUI.indentLevel++;
        if (showWind)
        {
            mapGen.universalWindVector = EditorGUILayout.Vector2Field("Wind Vector", mapGen.universalWindVector);
            mapGen.deviationCoefficient = EditorGUILayout.FloatField("Deviation coefficient", mapGen.deviationCoefficient);
        }
        EditorGUI.indentLevel--;



        //debug
        showDebug = EditorGUILayout.ToggleLeft("Debug", showDebug);
        EditorGUI.indentLevel++;
        if (showDebug)
        {
            mapGen.debugPoint = EditorGUILayout.Vector2IntField("Debug Point", mapGen.debugPoint);
            mapGen.debugHeight = EditorGUILayout.FloatField("Height", mapGen.debugHeight);
            mapGen.debugBedrock = EditorGUILayout.FloatField("Bedrock", mapGen.debugBedrock);
            mapGen.debugSand = EditorGUILayout.FloatField("Sand", mapGen.debugSand);
            mapGen.debugNormal = EditorGUILayout.Vector3Field("Normal Vector", mapGen.debugNormal);
            mapGen.debugGradient = EditorGUILayout.Vector2Field("Gradient Vector", mapGen.debugGradient);
            mapGen.debugAngle = EditorGUILayout.FloatField("Angle", mapGen.debugAngle);
            mapGen.debugCrest = EditorGUILayout.FloatField("Crest", mapGen.debugCrest);
            mapGen.debugHighestDiff = EditorGUILayout.FloatField("Highest Diff", mapGen.debugHighestDiff);
            mapGen.debugSlopeThreshold = EditorGUILayout.FloatField("Slope Threshold ", mapGen.debugSlopeThreshold);
            mapGen.debugSaltationVector = EditorGUILayout.Vector2Field("Saltation Vector", mapGen.debugSaltationVector);
        }
        EditorGUI.indentLevel--;



        //-----------------------------------------
        //Detect properties changed
        // if (EditorGUI.EndChangeCheck())
        // { 
        //     mapGen.Initialize();
        //     mapGen.RenderInEditor();
        // }


        





        if (GUILayout.Button("Initialize"))
        {
            mapGen.Initialize();
            mapGen.RenderInEditor();
            mapGen.UpdateMap();
        }

        if (GUILayout.Button("Tick"))
        {
            mapGen.Tick();
        }

        // if (GUILayout.Button("Display wind"))
        // {
        //     mapGen.ShowWind();
        // }



        if (GUILayout.Button("Saltate"))
        {
            mapGen.SaltationCPU(10000);
            mapGen.RecalculateHeights();
            mapGen.RenderInEditor();
        }

        if (GUILayout.Button("Blur"))
        {
            mapGen.BlurGPU();
            mapGen.RecalculateHeights();
            mapGen.RenderInEditor();
        }

        if (GUILayout.Button("Slip"))
        {
            mapGen.SlipCPU(mapGen.slipIterations);
            mapGen.RecalculateHeights();
            mapGen.RenderInEditor();
            mapGen.UpdateMap();
        }

        if (GUILayout.Button("Show wind"))
        {
            mapGen.ShowWind();
        }
    }
}