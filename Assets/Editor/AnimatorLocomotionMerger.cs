using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;

public class AnimatorLocomotionMerger : EditorWindow
{
    public AnimatorController targetController;

    [Header("Idle Clip")]
    public AnimationClip idleClip;

    [Header("Walk Clips (2D Directional)")]
    public AnimationClip walkForwardClip;
    public AnimationClip walkBackwardClip;
    public AnimationClip walkLeftClip;
    public AnimationClip walkRightClip;

    [Header("Run Clips (2D Directional)")]
    public AnimationClip runForwardClip;
    public AnimationClip runBackwardClip;
    public AnimationClip runLeftClip;
    public AnimationClip runRightClip;

    private readonly string[] statesToMerge = { "Idle", "Walk", "Run", "BackWalk" };

    [MenuItem("Tools/Merge Locomotion States")]
    public static void ShowWindow()
    {
        GetWindow<AnimatorLocomotionMerger>("Merge Locomotion");
    }

    private void OnGUI()
    {
        GUILayout.Label("Locomotion Blend Tree Merger (1D -> 2D Nested)", EditorStyles.boldLabel);

        targetController = (AnimatorController)EditorGUILayout.ObjectField("Animator Controller", targetController, typeof(AnimatorController), false);

        EditorGUILayout.Space();
        GUILayout.Label("Animation Clips Setup", EditorStyles.boldLabel);
        idleClip = (AnimationClip)EditorGUILayout.ObjectField("Idle Clip", idleClip, typeof(AnimationClip), false);

        EditorGUILayout.Space();
        GUILayout.Label("Walk Directional Clips", EditorStyles.miniBoldLabel);
        walkForwardClip = (AnimationClip)EditorGUILayout.ObjectField("Walk Forward", walkForwardClip, typeof(AnimationClip), false);
        walkBackwardClip = (AnimationClip)EditorGUILayout.ObjectField("Walk Backward", walkBackwardClip, typeof(AnimationClip), false);
        walkLeftClip = (AnimationClip)EditorGUILayout.ObjectField("Walk Left", walkLeftClip, typeof(AnimationClip), false);
        walkRightClip = (AnimationClip)EditorGUILayout.ObjectField("Walk Right", walkRightClip, typeof(AnimationClip), false);

        EditorGUILayout.Space();
        GUILayout.Label("Run Directional Clips", EditorStyles.miniBoldLabel);
        runForwardClip = (AnimationClip)EditorGUILayout.ObjectField("Run Forward", runForwardClip, typeof(AnimationClip), false);
        runBackwardClip = (AnimationClip)EditorGUILayout.ObjectField("Run Backward", runBackwardClip, typeof(AnimationClip), false);
        runLeftClip = (AnimationClip)EditorGUILayout.ObjectField("Run Left", runLeftClip, typeof(AnimationClip), false);
        runRightClip = (AnimationClip)EditorGUILayout.ObjectField("Run Right", runRightClip, typeof(AnimationClip), false);

        EditorGUILayout.Space();

        if (GUILayout.Button("Merge States into Nested BlendTree"))
        {
            if (targetController != null)
            {
                MergeStates();
                EditorUtility.DisplayDialog("완료", "1D-2D 중첩 블렌드 트리 생성 및 좌우 Run 클립 반영이 완료되었습니다.", "확인");
            }
            else
            {
                EditorUtility.DisplayDialog("오류", "Animator Controller를 할당해 주십시오.", "확인");
            }
        }
    }

    private void MergeStates()
    {
        AddParameterIfNotExists(targetController, "Speed", AnimatorControllerParameterType.Float);
        AddParameterIfNotExists(targetController, "MoveX", AnimatorControllerParameterType.Float);
        AddParameterIfNotExists(targetController, "MoveZ", AnimatorControllerParameterType.Float);

        AnimatorStateMachine rootStateMachine = targetController.layers[0].stateMachine;

        BlendTree locomotionTree;
        AnimatorState locomotionState = targetController.CreateBlendTreeInController("Locomotion", out locomotionTree, 0);
        locomotionTree.blendType = BlendTreeType.Simple1D;
        locomotionTree.blendParameter = "Speed";

        BlendTree walkTree = new BlendTree();
        walkTree.name = "Walk";
        walkTree.blendType = BlendTreeType.FreeformDirectional2D;
        walkTree.blendParameter = "MoveX";
        walkTree.blendParameterY = "MoveZ";
        walkTree.AddChild(walkForwardClip, new Vector2(0f, 1f));
        walkTree.AddChild(walkBackwardClip, new Vector2(0f, -1f));
        walkTree.AddChild(walkLeftClip, new Vector2(-1f, 0f));
        walkTree.AddChild(walkRightClip, new Vector2(1f, 0f));
        AssetDatabase.AddObjectToAsset(walkTree, targetController);

        BlendTree runTree = new BlendTree();
        runTree.name = "Run";
        runTree.blendType = BlendTreeType.FreeformDirectional2D;
        runTree.blendParameter = "MoveX";
        runTree.blendParameterY = "MoveZ";
        runTree.AddChild(runForwardClip, new Vector2(0f, 1f));
        runTree.AddChild(runBackwardClip, new Vector2(0f, -1f));
        runTree.AddChild(runLeftClip, new Vector2(-1f, 0f));
        runTree.AddChild(runRightClip, new Vector2(1f, 0f));
        AssetDatabase.AddObjectToAsset(runTree, targetController);

        locomotionTree.AddChild(idleClip, 0f);
        locomotionTree.AddChild(walkTree, 0.5f);
        locomotionTree.AddChild(runTree, 1f);

        if (rootStateMachine.defaultState != null && statesToMerge.Contains(rootStateMachine.defaultState.name))
        {
            rootStateMachine.defaultState = locomotionState;
        }

        foreach (var t in rootStateMachine.anyStateTransitions)
        {
            if (t.destinationState != null && statesToMerge.Contains(t.destinationState.name))
            {
                t.destinationState = locomotionState;
            }
        }

        foreach (var state in rootStateMachine.states)
        {
            if (statesToMerge.Contains(state.state.name)) continue;

            foreach (var t in state.state.transitions)
            {
                if (t.destinationState != null && statesToMerge.Contains(t.destinationState.name))
                {
                    t.destinationState = locomotionState;
                }
            }
        }

        List<AnimatorStateTransition> processedTransitions = new List<AnimatorStateTransition>();

        foreach (var childState in rootStateMachine.states)
        {
            if (!statesToMerge.Contains(childState.state.name)) continue;

            foreach (var t in childState.state.transitions)
            {
                if (t.destinationState != null && statesToMerge.Contains(t.destinationState.name)) continue;

                if (!IsDuplicateTransition(processedTransitions, t))
                {
                    AnimatorStateTransition newTransition = locomotionState.AddTransition(t.destinationState);
                    CopyTransitionProperties(t, newTransition);
                    processedTransitions.Add(newTransition);
                }
            }
        }

        for (int i = rootStateMachine.states.Length - 1; i >= 0; i--)
        {
            var childState = rootStateMachine.states[i];
            if (statesToMerge.Contains(childState.state.name))
            {
                rootStateMachine.RemoveState(childState.state);
            }
        }

        AssetDatabase.SaveAssets();
    }

    private void AddParameterIfNotExists(AnimatorController controller, string paramName, AnimatorControllerParameterType type)
    {
        if (!controller.parameters.Any(p => p.name == paramName))
        {
            controller.AddParameter(paramName, type);
        }
    }

    private void CopyTransitionProperties(AnimatorStateTransition source, AnimatorStateTransition target)
    {
        target.hasExitTime = source.hasExitTime;
        target.exitTime = source.exitTime;
        target.hasFixedDuration = source.hasFixedDuration;
        target.duration = source.duration;
        target.offset = source.offset;
        target.interruptionSource = source.interruptionSource;
        target.orderedInterruption = source.orderedInterruption;
        target.canTransitionToSelf = source.canTransitionToSelf;

        foreach (var cond in source.conditions)
        {
            target.AddCondition(cond.mode, cond.threshold, cond.parameter);
        }
    }

    private bool IsDuplicateTransition(List<AnimatorStateTransition> list, AnimatorStateTransition t)
    {
        foreach (var existing in list)
        {
            if (existing.destinationState == t.destinationState && existing.conditions.Length == t.conditions.Length)
            {
                bool conditionsMatch = true;
                for (int i = 0; i < existing.conditions.Length; i++)
                {
                    var cond1 = existing.conditions[i];
                    var cond2 = t.conditions[i];
                    if (cond1.mode != cond2.mode || cond1.parameter != cond2.parameter || cond1.threshold != cond2.threshold)
                    {
                        conditionsMatch = false;
                        break;
                    }
                }
                if (conditionsMatch) return true;
            }
        }
        return false;
    }
}