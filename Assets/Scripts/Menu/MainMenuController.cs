using System.Collections.Generic;
using ShinyMinds.Api;
using ShinyMinds.Core;
using ShinyMinds.Progress;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShinyMinds.Menu
{
    /// <summary>
    /// The opening screen. Drop this on a single GameObject in the MainMenu scene and
    /// it builds and drives everything: sign in, create account, then Continue,
    /// New Game, Missions and Profile.
    ///
    /// Nothing here is serialised, so the scene file stays a few lines long and the
    /// menu can be reviewed as ordinary code.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        private enum Screen
        {
            Loading,
            Auth,
            Main,
            Missions,
            Profile,
            ConfirmNewGame,
        }

        private const string BackgroundResourcePath = "UI/menu_background";

        /// <summary>
        /// The only mission that exists as playable content. The backend seeds the whole
        /// eight-mission catalogue and unlocks them in order, but only this one has been
        /// built in the scene — the rest would drop the player into an empty city.
        ///
        /// This is the backend row titled "The Road Home"; the Unity asset is
        /// mission_01_road_home. Delete this gate once a second mission ships.
        /// </summary>
        private const string PlayableMissionCode = "school_zone";

        private const string ComingSoonStatus = "Coming soon";

        private Canvas _canvas;
        private RectTransform _authPanel;
        private RectTransform _mainPanel;
        private RectTransform _missionsPanel;
        private RectTransform _profilePanel;
        private RectTransform _confirmNewGamePanel;

        // Auth screen
        private bool _registerMode;
        private TextMeshProUGUI _authTitle;
        private TMP_InputField _usernameField;
        private TMP_InputField _passwordField;
        private TMP_InputField _displayNameField;
        private TMP_InputField _ageField;
        private TMP_InputField _linkCodeField;
        private RectTransform _registerOnlyGroup;
        private TextMeshProUGUI _authMessage;
        private Button _authSubmit;
        private Button _authToggle;

        // Main screen
        private TextMeshProUGUI _greeting;
        private TextMeshProUGUI _progressLine;
        private Button _continueButton;
        private TextMeshProUGUI _continueLabel;

        // Which mission the Continue button acts on, resolved once per visit to the main
        // screen by ApplyProfile.
        private ContinueMission _startTarget;

        // Missions and profile
        private RectTransform _missionList;
        private TextMeshProUGUI _profileBody;
        private RectTransform _linkParentGroup;
        private TMP_InputField _linkParentField;
        private Button _linkParentButton;
        private TextMeshProUGUI _linkParentMessage;
        private TextMeshProUGUI _statusLine;

        // Fixed display order for skills. The profile arrives as a dictionary, whose
        // iteration order is not guaranteed, so listing it directly would let the four
        // skills reorder themselves between visits.
        private static readonly string[] SkillOrder =
        {
            "SAFETY",
            "COMMUNICATION",
            "EMPATHY",
            "CONFIDENCE",
        };

        private PlayerProfile _profile;
        private readonly List<GameObject> _missionRows = new List<GameObject>();

        private void Start()
        {
            // The cursor is global state that survives LoadScene, and gameplay leaves it
            // locked and hidden. Without this claim the menu is unusable on the way back
            // from a mission, and in the Editor the lock carries into the next Play session.
            PlayerInputLock.PushCursorFree(this);

            // Coming back from gameplay: the previous run's session is finished here so
            // its playtime is recorded even if the player never quits the application.
            if (GameProgressTracker.Instance.HasActiveSession)
            {
                GameProgressTracker.Instance.EndSession();
            }

            GameFlow.ClearSelection();

            BuildUi();

            if (PlayerSession.IsSignedIn)
            {
                Show(Screen.Loading);
                RefreshProfile();
            }
            else
            {
                Show(Screen.Auth);
            }
        }

        private void OnDestroy()
        {
            // Released as the menu unloads, so the gameplay scene re-locks the cursor on
            // its own. Leaving the claim behind would free the cursor during a mission.
            PlayerInputLock.PopCursorFree(this);
        }

        // ---------------------------------------------------------------- build

        private void BuildUi()
        {
            _canvas = MenuUI.CreateCanvas("MainMenuCanvas", transform);

            MenuUI.CreateBackground(_canvas.transform, LoadBackgroundSprite());

            BuildAuthPanel();
            BuildMainPanel();
            BuildMissionsPanel();
            BuildProfilePanel();
            BuildConfirmNewGamePanel();

            _statusLine = MenuUI.CreateText(
                _canvas.transform,
                string.Empty,
                MenuTheme.SmallFontSize,
                MenuTheme.Ink,
                TextAlignmentOptions.Center);

            RectTransform statusRect = _statusLine.rectTransform;
            statusRect.anchorMin = new Vector2(0.5f, 0f);
            statusRect.anchorMax = new Vector2(0.5f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.anchoredPosition = new Vector2(0f, 28f);
            statusRect.sizeDelta = new Vector2(MenuTheme.ColumnWidth, 30f);
        }

        /// <summary>
        /// Loads the menu artwork from Resources and wraps it in a sprite. Loading at
        /// runtime keeps the texture out of the scene file and needs no import settings.
        /// </summary>
        private static Sprite LoadBackgroundSprite()
        {
            Texture2D texture = Resources.Load<Texture2D>(BackgroundResourcePath);

            if (texture == null)
            {
                Debug.LogWarning(
                    $"[MainMenu] No background at Assets/Resources/{BackgroundResourcePath}. " +
                    "Falling back to a plain sky colour.");

                return null;
            }

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private void BuildAuthPanel()
        {
            _authPanel = MenuUI.CreatePanel(_canvas.transform, "AuthPanel", MenuTheme.ColumnWidth);

            MenuUI.CreateText(_authPanel, "ShinyMinds", MenuTheme.TitleFontSize, MenuTheme.Primary,
                TextAlignmentOptions.Center, FontStyles.Bold);

            _authTitle = MenuUI.CreateText(_authPanel, "Welcome back!", MenuTheme.HeadingFontSize,
                MenuTheme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);

            _usernameField = MenuUI.CreateField(_authPanel, "Username");
            _passwordField = MenuUI.CreateField(_authPanel, "Password", true);

            // Fields only needed when creating an account are grouped so the whole group
            // can be hidden in sign-in mode.
            GameObject group = new GameObject("RegisterFields", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            group.transform.SetParent(_authPanel, false);

            VerticalLayoutGroup groupLayout = group.GetComponent<VerticalLayoutGroup>();
            groupLayout.spacing = 16f;
            groupLayout.childControlWidth = true;
            groupLayout.childControlHeight = true;
            groupLayout.childForceExpandWidth = true;
            groupLayout.childForceExpandHeight = false;

            group.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _registerOnlyGroup = group.GetComponent<RectTransform>();

            _displayNameField = MenuUI.CreateField(_registerOnlyGroup, "Your name");
            _ageField = MenuUI.CreateField(_registerOnlyGroup, "Age", false,
                TMP_InputField.CharacterValidation.Integer);
            _linkCodeField = MenuUI.CreateField(_registerOnlyGroup, "Parent code (optional)");

            _authMessage = MenuUI.CreateText(_authPanel, string.Empty, MenuTheme.SmallFontSize,
                MenuTheme.ErrorText, TextAlignmentOptions.Center);

            _authSubmit = MenuUI.CreateButton(_authPanel, "Sign In", MenuTheme.Primary, SubmitAuth);
            _authToggle = MenuUI.CreateButton(_authPanel, "Create an account", MenuTheme.Secondary,
                () => SetRegisterMode(!_registerMode), 56f);

            SetRegisterMode(false);
        }

        private void BuildMainPanel()
        {
            _mainPanel = MenuUI.CreatePanel(_canvas.transform, "MainPanel", MenuTheme.ColumnWidth);

            _greeting = MenuUI.CreateText(_mainPanel, "Hello!", MenuTheme.TitleFontSize,
                MenuTheme.Primary, TextAlignmentOptions.Center, FontStyles.Bold);

            _progressLine = MenuUI.CreateText(_mainPanel, string.Empty, MenuTheme.BodyFontSize,
                MenuTheme.InkMuted, TextAlignmentOptions.Center);

            MenuUI.SetSpacer(_mainPanel, 8f);

            _continueButton = MenuUI.CreateButton(_mainPanel, "Continue", MenuTheme.Primary, OnContinue);
            _continueLabel = _continueButton.GetComponentInChildren<TextMeshProUGUI>();

            MenuUI.CreateButton(_mainPanel, "New Game", MenuTheme.Secondary, OnNewGame);
            MenuUI.CreateButton(_mainPanel, "Missions", MenuTheme.Secondary, OnOpenMissions);
            MenuUI.CreateButton(_mainPanel, "My Progress", MenuTheme.Secondary, OnOpenProfile);

            MenuUI.SetSpacer(_mainPanel, 8f);

            RectTransform row = MenuUI.CreateRow(_mainPanel, 12f, 56f);

            MenuUI.CreateButton(row, "Sign Out", MenuTheme.Disabled, OnSignOut, 56f);
            MenuUI.CreateButton(row, "Quit", MenuTheme.Danger, OnQuit, 56f);
        }

        private void BuildMissionsPanel()
        {
            _missionsPanel = MenuUI.CreatePanel(_canvas.transform, "MissionsPanel", MenuTheme.ColumnWidth + 60f);

            MenuUI.CreateText(_missionsPanel, "Missions", MenuTheme.HeadingFontSize, MenuTheme.Ink,
                TextAlignmentOptions.Center, FontStyles.Bold);

            MenuUI.CreateText(_missionsPanel,
                "Finish a mission to unlock the next one.",
                MenuTheme.SmallFontSize, MenuTheme.InkMuted);

            _missionList = MenuUI.CreateScrollList(_missionsPanel, 520f);

            MenuUI.CreateButton(_missionsPanel, "Back", MenuTheme.Disabled, () => Show(Screen.Main), 56f);
        }

        private void BuildConfirmNewGamePanel()
        {
            _confirmNewGamePanel = MenuUI.CreatePanel(_canvas.transform, "ConfirmNewGamePanel", MenuTheme.ColumnWidth);

            MenuUI.CreateText(_confirmNewGamePanel, "Start a new game?", MenuTheme.HeadingFontSize, MenuTheme.Ink,
                TextAlignmentOptions.Center, FontStyles.Bold);

            MenuUI.CreateText(_confirmNewGamePanel,
                "This erases every mission you have finished and starts again from the beginning.",
                MenuTheme.SmallFontSize, MenuTheme.Ink);

            MenuUI.SetSpacer(_confirmNewGamePanel, 8f);

            MenuUI.CreateButton(_confirmNewGamePanel, "Yes, start over", MenuTheme.Danger, OnConfirmNewGame);
            MenuUI.CreateButton(_confirmNewGamePanel, "Cancel", MenuTheme.Disabled, () => Show(Screen.Main), 56f);
        }

        private void BuildProfilePanel()
        {
            _profilePanel = MenuUI.CreatePanel(_canvas.transform, "ProfilePanel", MenuTheme.ColumnWidth);

            MenuUI.CreateText(_profilePanel, "My Progress", MenuTheme.HeadingFontSize, MenuTheme.Ink,
                TextAlignmentOptions.Center, FontStyles.Bold);

            _profileBody = MenuUI.CreateText(_profilePanel, string.Empty, MenuTheme.BodyFontSize,
                MenuTheme.Ink, TextAlignmentOptions.Left);

            // Shown only while the player has no parent attached. Without this, a child
            // who skipped the code at sign-up had no way to link afterwards.
            GameObject group = new GameObject("LinkParent", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            group.transform.SetParent(_profilePanel, false);

            VerticalLayoutGroup groupLayout = group.GetComponent<VerticalLayoutGroup>();
            groupLayout.spacing = 12f;
            groupLayout.childControlWidth = true;
            groupLayout.childControlHeight = true;
            groupLayout.childForceExpandWidth = true;
            groupLayout.childForceExpandHeight = false;

            group.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _linkParentGroup = group.GetComponent<RectTransform>();

            _linkParentField = MenuUI.CreateField(_linkParentGroup, "Parent code");
            _linkParentField.characterLimit = 6;

            _linkParentMessage = MenuUI.CreateText(_linkParentGroup, string.Empty,
                MenuTheme.SmallFontSize, MenuTheme.ErrorText, TextAlignmentOptions.Center);

            _linkParentButton = MenuUI.CreateButton(_linkParentGroup, "Link to Parent",
                MenuTheme.Primary, OnLinkParent, 56f);

            MenuUI.CreateButton(_profilePanel, "Back", MenuTheme.Disabled, () => Show(Screen.Main), 56f);
        }

        private void OnLinkParent()
        {
            string code = _linkParentField.text.Trim().ToUpperInvariant();

            if (code.Length != 6)
            {
                _linkParentMessage.color = MenuTheme.ErrorText;
                _linkParentMessage.text = "A parent code is 6 characters.";

                return;
            }

            _linkParentButton.interactable = false;
            _linkParentMessage.text = string.Empty;

            ApiClient.Instance.LinkParent(code, result =>
            {
                _linkParentButton.interactable = true;

                if (!result.Success)
                {
                    _linkParentMessage.color = MenuTheme.ErrorText;
                    _linkParentMessage.text = result.ErrorMessage;

                    return;
                }

                _linkParentMessage.color = MenuTheme.SuccessText;
                _linkParentMessage.text = "Linked! Your parent can see your progress now.";

                _linkParentField.text = string.Empty;

                // Re-read the profile so IsLinkedToParent is current and the form hides.
                ApiClient.Instance.GetProfile(profileResult =>
                {
                    if (profileResult.Success)
                    {
                        _profile = profileResult.Data;

                        UpdateProfileText();
                    }
                });
            });
        }

        // ------------------------------------------------------------ navigation

        private void Show(Screen screen)
        {
            _authPanel.gameObject.SetActive(screen == Screen.Auth);
            _mainPanel.gameObject.SetActive(screen == Screen.Main);
            _missionsPanel.gameObject.SetActive(screen == Screen.Missions);
            _profilePanel.gameObject.SetActive(screen == Screen.Profile);
            _confirmNewGamePanel.gameObject.SetActive(screen == Screen.ConfirmNewGame);

            if (screen == Screen.Loading)
            {
                SetStatus("Loading your progress...");
            }
        }

        private void SetStatus(string message, bool isError = false)
        {
            if (_statusLine == null)
            {
                return;
            }

            _statusLine.text = message ?? string.Empty;
            _statusLine.color = isError ? MenuTheme.ErrorText : MenuTheme.Ink;
        }

        // ----------------------------------------------------------------- auth

        private void SetRegisterMode(bool register)
        {
            _registerMode = register;

            _registerOnlyGroup.gameObject.SetActive(register);

            _authTitle.text = register ? "Create your player" : "Welcome back!";
            _authMessage.text = string.Empty;

            SetButtonLabel(_authSubmit, register ? "Create Account" : "Sign In");
            SetButtonLabel(_authToggle, register ? "I already have an account" : "Create an account");
        }

        private void SubmitAuth()
        {
            string username = _usernameField.text.Trim();
            string password = _passwordField.text;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowAuthError("Please enter a username and password.");

                return;
            }

            SetAuthBusy(true);
            _authMessage.text = string.Empty;

            if (!_registerMode)
            {
                ApiClient.Instance.LoginPlayer(username, password, result =>
                {
                    SetAuthBusy(false);

                    if (!result.Success)
                    {
                        ShowAuthError(result.ErrorMessage);

                        return;
                    }

                    Show(Screen.Loading);
                    RefreshProfile();
                });

                return;
            }

            string displayName = _displayNameField.text.Trim();

            if (string.IsNullOrEmpty(displayName))
            {
                SetAuthBusy(false);
                ShowAuthError("Please tell us your name.");

                return;
            }

            int? age = int.TryParse(_ageField.text, out int parsedAge) ? parsedAge : (int?)null;

            ApiClient.Instance.RegisterPlayer(
                username,
                password,
                displayName,
                age,
                _linkCodeField.text.Trim(),
                result =>
                {
                    SetAuthBusy(false);

                    if (!result.Success)
                    {
                        ShowAuthError(result.ErrorMessage);

                        return;
                    }

                    Show(Screen.Loading);
                    RefreshProfile();
                });
        }

        private void ShowAuthError(string message)
        {
            _authMessage.color = MenuTheme.ErrorText;
            _authMessage.text = message;
        }

        private void SetAuthBusy(bool busy)
        {
            _authSubmit.interactable = !busy;
            _authToggle.interactable = !busy;

            SetStatus(busy ? "Talking to the server..." : string.Empty);
        }

        // -------------------------------------------------------------- profile

        private void RefreshProfile()
        {
            ApiClient.Instance.GetProfile(result =>
            {
                if (!result.Success)
                {
                    SetStatus(result.ErrorMessage, true);

                    // A failure here usually means the session is gone or the server is
                    // down; either way the player needs the sign-in screen back.
                    Show(Screen.Auth);

                    return;
                }

                _profile = result.Data;

                SetStatus(string.Empty);
                ApplyProfile();
                Show(Screen.Main);
            });
        }

        private void ApplyProfile()
        {
            if (_profile?.Child == null)
            {
                return;
            }

            _greeting.text = $"Hi, {_profile.Child.DisplayName}!";

            _progressLine.text =
                $"{_profile.MissionsCompleted} of {_profile.MissionsTotal} missions complete" +
                $"   •   {FormatPlaytime(_profile.PlaytimeSeconds)} played";

            // The backend unlocks all eight missions in order, so its continue target is
            // usually one that has no scene content yet. The button follows the mission
            // that actually exists instead.
            _startTarget = ResolveStartTarget();

            _continueButton.interactable = _startTarget != null;

            if (_startTarget == null)
            {
                _continueLabel.text = "All missions complete!";

                return;
            }

            _continueLabel.text = _startTarget.Resuming
                ? $"Continue: {_startTarget.Title}"
                : $"Start: {_startTarget.Title}";
        }

        /// <summary>
        /// Which mission the Continue button acts on. The server's own choice when it
        /// happens to be the built one, otherwise the built one described locally —
        /// the profile response carries no mission list to look it up in.
        /// </summary>
        private ContinueMission ResolveStartTarget()
        {
            ContinueMission fromServer = _profile.ContinueMission;

            if (fromServer != null && IsPlayable(fromServer.Code))
            {
                return fromServer;
            }

            return new ContinueMission
            {
                Code = PlayableMissionCode,
                Title = "The Road Home",
                Topic = "school zone safety",
                Resuming = false,
            };
        }

        private static bool IsPlayable(MissionSummary mission) =>
            mission != null && IsPlayable(mission.Code);

        private static bool IsPlayable(string code) => code == PlayableMissionCode;

        private void UpdateProfileText()
        {
            if (_profile == null)
            {
                _profileBody.text = "No progress yet.";

                return;
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();

            builder.AppendLine($"Player: {_profile.Child.DisplayName}");
            builder.AppendLine($"Missions completed: {_profile.MissionsCompleted} / {_profile.MissionsTotal}");
            builder.AppendLine($"Time played: {FormatPlaytime(_profile.PlaytimeSeconds)}");
            builder.AppendLine();
            builder.AppendLine("Skills");

            if (_profile.Skills != null)
            {
                foreach (string skill in SkillOrder)
                {
                    if (_profile.Skills.TryGetValue(skill, out int score))
                    {
                        builder.AppendLine($"  {PrettySkill(skill)}: {score} / 100");
                    }
                }
            }

            builder.AppendLine();
            builder.AppendLine(_profile.Child.IsLinkedToParent
                ? "Linked to a parent account."
                : "Not linked to a parent yet. Ask them for their 6-character code.");

            _profileBody.text = builder.ToString();

            _linkParentGroup.gameObject.SetActive(!_profile.Child.IsLinkedToParent);
        }

        // ------------------------------------------------------------ menu actions

        private void OnContinue()
        {
            if (_startTarget == null)
            {
                return;
            }

            StartMission(
                _startTarget.Code,
                _startTarget.Topic,
                _startTarget.Title);
        }

        private void OnNewGame()
        {
            // Erasing a child's progress is not something a stray tap should do.
            SetStatus(string.Empty);

            Show(Screen.ConfirmNewGame);
        }

        private void OnConfirmNewGame()
        {
            SetStatus("Starting a new game...");

            ApiClient.Instance.StartNewGame(result =>
            {
                if (!result.Success)
                {
                    SetStatus(result.ErrorMessage, true);

                    Show(Screen.Main);

                    return;
                }

                SetStatus(string.Empty);

                EnterOpenWorld();
            });
        }

        /// <summary>
        /// Drops the player into the city with no mission chosen. The session still
        /// begins here so roaming time reaches the parent dashboard; the mission attempt
        /// itself starts when MissionTrigger is accepted in world.
        /// </summary>
        private void EnterOpenWorld()
        {
            SetStatus("Loading the city...");

            GameProgressTracker.Instance.BeginSession(_ => GameFlow.EnterOpenWorld());
        }

        private void OnOpenMissions()
        {
            Show(Screen.Missions);
            SetStatus("Loading missions...");

            ApiClient.Instance.GetMissions(result =>
            {
                if (!result.Success || result.Data?.Missions == null)
                {
                    SetStatus(result.ErrorMessage, true);

                    return;
                }

                SetStatus(string.Empty);
                PopulateMissions(result.Data.Missions);
            });
        }

        private void PopulateMissions(List<MissionSummary> missions)
        {
            foreach (GameObject row in _missionRows)
            {
                Destroy(row);
            }

            _missionRows.Clear();

            foreach (MissionSummary mission in missions)
            {
                bool playable = IsPlayable(mission);

                string status = !playable
                    ? ComingSoonStatus
                    : mission.IsCompleted
                        ? $"Best {mission.BestScore}/{mission.MaxScore}"
                        : mission.IsUnlocked ? "Ready to play" : "Locked";

                string label = $"{mission.OrderIndex}. {mission.Title}   ({status})";

                Color color = !playable
                    ? MenuTheme.Disabled
                    : mission.IsCompleted
                        ? MenuTheme.Primary
                        : mission.IsUnlocked ? MenuTheme.Secondary : MenuTheme.Disabled;

                MissionSummary captured = mission;

                Button button = MenuUI.CreateButton(_missionList, label, color, () =>
                    StartMission(captured.Code, captured.Topic, captured.Title), 74f);

                // Completed missions stay replayable; locked ones and the ones that have
                // no scene content yet are blocked.
                button.interactable = playable && (mission.IsUnlocked || mission.IsCompleted);

                _missionRows.Add(button.gameObject);
            }
        }

        private void OnOpenProfile()
        {
            UpdateProfileText();
            Show(Screen.Profile);
        }

        private void StartMission(string code, string topic, string title)
        {
            SetStatus($"Loading {title}...");

            GameFlow.SelectMission(code, topic, title);

            // The play session starts here so time spent loading and playing is counted
            // as one continuous session on the dashboard.
            GameProgressTracker.Instance.BeginSession(_ =>
                GameProgressTracker.Instance.BeginMission(code, (started, error) =>
                {
                    if (!started)
                    {
                        SetStatus(error, true);

                        return;
                    }

                    GameFlow.LoadGameplay();
                }));
        }

        private void OnSignOut()
        {
            SetStatus("Signing out...");

            ApiClient.Instance.SignOut(() =>
            {
                _profile = null;

                _usernameField.text = string.Empty;
                _passwordField.text = string.Empty;

                SetStatus(string.Empty);
                SetRegisterMode(false);
                Show(Screen.Auth);
            });
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // --------------------------------------------------------------- helpers

        private static void SetButtonLabel(Button button, string label)
        {
            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();

            if (text != null)
            {
                text.text = label;
            }
        }

        private static string FormatPlaytime(int seconds)
        {
            if (seconds < 60)
            {
                return $"{seconds}s";
            }

            int minutes = seconds / 60;

            if (minutes < 60)
            {
                return $"{minutes} min";
            }

            return $"{minutes / 60}h {minutes % 60}m";
        }

        private static string PrettySkill(string key)
        {
            switch (key)
            {
                case "SAFETY": return "Safety Awareness";
                case "COMMUNICATION": return "Communication";
                case "EMPATHY": return "Empathy";
                case "CONFIDENCE": return "Confidence";
                default: return key;
            }
        }
    }
}
