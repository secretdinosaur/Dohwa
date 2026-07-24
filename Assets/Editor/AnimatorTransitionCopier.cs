using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class AnimatorTransitionCopier : Editor
{
    [MenuItem("Tools/Duplicate SkillState")]
    public static void DuplicateSkillState()
    {
        AnimatorController controller = Selection.activeObject as AnimatorController;

        if (controller == null)
        {
            Debug.LogError("프로젝트 창에서 Animator Controller를 선택하십시오.");
            return;
        }

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        ChildAnimatorState originalChildState = default;
        bool found = false;

        foreach (var child in sm.states)
        {
            if (child.state.name == "SkillState")
            {
                originalChildState = child;
                found = true;
                break;
            }
        }

        if (!found)
        {
            Debug.LogError("SkillState를 찾을 수 없습니다. 이름을 확인하십시오.");
            return;
        }

        AnimatorState originalState = originalChildState.state;

        CreateAndCopyState(sm, originalState, "SkillState1", originalChildState.position + new Vector3(0, 60, 0));
        CreateAndCopyState(sm, originalState, "SkillState2", originalChildState.position + new Vector3(0, 120, 0));

        Debug.Log("SkillState 복사 및 트랜지션 연결이 완료되었습니다.");
    }

    private static void CreateAndCopyState(AnimatorStateMachine sm, AnimatorState original, string newName, Vector3 position)
    {
        AnimatorState newState = sm.AddState(newName, position);

        newState.motion = original.motion;
        newState.speed = original.speed;
        newState.cycleOffset = original.cycleOffset;
        newState.mirror = original.mirror;
        newState.iKOnFeet = original.iKOnFeet;
        newState.writeDefaultValues = original.writeDefaultValues;

        foreach (var t in original.transitions)
        {
            var newT = newState.AddTransition(t.destinationState);
            CopyTransitionParameters(t, newT);
        }

        foreach (var child in sm.states)
        {
            foreach (var t in child.state.transitions)
            {
                if (t.destinationState == original)
                {
                    var newT = child.state.AddTransition(newState);
                    CopyTransitionParameters(t, newT);
                }
            }
        }

        foreach (var t in sm.anyStateTransitions)
        {
            if (t.destinationState == original)
            {
                var newT = sm.AddAnyStateTransition(newState);
                CopyTransitionParameters(t, newT);
            }
        }
    }

    private static void CopyTransitionParameters(AnimatorStateTransition source, AnimatorStateTransition dest)
    {
        dest.hasExitTime = source.hasExitTime;
        dest.exitTime = source.exitTime;
        dest.hasFixedDuration = source.hasFixedDuration;
        dest.duration = source.duration;
        dest.offset = source.offset;
        dest.interruptionSource = source.interruptionSource;
        dest.orderedInterruption = source.orderedInterruption;
        dest.canTransitionToSelf = source.canTransitionToSelf;

        foreach (var cond in source.conditions)
        {
            dest.AddCondition(cond.mode, cond.threshold, cond.parameter);
        }
    }
}