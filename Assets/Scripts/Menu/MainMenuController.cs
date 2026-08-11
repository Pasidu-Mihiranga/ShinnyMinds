using System.Collections.Generic;
using ShinyMinds.Api;
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
        }

        private const string BackgroundResourcePath = "UI/menu_background";

        private Canvas _canvas;
        private RectTransform _authPanel;
        private RectTransform _mainPanel;
        private RectTransform _missionsPanel;
        private RectTransform _profilePanel;

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

        // Missions and profile
        private RectTransform _missionList;
        private TextMeshProUGUI _profileBody;
        private TextMeshProUGUI _statusLine;

        private PlayerProfile _profile;
        private readonly List<GameObject> _missionRows = new List<GameObject>();

        private void Start()
        {
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

        // ---------------------------------------------------------------- build

        private void BuildUi()
        {
            _canvas = MenuUI.CreateCanvas("MainMenuCanvas", transform);

            MenuUI.CreateBackground(_canvas.transform, LoadBackgroundSprite());

            BuildAuthPanel();
            BuildMainPanel();
            BuildMissionsPanel();
            BuildProfilePanel();

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

        private void BuildProfilePanel()
        {
            _profilePanel = MenuUI.CreatePanel(_canvas.transform, "ProfilePanel", MenuTheme.ColumnWidth);

            MenuUI.CreateText(_profilePanel, "My Progress", MenuTheme.HeadingFontSize, MenuTheme.Ink,
                TextAlignmentOptions.Center, FontStyles.Bold);

            _profileBody = MenuUI.CreateText(_profilePanel, string.Empty, MenuTheme.BodyFontSize,
                MenuTheme.Ink, TextAlignmentOptions.Left);

            MenuUI.CreateButton(_profilePanel, "Back", MenuTheme.Disabled, () => Show(Screen.Main), 56f);
        }

        // ------------------------------------------------------------ navigation

        private void Show(Screen screen)
        {
            _authPanel.gameObject.SetActive(screen == Screen.Auth);
            _mainPanel.gameObject.SetActive(screen == Screen.Main);
            _missionsPanel.gameObject.SetActive(screen == Screen.Missions);
            _profilePanel.gameObject.SetActive(screen == Screen.Profile);

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

            bool canContinue = _profile.ContinueMission != null;

            _continueButton.interactable = canContinue;

            if (!canContinue)
            {
                _continueLabel.text = "All missions complete!";

                return;
            }

            _continueLabel.text = _profile.ContinueMission.Resuming
                ? $"Continue: {_profile.ContinueMission.Title}"
                : $"Start: {_profile.ContinueMission.Title}";
        }

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
                foreach (KeyValuePair<string, int> entry in _profile.Skills)
                {
                    builder.AppendLine($"  {PrettySkill(entry.Key)}: {entry.Value} / 100");
                }
            }

            builder.AppendLine();
            builder.AppendLine(_profile.Child.IsLinkedToParent
                ? "Linked to a parent account."
                : "Not linked to a parent yet. Ask them for their 6-character code.");

            _profileBody.text = builder.ToString();
        }

        // ------------------------------------------------------------ menu actions

        private void OnContinue()
        {
            if (_profile?.ContinueMission == null)
            {
                return;
            }

            StartMission(
                _profile.ContinueMission.Code,
                _profile.ContinueMission.Topic,
                _profile.ContinueMission.Title);
        }

        private void OnNewGame()
        {
            SetStatus("Starting a new game...");

            ApiClient.Instance.StartNewGame(result =>
            {
                if (!result.Success)
                {
                    SetStatus(result.ErrorMessage, true);

                    return;
                }

                SetStatus(string.Empty);
                RefreshProfile();
            });
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
                string status = mission.IsCompleted
                    ? $"Best {mission.BestScore}/{mission.MaxScore}"
                    : mission.IsUnlocked ? "Ready to play" : "Locked";

                string label = $"{mission.OrderIndex}. {mission.Title}   ({status})";

                Color color = mission.IsCompleted
                    ? MenuTheme.Primary
                    : mission.IsUnlocked ? MenuTheme.Secondary : MenuTheme.Disabled;

                MissionSummary captured = mission;

                Button button = MenuUI.CreateButton(_missionList, label, color, () =>
                    StartMission(captured.Code, captured.Topic, captured.Title), 74f);

                // Completed missions stay replayable; only locked ones are blocked.
                button.interactable = mission.IsUnlocked || mission.IsCompleted;

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
