#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class TrimAndCopyEventsWindow : EditorWindow
{
    private class AnimationPair
    {
        public AnimationClip originalClip;
        public AnimationClip newClip;
    }

    private List<AnimationPair> pairs = new List<AnimationPair>();
    private Vector2 scrollPos;

    [MenuItem("Window/Trim And Copy Events")]
    public static void ShowWindow()
    {
        GetWindow<TrimAndCopyEventsWindow>("Trim & Copy");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("항목 추가"))
        {
            pairs.Add(new AnimationPair());
        }

        scrollPos = GUILayout.BeginScrollView(scrollPos);
        for (int i = 0; i < pairs.Count; i++)
        {
            GUILayout.BeginHorizontal();
            pairs[i].originalClip = (AnimationClip)EditorGUILayout.ObjectField("Original", pairs[i].originalClip, typeof(AnimationClip), false);
            pairs[i].newClip = (AnimationClip)EditorGUILayout.ObjectField("New", pairs[i].newClip, typeof(AnimationClip), false);

            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                pairs.RemoveAt(i);
                i--;
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();

        if (GUILayout.Button("애니메이션 변환 실행"))
        {
            ProcessPairs();
        }
    }

    private void ProcessPairs()
    {
        int trimFrames = 20;

        foreach (var pair in pairs)
        {
            if (pair.originalClip == null || pair.newClip == null) continue;
            TrimAndCopy(pair.originalClip, pair.newClip, trimFrames);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private void TrimAndCopy(AnimationClip originalClip, AnimationClip newClip, int framesToTrim)
    {
        float timeToTrim = framesToTrim / newClip.frameRate;
        AnimationClip processedClip = new AnimationClip();
        processedClip.frameRate = newClip.frameRate;

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(newClip);
        AnimationUtility.SetAnimationClipSettings(processedClip, settings);

        EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(newClip);
        foreach (EditorCurveBinding binding in curveBindings)
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(newClip, binding);
            AnimationCurve newCurve = new AnimationCurve();

            bool excludeZeroCopy = binding.path.Contains("UpperClothes") ||
                                   binding.path.Contains("LowerClothes") ||
                                   binding.path.Contains("sheath");

            if (!excludeZeroCopy)
            {
                Keyframe zeroKey = new Keyframe();
                bool foundZero = false;
                foreach (Keyframe key in curve.keys)
                {
                    if (key.time == 0f || !foundZero)
                    {
                        zeroKey = key;
                        foundZero = true;
                        if (key.time >= 0f) break;
                    }
                }
                zeroKey.time = 0f;
                newCurve.AddKey(zeroKey);
            }

            foreach (Keyframe key in curve.keys)
            {
                if (key.time > timeToTrim + 0.0001f)
                {
                    Keyframe newKey = key;
                    newKey.time -= timeToTrim;
                    newCurve.AddKey(newKey);
                }
            }
            AnimationUtility.SetEditorCurve(processedClip, binding, newCurve);
        }

        EditorCurveBinding[] objectReferenceBindings = AnimationUtility.GetObjectReferenceCurveBindings(newClip);
        foreach (EditorCurveBinding binding in objectReferenceBindings)
        {
            ObjectReferenceKeyframe[] objectReferenceCurves = AnimationUtility.GetObjectReferenceCurve(newClip, binding);
            List<ObjectReferenceKeyframe> newObjectReferenceCurves = new List<ObjectReferenceKeyframe>();

            bool excludeZeroCopy = binding.path.Contains("UpperClothes") ||
                                   binding.path.Contains("LowerClothes") ||
                                   binding.path.Contains("sheath");

            if (!excludeZeroCopy && objectReferenceCurves.Length > 0)
            {
                Object refAtZero = objectReferenceCurves[0].value;
                foreach (var k in objectReferenceCurves)
                {
                    if (k.time <= 0f) refAtZero = k.value;
                    else break;
                }

                ObjectReferenceKeyframe zeroKey = new ObjectReferenceKeyframe();
                zeroKey.time = 0f;
                zeroKey.value = refAtZero;
                newObjectReferenceCurves.Add(zeroKey);
            }

            foreach (ObjectReferenceKeyframe key in objectReferenceCurves)
            {
                if (key.time > timeToTrim + 0.0001f)
                {
                    ObjectReferenceKeyframe newKey = key;
                    newKey.time -= timeToTrim;
                    newObjectReferenceCurves.Add(newKey);
                }
            }
            AnimationUtility.SetObjectReferenceCurve(processedClip, binding, newObjectReferenceCurves.ToArray());
        }

        AnimationEvent[] originalEvents = AnimationUtility.GetAnimationEvents(originalClip);
        AnimationUtility.SetAnimationEvents(processedClip, originalEvents);

        string newPath = AssetDatabase.GetAssetPath(newClip);
        string directory = Path.GetDirectoryName(newPath);
        string newName = newClip.name + "_New.anim";
        string finalPath = Path.Combine(directory, newName);

        finalPath = AssetDatabase.GenerateUniqueAssetPath(finalPath);
        AssetDatabase.CreateAsset(processedClip, finalPath);
    }
}
#endif