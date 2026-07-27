using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using static ShinyMinds.Missions.EditorTools.MissionEditorUtil;

namespace ShinyMinds.Missions.EditorTools
{
    /// <summary>
    /// Sets up the rigs and animator controllers the mission needs.
    ///
    /// Teacher and Mother ship with no animator controller at all, and the
    /// fear/sad/sit states already in PlayerAnimator have no way to be reached
    /// because nothing declares the triggers.
    /// </summary>
    public static class MissionAnimatorBuilder
    {
        public const string MissionActorControllerPath =
            "Assets/Animations/Controllers/MissionActorAnimator.controller";

        const string PlayerControllerPath = "Assets/PlayerAnimator.controller";

        const string ClipIdle = "Assets/animations/NPC_Animations/Look Around.fbx";
        const string ClipWalk = "Assets/animations/Walking.fbx";
        const string ClipRun = "Assets/animations/Slow Run.fbx";
        const string ClipTalk = "Assets/animations/Talking (1).fbx";
        const string ClipFear = "Assets/animations/Fear.fbx";
        const string ClipSad = "Assets/animations/Sitting Disbelief.fbx";
        const string ClipLaugh = "Assets/animations/Laughing.fbx";

        static readonly string[] HumanoidRigs =
        {
            "Assets/characters/Teacher.fbx",
            "Assets/characters/Mother.fbx",
            "Assets/characters/NPC_Characters/Ch29_nonPBR.fbx",
        };

        // Clips that should cycle rather than play once.
        static readonly string[] LoopingClips = { ClipIdle, ClipWalk, ClipRun, ClipTalk };

        // ------------------------------------------------------------------- 3. rigs

        [MenuItem("ShinyMinds/Setup/3. Configure Character Rigs")]
        public static void ConfigureRigs()
        {
            // Do this BEFORE wiring the scene. Re-importing an FBX with a changed rig
            // type can drop m_AddedComponents overrides on its scene instances.
            foreach (string path in HumanoidRigs)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    Debug.LogWarning($"ConfigureRigs: no model importer at '{path}'.");
                    continue;
                }

                if (importer.animationType == ModelImporterAnimationType.Human)
                {
                    Debug.Log($"Rig already Humanoid: {path}");
                    continue;
                }

                importer.animationType = ModelImporterAnimationType.Human;
                importer.SaveAndReimport();
                Debug.Log($"Set rig to Humanoid: {path}");
            }

            foreach (string path in LoopingClips)
                SetClipLooping(path);

            AssetDatabase.SaveAssets();
            Debug.Log("Rigs configured. If you had already run 'Wire Open Scene', run it again — " +
                      "a rig re-import can drop component overrides on scene instances.");
        }

        static void SetClipLooping(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.defaultClipAnimations;

            if (clips == null || clips.Length == 0) return;

            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].loopTime) continue;
                clips[i].loopTime = true;
                changed = true;
            }

            if (!changed) return;

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            Debug.Log($"Set looping: {path}");
        }

        // -------------------------------------------------------------- 4. controllers

        [MenuItem("ShinyMinds/Setup/4. Build Animator Controllers")]
        public static void BuildControllers()
        {
            BuildMissionActorController();
            ExtendPlayerController();
            AssetDatabase.SaveAssets();
        }

        static void BuildMissionActorController()
        {
            EnsureFolder(MissionActorControllerPath);

            // Rebuild from scratch so re-running is deterministic rather than additive.
            AssetDatabase.DeleteAsset(MissionActorControllerPath);
            AnimatorController ac = AnimatorController.CreateAnimatorControllerAtPath(MissionActorControllerPath);

            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Backward", AnimatorControllerParameterType.Bool);
            ac.AddParameter("IsTalking", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Fear", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Sad", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Laugh", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = ac.layers[0].stateMachine;

            // Locomotion blend tree. The thresholds 0 / 2 / 6 must match ActorMover's
            // walkAnimValue and runAnimValue, which in turn match PlayerController.
            AnimatorState locomotion = ac.CreateBlendTreeInController("Locomotion", out BlendTree tree);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;

            AddChildIfPresent(tree, ClipIdle, 0f);
            AddChildIfPresent(tree, ClipWalk, 2f);
            AddChildIfPresent(tree, ClipRun, 6f);

            sm.defaultState = locomotion;

            // Talking, driven by MissionRunner while a line is on screen.
            AnimationClip talkClip = LoadClip(ClipTalk);
            if (talkClip != null)
            {
                AnimatorState talk = sm.AddState("Talking");
                talk.motion = talkClip;

                AnimatorStateTransition toTalk = locomotion.AddTransition(talk);
                toTalk.AddCondition(AnimatorConditionMode.If, 0f, "IsTalking");
                toTalk.hasExitTime = false;
                toTalk.duration = 0.25f;

                AnimatorStateTransition fromTalk = talk.AddTransition(locomotion);
                fromTalk.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsTalking");
                fromTalk.hasExitTime = false;
                fromTalk.duration = 0.25f;
            }

            AddEmote(sm, locomotion, "Fear", ClipFear);
            AddEmote(sm, locomotion, "Sad", ClipSad);
            AddEmote(sm, locomotion, "Laugh", ClipLaugh);

            EditorUtility.SetDirty(ac);
            Debug.Log($"Built {MissionActorControllerPath}", ac);
        }

        static void ExtendPlayerController()
        {
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
            if (ac == null)
            {
                Debug.LogError($"No animator controller at '{PlayerControllerPath}'.");
                return;
            }

            AnimatorStateMachine sm = ac.layers[0].stateMachine;

            // fear / sad / sit already exist as states but are unreachable, because
            // nothing declares the triggers. Reuse them rather than adding duplicates.
            AddTriggerIfMissing(ac, "Fear");
            AddTriggerIfMissing(ac, "Sad");
            AddTriggerIfMissing(ac, "Sit");
            AddTriggerIfMissing(ac, "Laugh");

            AnimatorState idle = FindState(sm, "idle") ?? sm.defaultState;
            if (idle == null)
            {
                Debug.LogError("PlayerAnimator has no 'idle' state to return to.");
                return;
            }

            LinkExistingEmote(sm, idle, "fear", "Fear");
            LinkExistingEmote(sm, idle, "sad", "Sad");
            LinkExistingEmote(sm, idle, "sit", "Sit");

            // No laugh state ships with PlayerAnimator; add one.
            if (FindState(sm, "laugh") == null)
            {
                AnimationClip laugh = LoadClip(ClipLaugh);
                if (laugh != null)
                {
                    AnimatorState state = sm.AddState("laugh");
                    state.motion = laugh;
                    LinkExistingEmote(sm, idle, "laugh", "Laugh");
                }
            }

            EditorUtility.SetDirty(ac);
            Debug.Log($"Extended {PlayerControllerPath} with Fear / Sad / Sit / Laugh.", ac);
        }

        // ------------------------------------------------------------------ helpers

        static void AddEmote(AnimatorStateMachine sm, AnimatorState returnTo, string trigger, string clipPath)
        {
            AnimationClip clip = LoadClip(clipPath);
            if (clip == null) return;

            AnimatorState state = sm.AddState(trigger);
            state.motion = clip;

            AnimatorStateTransition enter = sm.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            // Guard on Speed so a stray trigger cannot interrupt a walk.
            enter.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            enter.hasExitTime = false;
            enter.duration = 0.25f;
            enter.canTransitionToSelf = false;

            AnimatorStateTransition exit = state.AddTransition(returnTo);
            exit.hasExitTime = true;
            exit.exitTime = 0.9f;
            exit.duration = 0.25f;
        }

        static void LinkExistingEmote(AnimatorStateMachine sm, AnimatorState idle, string stateName, string trigger)
        {
            AnimatorState state = FindState(sm, stateName);
            if (state == null)
            {
                Debug.LogWarning($"PlayerAnimator has no '{stateName}' state; skipping trigger '{trigger}'.");
                return;
            }

            bool alreadyLinked = sm.anyStateTransitions.Any(
                t => t.destinationState == state &&
                     t.conditions.Any(c => c.parameter == trigger));

            if (!alreadyLinked)
            {
                AnimatorStateTransition enter = sm.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
                enter.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
                enter.hasExitTime = false;
                enter.duration = 0.25f;
                enter.canTransitionToSelf = false;
            }

            if (!state.transitions.Any(t => t.destinationState == idle))
            {
                AnimatorStateTransition exit = state.AddTransition(idle);
                exit.hasExitTime = true;
                exit.exitTime = 0.9f;
                exit.duration = 0.25f;
            }
        }

        static void AddTriggerIfMissing(AnimatorController ac, string name)
        {
            if (ac.parameters.Any(p => p.name == name)) return;
            ac.AddParameter(name, AnimatorControllerParameterType.Trigger);
        }

        static AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            foreach (ChildAnimatorState c in sm.states)
                if (string.Equals(c.state.name, name, System.StringComparison.OrdinalIgnoreCase))
                    return c.state;
            return null;
        }

        static void AddChildIfPresent(BlendTree tree, string clipPath, float threshold)
        {
            AnimationClip clip = LoadClip(clipPath);
            if (clip == null) return;

            tree.AddChild(clip, threshold);
        }

        /// <summary>Pulls the real AnimationClip out of an imported FBX, skipping previews.</summary>
        static AnimationClip LoadClip(string path)
        {
            IEnumerable<AnimationClip> clips = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"));

            AnimationClip clip = clips.FirstOrDefault();

            if (clip == null)
                Debug.LogWarning($"No AnimationClip found in '{path}'.");

            return clip;
        }
    }
}
