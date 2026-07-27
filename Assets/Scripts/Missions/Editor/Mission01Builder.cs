using System.Collections.Generic;
using System.IO;
using ShinyMinds.Missions.Data;
using UnityEditor;
using UnityEngine;

namespace ShinyMinds.Missions.EditorTools
{
    /// <summary>
    /// Builds "The Road Home" as a MissionData asset.
    ///
    /// The graph is generated rather than hand-authored in YAML because cutscene
    /// actions use [SerializeReference], whose managed-reference RefIds are not
    /// practical to write by hand. Re-running this rebuilds the asset in place, so
    /// the story stays reviewable as code and the .asset stays reproducible.
    ///
    /// Sprites, audio clips and the speakers' voice ids are intentionally left empty
    /// and are assigned in the Inspector.
    /// </summary>
    public static class Mission01Builder
    {
        const string Folder = "Assets/GameData/Missions";
        const string AssetPath = Folder + "/Mission01_TheRoadHome.asset";

        [MenuItem("ShinyMinds/Build Mission 01 (The Road Home)")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);

            MissionData mission = AssetDatabase.LoadAssetAtPath<MissionData>(AssetPath);
            bool isNew = mission == null;

            if (isNew)
                mission = ScriptableObject.CreateInstance<MissionData>();

            Populate(mission);

            if (isNew)
                AssetDatabase.CreateAsset(mission, AssetPath);

            EditorUtility.SetDirty(mission);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            mission.ValidateGraph();

            Selection.activeObject = mission;
            EditorGUIUtility.PingObject(mission);
            Debug.Log($"Mission 01 built: {mission.nodes.Count} nodes, {mission.endings.Count} endings -> {AssetPath}", mission);
        }

        static void Populate(MissionData m)
        {
            m.missionId = "mission_01_road_home";
            m.title = "The Road Home";
            m.objective = "Walk home from school.";
            m.startNodeId = "s1_open";

            BuildSpeakers(m);
            BuildEndings(m);

            m.nodes = new List<MissionNode>();
            BuildScene1(m.nodes);
            BuildScene2(m.nodes);
            BuildPathA(m.nodes);
            BuildPathB(m.nodes);
            BuildPathC(m.nodes);
        }

        // ------------------------------------------------------------------ cast

        static void BuildSpeakers(MissionData m)
        {
            m.speakers = new List<SpeakerProfile>
            {
                new SpeakerProfile
                {
                    key = "narrator", displayName = "", actorKey = "",
                    nameColor = new Color(0.80f, 0.80f, 0.85f)
                },
                new SpeakerProfile
                {
                    key = "aisha", displayName = "Aisha", actorKey = "aisha",
                    nameColor = new Color(0.45f, 0.80f, 1.00f)
                },
                new SpeakerProfile
                {
                    key = "stranger", displayName = "Stranger", actorKey = "stranger",
                    nameColor = new Color(0.95f, 0.55f, 0.35f)
                },
                new SpeakerProfile
                {
                    key = "teacher", displayName = "Teacher", actorKey = "teacher",
                    nameColor = new Color(0.60f, 0.90f, 0.60f)
                },
                new SpeakerProfile
                {
                    key = "mother", displayName = "Mother", actorKey = "mother",
                    nameColor = new Color(1.00f, 0.75f, 0.85f)
                },
                // Heard in Scene 1 as a remembered voice — she has no body in the world yet.
                new SpeakerProfile
                {
                    key = "mother_memory", displayName = "Mother", actorKey = "",
                    nameColor = new Color(1.00f, 0.75f, 0.85f)
                },
            };
        }

        static void BuildEndings(MissionData m)
        {
            m.endings = new List<MissionEnding>
            {
                new MissionEnding
                {
                    id = "ending_unsafe",
                    quality = EndingQuality.Unsafe,
                    title = "Unsafe Choice",
                    lesson = "Aisha went with someone she did not know.\n\n" +
                             "Never go anywhere with a stranger, even if they know your name " +
                             "or your family's names.",
                    stars = 0,
                    completesMission = false,
                    allowRetry = true,
                    allowContinue = false,
                },
                new MissionEnding
                {
                    id = "ending_safe",
                    quality = EndingQuality.Safe,
                    title = "Safe Choice",
                    lesson = "Aisha stayed safe by walking away.\n\n" +
                             "Walking away from a stranger is a good safety step, but it is not " +
                             "the safest choice.",
                    stars = 2,
                    completesMission = false,
                    allowRetry = true,
                    allowContinue = true,
                },
                new MissionEnding
                {
                    id = "ending_best",
                    quality = EndingQuality.Best,
                    title = "Best Safety Choice",
                    lesson = "When something feels wrong or confusing, seek help from a trusted " +
                             "adult immediately.\n\nThis is the safest choice.",
                    stars = 3,
                    completesMission = true,
                    allowRetry = true,
                    allowContinue = true,
                },
            };
        }

        // -------------------------------------------------------- Scene 1: School Ends

        static void BuildScene1(List<MissionNode> n)
        {
            n.Add(Cutscene("s1_open", "s1_narr",
                Control(false),
                Fade(true, 0f),
                Letterbox(true, 0f),
                Teleport("aisha", "m_aisha_start"),
                Shot("m_cam_gate", 0f),             // authored establishing pose
                Parallel(Sound(null)),              // school bell — assign the clip in the Inspector
                Fade(false, 1.5f),
                Wait(1.0f)));

            // Lines are kept to roughly 42 characters a line, two lines maximum. Long
            // single lines are the main readability failure for a young reader.
            n.Add(Line("s1_narr", "narrator",
                "A warm Tuesday afternoon.\nThe final school bell rings.", "s1_narr2"));

            n.Add(Line("s1_narr2", "narrator",
                "Children pour out of the gates,\nlaughing and talking.", "s1_mother"));

            n.Add(Line("s1_mother", "mother_memory",
                "Come straight home after school, Aisha.", "s1_aisha"));

            n.Add(Line("s1_aisha", "aisha",
                "Okay, Ammi.", "s1_setoff"));

            n.Add(Cutscene("s1_setoff", "s1_walk",
                Shot("m_cam_aisha_cu", 1.0f, "aisha"),
                Wait(1.2f),
                Letterbox(false, 0.4f),
                CameraRelease(0.8f),
                Objective("Walk home along the road."),
                Control(true)));

            // Free-roam segment. The ambient Groq road-safety NPCs are reachable here.
            n.Add(Cutscene("s1_walk", "s2_stop",
                WaitAtMarker("m_road_corner", 3f)));
        }

        // ------------------------------------------------- Scene 2: Someone Calls Her Name

        static void BuildScene2(List<MissionNode> n)
        {
            n.Add(Cutscene("s2_stop", "s2_call",
                Control(false),
                Objective(""),
                Letterbox(true, 0.4f),
                SetActive("stranger", true),
                Teleport("stranger", "m_stranger_spawn"),
                // Open wide. An open street reads as safe, which is the point — the
                // danger here never looks like danger.
                Framed(ShotType.Wide, 1.0f, null, "aisha", "stranger")));

            n.Add(Line("s2_call", "stranger", "Hey! Aisha!", "s2_turn"));

            n.Add(Cutscene("s2_turn", "s2_confirm",
                Parallel(Move("stranger", "m_stranger_call", faceActor: "aisha")),
                Face("aisha", "stranger", 0.6f),
                Framed(ShotType.TwoShot, 1.2f, "stranger", "aisha", "stranger")));

            n.Add(Line("s2_confirm", "stranger", "You are Aisha, right?", "s2_yes"));
            n.Add(Line("s2_yes", "aisha", "Yes...", "s2_think1"));

            n.Add(Thought("s2_think1", "aisha", "How does he know my name?", "s2_thought_so"));

            n.Add(Line("s2_thought_so", "stranger", "I thought so.", "s2_like_mother"));
            n.Add(Line("s2_like_mother", "stranger", "You look just like your mother.", "s2_closer"));

            // He steps closer. Tighten and drop the camera: closer framing reads as
            // pressure without anything overtly frightening happening on screen.
            n.Add(Cutscene("s2_closer", "s2_knows",
                Move("stranger", "m_stranger_close", faceActor: "aisha"),
                Framed(ShotType.OverShoulder, 0.9f, null, "aisha", "stranger", height: -0.1f),
                Push(0.35f, 3.5f, shake: 0.012f)));      // runs on under the next lines

            n.Add(Line("s2_knows", "stranger", "I know your mother, Lia.", "s2_work"));
            n.Add(Line("s2_work", "stranger", "We work together.", "s2_surprised"));

            n.Add(Cutscene("s2_surprised", "s2_offer",
                Framed(ShotType.CloseUp, 0.7f, null, "aisha"),
                Emote("aisha", "Fear", 1.0f)));

            n.Add(Line("s2_offer", "stranger",
                "She asked me to help you\nget home today.", "s2_hesitate"));

            // Hold on her face slightly longer than is comfortable. The pause before the
            // choice is where the lesson actually lands.
            n.Add(Cutscene("s2_hesitate", "s2_think2",
                Emote("aisha", "Sad", 0.8f),
                Framed(ShotType.CloseUp, 1.0f, "aisha", "aisha", angle: 22f),
                Push(0.25f, 4f)));                       // holds under her thought

            n.Add(Thought("s2_think2", "aisha", "I don't know this person.", "s2_choice"));

            var choice = new MissionNode
            {
                id = "s2_choice",
                kind = MissionNodeKind.Choice,
                prompt = "What should Aisha do?",
                choices = new List<MissionChoice>
                {
                    new MissionChoice { label = "Go with the stranger", nextId = "a1_okay",    tone = MissionChoiceTone.Unsafe },
                    new MissionChoice { label = "Walk away",            nextId = "b1_stepback", tone = MissionChoiceTone.Safe },
                    new MissionChoice { label = "Find a trusted adult", nextId = "c1_look",    tone = MissionChoiceTone.Safest },
                },
            };
            n.Add(choice);
        }

        // -------------------------------------------------- Path A: Go With The Stranger

        static void BuildPathA(List<MissionNode> n)
        {
            n.Add(Line("a1_okay", "aisha", "Okay.", "a2_good"));
            n.Add(Line("a2_good", "stranger", "Good choice.", "a3_walkoff"));

            n.Add(Cutscene("a3_walkoff", "a4_end",
                Shot("m_cam_end_a", 1.0f),
                Parallel(Move("stranger", "m_patha_exit_stranger")),
                Move("aisha", "m_patha_exit"),
                Fade(true, 2.0f)));

            n.Add(Ending("a4_end", "ending_unsafe"));
        }

        // ------------------------------------------------------------ Path B: Walk Away

        static void BuildPathB(List<MissionNode> n)
        {
            n.Add(Cutscene("b1_stepback", "b2_nothanks",
                Move("aisha", "m_aisha_stepback", backwards: true),
                Emote("aisha", "Fear", 0.5f)));

            n.Add(Line("b2_nothanks", "aisha", "No thank you.", "b3_insist"));
            n.Add(Line("b3_insist", "stranger", "Your mother asked me to help.", "b4_gohome"));
            n.Add(Line("b4_gohome", "aisha", "I need to go home.", "b5_leave"));

            n.Add(Cutscene("b5_leave", "b6_arrive",
                Move("aisha", "m_home_path", run: true),
                Fade(true, 1.2f)));

            n.Add(Cutscene("b6_arrive", "b7_youre_home",
                Teleport("aisha", "m_home_door"),
                SetActive("stranger", false),
                SetActive("mother", true),
                Teleport("mother", "m_mother_door"),
                Framed(ShotType.TwoShot, 0f, null, "aisha", "mother"),
                Fade(false, 1.0f)));

            n.Add(Line("b7_youre_home", "mother", "You're home!", "b8_tells"));

            n.Add(Line("b8_tells", "narrator",
                "Aisha tells her mother\nwhat happened.", "b8_serious"));

            n.Add(Line("b8_serious", "narrator",
                "Her mother's expression\nbecomes serious.", "b9_safer"));

            n.Add(Line("b9_safer", "mother", "You made a safer choice by walking away.", "b10_better"));
            n.Add(Line("b10_better", "mother", "But there was an even better choice.", "b11_therewas"));
            n.Add(Line("b11_therewas", "aisha", "There was?", "b12_end"));

            n.Add(Ending("b12_end", "ending_safe"));
        }

        // -------------------------------------------------- Path C: Find A Trusted Adult

        static void BuildPathC(List<MissionNode> n)
        {
            n.Add(Cutscene("c1_look", "c2_sees",
                Framed(ShotType.Wide, 0.8f, null, "aisha", "teacher"),
                Face("aisha", "teacher", 0.6f)));

            n.Add(Line("c2_sees", "narrator",
                "Aisha decides not to handle\nthis alone.", "c2_sees2"));

            n.Add(Line("c2_sees2", "narrator",
                "She looks around, and sees\nthe teacher of her class.", "c3_walk"));

            n.Add(Cutscene("c3_walk", "c4_ask",
                Parallel(Framed(ShotType.TwoShot, 1.4f, "aisha", "aisha", "teacher")),
                Move("aisha", "m_aisha_at_teacher", run: true, faceActor: "teacher")));

            n.Add(Line("c4_ask", "aisha",
                "Teacher, this man says he knows my mother.", "c5_hello"));

            n.Add(Line("c5_hello", "teacher", "Hello. Can I help you?", "c6_uncomfortable"));

            n.Add(Cutscene("c6_uncomfortable", "c7_nevermind",
                Face("stranger", "aisha", 0.4f),
                Emote("stranger", "Fear", 0.8f)));

            n.Add(Line("c7_nevermind", "stranger", "Never mind.", "c8_flee"));

            n.Add(Cutscene("c8_flee", "c9_calls",
                Move("stranger", "m_stranger_flee", run: true),
                SetActive("stranger", false)));

            n.Add(Line("c9_calls", "narrator",
                "The teacher stays with Aisha\nand calls her mother.", "c10_arrive"));

            n.Add(Cutscene("c10_arrive", "c11_right_thing",
                Fade(true, 0.8f, hold: 0.4f),
                SetActive("mother", true),
                Teleport("mother", "m_mother_arrive_spawn"),
                Framed(ShotType.Wide, 0f, null, "aisha", "mother"),
                Fade(false, 0.8f),
                Move("mother", "m_mother_arrive", run: true, faceActor: "aisha"),
                Framed(ShotType.TwoShot, 1.0f, null, "aisha", "mother")));

            n.Add(Line("c11_right_thing", "mother", "You did exactly the right thing.", "c12_trusted"));

            n.Add(Line("c12_trusted", "mother",
                "When you're unsure, always find a trusted adult.", "c13_relief"));

            n.Add(Cutscene("c13_relief", "c14_end",
                Emote("aisha", "Laugh", 1.2f)));

            n.Add(Ending("c14_end", "ending_best"));
        }

        // -------------------------------------------------------------- node helpers

        static MissionNode Line(string id, string speaker, string text, string next) =>
            new MissionNode
            {
                id = id, kind = MissionNodeKind.Line,
                speakerKey = speaker, text = text, nextId = next,
            };

        static MissionNode Thought(string id, string speaker, string text, string next) =>
            new MissionNode
            {
                id = id, kind = MissionNodeKind.Thought,
                speakerKey = speaker, text = text, nextId = next,
            };

        static MissionNode Cutscene(string id, string next, params CutsceneAction[] actions) =>
            new MissionNode
            {
                id = id, kind = MissionNodeKind.Cutscene,
                nextId = next, actions = new List<CutsceneAction>(actions),
            };

        static MissionNode Ending(string id, string endingId) =>
            new MissionNode
            {
                id = id, kind = MissionNodeKind.Ending,
                endingId = endingId, nextId = "",
            };

        // ------------------------------------------------------------ action helpers

        static CutsceneAction Parallel(CutsceneAction a)
        {
            a.runInParallel = true;
            return a;
        }

        static WaitAction Wait(float seconds) =>
            new WaitAction { seconds = seconds };

        static SetPlayerControlAction Control(bool allow) =>
            new SetPlayerControlAction { allowControl = allow };

        static FadeScreenAction Fade(bool toBlack, float seconds, float hold = 0f) =>
            new FadeScreenAction { toBlack = toBlack, seconds = seconds, holdSeconds = hold };

        static LetterboxAction Letterbox(bool on, float seconds) =>
            new LetterboxAction { on = on, seconds = seconds };

        static SetObjectiveAction Objective(string text) =>
            new SetObjectiveAction { text = text };

        static TeleportActorAction Teleport(string actor, string marker) =>
            new TeleportActorAction { actorKey = actor, markerKey = marker, snapToGround = true };

        static SetActorActiveAction SetActive(string actor, bool active) =>
            new SetActorActiveAction { actorKey = actor, active = active };

        /// <summary>
        /// A shot computed from where the actors actually are. Preferred over Shot() for
        /// anything covering a conversation — a fixed marker cannot know that an actor
        /// stopped short or approached from a different angle.
        /// </summary>
        static FramedShotAction Framed(ShotType type, float blend, string trackActor,
                                       params string[] subjects) =>
            Framed(type, blend, trackActor, 35f, 0.25f, subjects);

        static FramedShotAction Framed(ShotType type, float blend, string trackActor,
                                       string subjectA, string subjectB,
                                       float angle = 35f, float height = 0.25f) =>
            Framed(type, blend, trackActor, angle, height, subjectA, subjectB);

        static FramedShotAction Framed(ShotType type, float blend, string trackActor,
                                       string subject, float angle) =>
            Framed(type, blend, trackActor, angle, 0.25f, subject);

        static FramedShotAction Framed(ShotType type, float blend, string trackActor,
                                       float angle, float height, params string[] subjects)
        {
            int ignore = LayerMask.NameToLayer("CameraIgnore");

            return new FramedShotAction
            {
                subjectActorKeys = new List<string>(subjects),
                shotType = type,
                blendSeconds = blend,
                trackActorKey = trackActor,
                angleDegrees = angle,
                cameraHeightBias = height,
                letterbox = true,
                // The actors must never count as obstructions between camera and subject.
                obstructionMask = ignore >= 0 ? ~(1 << ignore) : ~0,
            };
        }

        static CameraMoveAction Push(float distance, float seconds, float shake = 0f) =>
            new CameraMoveAction
            {
                pushInDistance = distance,
                seconds = seconds,
                shakeAmplitude = shake,
                shakeDuration = 0.5f,
            };

        static CameraShotAction Shot(string marker, float blend, string lookAtActor = null) =>
            new CameraShotAction
            {
                markerKey = marker, blendSeconds = blend,
                lookAtActorKey = lookAtActor, letterbox = true,
            };

        static CameraReleaseAction CameraRelease(float blend) =>
            new CameraReleaseAction { blendSeconds = blend };

        static MoveActorAction Move(string actor, string marker, bool run = false,
                                    bool backwards = false, string faceActor = null) =>
            new MoveActorAction
            {
                actorKey = actor, markerKey = marker, run = run,
                walkBackwards = backwards, faceActorKeyOnArrive = faceActor,
            };

        static FaceAction Face(string actor, string targetActor, float seconds, string markerKey = null) =>
            new FaceAction
            {
                actorKey = actor, targetActorKey = targetActor,
                targetMarkerKey = markerKey, seconds = seconds,
            };

        static AnimatorParamAction Emote(string actor, string param, float hold) =>
            new AnimatorParamAction
            {
                actorKey = actor, paramName = param,
                kind = AnimatorParamAction.ParamKind.Trigger, holdSeconds = hold,
            };

        static PlaySoundAction Sound(AudioClip clip) =>
            new PlaySoundAction { clip = clip, volume = 1f };

        static WaitForPlayerAtMarkerAction WaitAtMarker(string marker, float radius) =>
            new WaitForPlayerAtMarkerAction { markerKey = marker, radius = radius, ignoreY = true };
    }
}
