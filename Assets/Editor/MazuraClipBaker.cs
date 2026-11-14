using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bakes Humanoid (Mixamo) clips onto the Generic Adventurer skeleton, so the player model stays untouched.
/// Finger bones, Tool_Axe and pole targets are NOT keyed, so the hand grip layers keep working.
/// Foot.L/R are IK-style bones under Root, so they are rebuilt from the lower-leg end bones.
/// </summary>
public static class MazuraClipBaker
{
    const string ProxyPath = "Assets/Project Assest/player animation/Retarget/Adventurer_HumanoidProxy.fbx";
    const string OutDir = "Assets/Project Assest/player animation/Baked";
    const float Fps = 30f;

    static readonly string[] Bones =
    {
        "Body","Abdomen","Torso","Chest","Neck","Head",
        "Shoulder.L","UpperArm.L","LowerArm.L","Wrist.L",
        "Shoulder.R","UpperArm.R","LowerArm.R","Wrist.R",
        "UpperLeg.L","LowerLeg.L","UpperLeg.R","LowerLeg.R"
    };

    /// <summary>Bakes one Mixamo FBX (at srcFbx) to OutDir/outName.anim. Returns a short report.</summary>
    public static string Bake(string srcFbx, string outName, bool loop)
    {
        if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/Project Assest/player animation", "Baked");

        AnimationClip src = null;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(srcFbx))
        {
            AnimationClip c = o as AnimationClip;
            if (c != null && !c.name.StartsWith("__preview")) { src = c; break; }
        }
        if (src == null) return outName + ": no source clip";

        Avatar avatar = null;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(ProxyPath)) if (o is Avatar) avatar = (Avatar)o;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProxyPath);
        GameObject go = (GameObject)Object.Instantiate(prefab);
        go.name = "__BakeProxy";
        Animator an = go.GetComponent<Animator>();
        if (an == null) an = go.AddComponent<Animator>();
        an.avatar = avatar;
        an.applyRootMotion = false;

        Transform root = go.transform.Find("CharacterArmature/Root");
        Dictionary<string, Transform> map = new Dictionary<string, Transform>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>()) if (!map.ContainsKey(t.name)) map[t.name] = t;

        // Rest relation of each foot to its lower-leg end bone (local space of the end bone)
        string[] sides = { "L", "R" };
        Vector3[] relPos = new Vector3[2];
        Quaternion[] relRot = new Quaternion[2];
        for (int s = 0; s < 2; s++)
        {
            Transform end = map["LowerLeg." + sides[s] + "_end"];
            Transform foot = map["Foot." + sides[s]];
            relPos[s] = end.InverseTransformPoint(foot.position);
            relRot[s] = Quaternion.Inverse(end.rotation) * foot.rotation;
        }

        int frames = Mathf.Max(2, Mathf.RoundToInt(src.length * Fps) + 1);
        Dictionary<string, AnimationCurve[]> rot = new Dictionary<string, AnimationCurve[]>();
        Dictionary<string, AnimationCurve[]> pos = new Dictionary<string, AnimationCurve[]>();
        List<string> keyed = new List<string>(Bones); keyed.Add("Foot.L"); keyed.Add("Foot.R");
        foreach (string b in keyed) { rot[b] = NewCurves(4); }
        pos["Body"] = NewCurves(3); pos["Foot.L"] = NewCurves(3); pos["Foot.R"] = NewCurves(3);

        for (int i = 0; i < frames; i++)
        {
            float t = Mathf.Min(i / Fps, src.length);
            src.SampleAnimation(go, t);
            for (int s = 0; s < 2; s++)
            {
                Transform end = map["LowerLeg." + sides[s] + "_end"];
                Transform foot = map["Foot." + sides[s]];
                foot.position = end.TransformPoint(relPos[s]);
                foot.rotation = end.rotation * relRot[s];
            }
            foreach (string b in keyed)
            {
                Transform tr = map[b];
                Quaternion q = tr.localRotation;
                AnimationCurve[] rc = rot[b];
                rc[0].AddKey(t, q.x); rc[1].AddKey(t, q.y); rc[2].AddKey(t, q.z); rc[3].AddKey(t, q.w);
                if (pos.ContainsKey(b))
                {
                    Vector3 p = tr.localPosition; AnimationCurve[] pc = pos[b];
                    pc[0].AddKey(t, p.x); pc[1].AddKey(t, p.y); pc[2].AddKey(t, p.z);
                }
            }
        }

        AnimationClip outClip = new AnimationClip();
        outClip.frameRate = Fps;
        foreach (string b in keyed)
        {
            string path = PathOf(map[b], go.transform);
            AnimationCurve[] rc = rot[b];
            outClip.SetCurve(path, typeof(Transform), "localRotation.x", rc[0]);
            outClip.SetCurve(path, typeof(Transform), "localRotation.y", rc[1]);
            outClip.SetCurve(path, typeof(Transform), "localRotation.z", rc[2]);
            outClip.SetCurve(path, typeof(Transform), "localRotation.w", rc[3]);
            if (pos.ContainsKey(b))
            {
                AnimationCurve[] pc = pos[b];
                outClip.SetCurve(path, typeof(Transform), "localPosition.x", pc[0]);
                outClip.SetCurve(path, typeof(Transform), "localPosition.y", pc[1]);
                outClip.SetCurve(path, typeof(Transform), "localPosition.z", pc[2]);
            }
        }
        outClip.EnsureQuaternionContinuity();
        AnimationClipSettings st = AnimationUtility.GetAnimationClipSettings(outClip);
        st.loopTime = loop; st.loopBlendOrientation = false;
        AnimationUtility.SetAnimationClipSettings(outClip, st);
        outClip.name = outName;

        string outPath = OutDir + "/" + outName + ".anim";
        AssetDatabase.DeleteAsset(outPath);
        AssetDatabase.CreateAsset(outClip, outPath);
        Object.DestroyImmediate(go);
        return outName + ": ok " + frames + " frames, " + src.length.ToString("0.00") + "s";
    }

    static AnimationCurve[] NewCurves(int n)
    {
        AnimationCurve[] c = new AnimationCurve[n];
        for (int i = 0; i < n; i++) c[i] = new AnimationCurve();
        return c;
    }

    static string PathOf(Transform t, Transform root)
    {
        string p = t.name;
        while (t.parent != null && t.parent != root) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
