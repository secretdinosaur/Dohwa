#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class AnimationTrimmerOnly
{
    [MenuItem("Assets/Trim First 20 Frames Only")]
    public static void TrimAnimations()
    {
        int trimFrames = 100;

        foreach (Object obj in Selection.objects)
        {
            if (obj is AnimationClip clip)
            {
                TrimClip(clip, trimFrames);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void TrimClip(AnimationClip clip, int framesToTrim)
    {
        float timeToTrim = framesToTrim / clip.frameRate;
        float bakeTime = (framesToTrim - 1) / clip.frameRate;

        AnimationClip newClip = new AnimationClip();
        newClip.frameRate = clip.frameRate;

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        AnimationUtility.SetAnimationClipSettings(newClip, settings);

        EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(clip);
        foreach (EditorCurveBinding binding in curveBindings)
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            AnimationCurve newCurve = new AnimationCurve();

            bool excludeZeroCopy = binding.path.Contains("UpperClothes") ||
                                   binding.path.Contains("LowerClothes") ||
                                   binding.path.Contains("sheath");

            if (!excludeZeroCopy)
            {
                float bakedValue = curve.Evaluate(bakeTime);
                Keyframe zeroKey = new Keyframe(0f, bakedValue);
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
            AnimationUtility.SetEditorCurve(newClip, binding, newCurve);
        }

        EditorCurveBinding[] objectReferenceBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        foreach (EditorCurveBinding binding in objectReferenceBindings)
        {
            ObjectReferenceKeyframe[] objectReferenceCurves = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            List<ObjectReferenceKeyframe> newObjectReferenceCurves = new List<ObjectReferenceKeyframe>();

            bool excludeZeroCopy = binding.path.Contains("UpperClothes") ||
                                   binding.path.Contains("LowerClothes") ||
                                   binding.path.Contains("sheath");

            if (!excludeZeroCopy && objectReferenceCurves.Length > 0)
            {
                Object refAtBakeTime = objectReferenceCurves[0].value;
                foreach (var k in objectReferenceCurves)
                {
                    if (k.time <= bakeTime) refAtBakeTime = k.value;
                    else break;
                }

                ObjectReferenceKeyframe zeroKey = new ObjectReferenceKeyframe();
                zeroKey.time = 0f;
                zeroKey.value = refAtBakeTime;
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
            AnimationUtility.SetObjectReferenceCurve(newClip, binding, newObjectReferenceCurves.ToArray());
        }

        AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
        List<AnimationEvent> newEvents = new List<AnimationEvent>();
        foreach (AnimationEvent ev in events)
        {
            if (ev.time >= timeToTrim)
            {
                AnimationEvent newEv = ev;
                newEv.time -= timeToTrim;
                newEvents.Add(newEv);
            }
        }
        AnimationUtility.SetAnimationEvents(newClip, newEvents.ToArray());

        string originalPath = AssetDatabase.GetAssetPath(clip);
        string directory = Path.GetDirectoryName(originalPath);
        string newName = clip.name + "_New.anim";
        string newPath = Path.Combine(directory, newName);

        newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);
        AssetDatabase.CreateAsset(newClip, newPath);
    }
}
#endif