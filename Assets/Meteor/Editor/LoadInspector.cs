﻿using UnityEngine;
using System.Collections;
using UnityEditor;
using System.Net;

//[CustomEditor(typeof(SFXUnit))]
//public class SFXUnitInspector : Editor
//{
//    public override void OnInspectorGUI()
//    {
//        base.OnInspectorGUI();
//        if (GUILayout.Button("DumpParticle"))
//        {
//            var obj = (SFXUnit)target;
//            string file = EditorUtility.SaveFilePanel("DumpParticle", "", "Particle", "bytes");
//            if (!string.IsNullOrEmpty(file))
//                obj.SaveParticle(file);
//        }
//    }
//}

[CustomEditor(typeof(AudioButton))]
public class AudioButtonInspector:Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
    }
}

[CustomEditor(typeof(ChangeShader))]
public class ChangeShaderInspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Change"))
        {
            var obj = (ChangeShader)target;
            obj.Change();

        }
    }
}

[CustomEditor(typeof(PMDSave))]
public class PMDSaveInspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Save"))
        {
            var obj = (PMDSave)target;
            string file = EditorUtility.SaveFilePanel("save pmd file", "", "", "pmd");
            if (!string.IsNullOrEmpty(file))
                obj.Save(file);
        }

        if (GUILayout.Button("SaveMap"))
        {
            var obj = (PMDSave)target;
            string file = EditorUtility.SaveFilePanel("save pmd file", "", "", "pmd");
            if (!string.IsNullOrEmpty(file))
                obj.SaveMap(file);
        }
    }
}

[CustomEditor(typeof(Loader))]
public class LoadInspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        Loader myTarget = (Loader)target;
        //if (GUILayout.Button("AddSceneObj"))
        //{
        //    myTarget.LoadSceneObjs(myTarget.LevelId);
        //}
        if (GUILayout.Button("AddSceneObjsFromDes"))
        {
            myTarget.LoadSceneObjsFromDes(myTarget.defFile);
        }
        //if (GUILayout.Button("SaveFixedSceneObjs"))
        //{
            //myTarget.SaveFixedSceneObjs();
        //}
    }
}

[CustomEditor(typeof(FMCPlayer))]
public class FixedTriggerInspector: Editor
{
    string pos = "Pos Id";
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        FMCPlayer myTarget = (FMCPlayer)target;
        pos = GUILayout.TextField(pos);
        if (GUILayout.Button("ChangePose"))
        {
            myTarget.ChangePose(int.Parse(pos), 0);
        }

        if (GUILayout.Button("LoadFile"))
        {
            myTarget.Init(myTarget.fmcFile);
        }
    }
}
[CustomEditor(typeof(IFLLoader))]
public class IFLInspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        IFLLoader myTarget = (IFLLoader)target;
        if (GUILayout.Button("LoadIFL"))
        {
            myTarget.LoadIFL();
        }
    }
}

[CustomEditor(typeof(UIMoveControl))]
public class UIMoveInspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        UIMoveControl myTarget = (UIMoveControl)target;
        if (GUILayout.Button("ShowAction"))
        {
            myTarget.ShowAction();
        }
        if (GUILayout.Button("HideAction"))
        {
            myTarget.HideAction();
        }
    }

    
}

[CustomEditor(typeof(MeteorUnitDebug))]
public class MeteorUnitDebugInspector : Editor
{
    int source = 0;
    int keyframe = 0;
    string src = "source";
    string key = "keyframe";
    string action = "action";
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        MeteorUnitDebug myTarget = (MeteorUnitDebug)target;
        src = GUILayout.TextField(src);
        key = GUILayout.TextField(key);
        action = GUILayout.TextField(action);


        if (GUILayout.Button("SetKeyFrame"))
        {
            keyframe = int.Parse(key);
            source = int.Parse(src);
            if (!string.IsNullOrEmpty(src))
                myTarget.charLoader.ChangeFrame(source, keyframe);
        }

        if (GUILayout.Button("ChangeAction"))
        {
            int act = int.Parse(action);
            myTarget.posMng.ChangeActionSingle(act);
        }
    }
}

//public class StatusMachineEditor
//{
//    [MenuItem("Assets/Create/Meteor/PassCondition")]
//    public static void CreateCondition()
//    {
//        string assetPath = EditorUtility.SaveFilePanelInProject("New PassCondition", "New PassCondition", "asset", "Create a new PassCondition.");
//        if (string.IsNullOrEmpty(assetPath))
//            return;
//        PassCondition asset = ScriptableObject.CreateInstance<PassCondition>();  //scriptable object
//        AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(assetPath));
//        AssetDatabase.Refresh();
//    }

//    [MenuItem("Assets/Create/Meteor/StateMachine")]
//    public static void CreateDatabase()
//    {
//        string assetPath = EditorUtility.SaveFilePanelInProject("New StateMachine", "New StateMachine", "asset", "Create a new StateMachin.");
//        if (string.IsNullOrEmpty(assetPath))
//            return;
//        StatusMachine asset = ScriptableObject.CreateInstance<StatusMachine>();  //scriptable object
//        AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(assetPath));
//        AssetDatabase.Refresh();
//    }
//}

[CustomEditor(typeof(CharacterLoader))]
public class CharacterLoaderInspector : Editor
{
    
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
       
    }
}

[CustomEditor(typeof(CameraSnapShot))]
public class CameraSnapShotInspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        CameraSnapShot myTarget = (CameraSnapShot)target;
        if (GUILayout.Button("SnapShot"))
        {
            myTarget.OnSaveMapFogTexture();
        }
    }
}

[CustomEditor(typeof(MapLoader))]
public class MapLoaderInspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        MapLoader myTarget = (MapLoader)target;
        if (GUILayout.Button("LoadFromXls"))
        {
            myTarget.LoadMap(myTarget.levelId);
        }
        if (GUILayout.Button("LoadFromDes"))
        {
            myTarget.LoadDesMap(myTarget.desFile);
        }
    }
}

[CustomEditor(typeof(CalcDistance))]
public class CalcDistanceInspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        CalcDistance myTarget = (CalcDistance)target;
        if (GUILayout.Button("Calc"))
        {
            myTarget.Calc();
        }
    }
}

[CustomEditor(typeof(MeteorUnit))]
public class MeteorUnitInspector : Editor
{
    public int wayIndex = -1;
    public string way = "";
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        MeteorUnit myTarget = (MeteorUnit)target;
        way = GUILayout.TextField(way);
        if (GUILayout.Button("GetWayIndex"))
        {
            wayIndex = PathMng.Instance.GetWayIndex(myTarget.transform.position, myTarget);
            way = wayIndex.ToString();
        }

        if (GUILayout.Button("GetWayIndex2"))
        {
            wayIndex = PathMng.Instance.GetWayIndex(myTarget.transform.position, null);
            way = wayIndex.ToString();
        }
    }
}