/// <summary>
/// NKLI     : Nigiri - SVO, debug tool inspector
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>
/// 

using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NKLI.Nigiri.SVO
{
    [CustomEditor(typeof(_NKLI_Nigiri_SVO_DebugTool))]
    public class DebugTool_Editor : Editor
    {
        public override void OnInspectorGUI()
        {
            // Setup
            DrawDefaultInspector();

            // Get object reference
            _NKLI_Nigiri_SVO_DebugTool myTarget = (_NKLI_Nigiri_SVO_DebugTool)target;

            EditorGUILayout.LabelField("WARNING! Output data is not spase. Will require VRAM size * 4 in HDD space!");
            EditorGUILayout.LabelField("Operation will require some time to complete on lower end systems.");
            if (GUILayout.Button("Dump out human readable SVO"))
            {
                myTarget.Output_Human_Readable_SVO();
            }
        }
    }
}