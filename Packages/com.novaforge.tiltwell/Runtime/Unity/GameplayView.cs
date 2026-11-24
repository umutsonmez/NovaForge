using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NovaForge.Core.Ads;
using NovaForge.Core.Economy;
using NovaForge.Core.Foundation;
using NovaForge.Core.Localization;
using NovaForge.Core.UI;
using NovaForge.UI;
using NovaForge.Unity.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if TILTWELL_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NovaForge.Tiltwell.Unity
{
    public static class TiltwellIds
    {
        public const string Hint = "hint";
        public const string HintPlacement = "hint";
        public const string ExtraMovesPlacement = AdPlacements.ExtraMoves;
        public const int ExtraMoves = 5;
    }

    /// <summary>The puzzle screen (view id "gameplay"). Args: level id.</summary>
    public sealed class GameplayView : UIView
    {
        [SerializeField] private BoardView board;
        [SerializeField] private SwipeInput swipe;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text movesLabel;
        [SerializeField] private TMP_Text messageLabel;
        [Tooltip("Optional background shown/hidden together with the message.")]
        [SerializeField] private GameObject messageBubble;
        [SerializeField] private TMP_Text hintCountLabel;
        [SerializeField] private Button homeButton;
        [SerializeField] private Button undoButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button hintButton;
        [SerializeField] private RectTransform hintArrow;

        private PuzzleSession _session;
        private Tilt? _queued;
        private bool _finished;
        private IDisposable _balanceSub;

        public PuzzleSession Session => _session;
        public bool IsAnimating => board != null && board.IsAnimating;

        protected override void Awake()
        {
            base.Awake();
            swipe.Swiped += t => Input(t);
            homeButton.onClick.AddListener(GoHome);
            undoButton.onClick.AddListener(OnUndo);
            restartButton.onClick.AddListener(OnRestart);
            hintButton.onClick.AddListener(OnHint);
        }

        protected override void OnShowing(object args)
        {
            var app = TiltwellApp.Instance;
            var levelId = args as string ?? app.NextPlayableLevelId();
            var level = app.Levels.Find(l => l.Id == levelId) ?? throw new KeyNotFoundException(levelId);
            Core.LevelSession.Quit(); // ends any attempt that was left open (app returns here after Next/Retry)
            Core.LevelSession.Start(levelId);
            _session = new PuzzleSession(level);
            _finished = false;
            _queued = null;
            Canvas.ForceUpdateCanvases(); // board rect must have its final size before building
            board.Build(_session.Board, _session.State);
            int index = Core.Progression.Catalog.IndexOf(levelId);
            levelLabel.text = Core.Localization.Format(TiltwellStrings.LevelN, index + 1);
            ShowMessage(index == 0 ? TiltwellStrings.Tutorial
                : level.Tier == 2 && IsFirstOfTier(app, level) ? TiltwellStrings.TutorialMix
                : IsFirstBrownLevel(app, level) ? TiltwellStrings.TutorialBrown
                : null);
            _balanceSub = Core.Events.Subscribe<BalanceChanged>(_ => RefreshHud());
            RefreshHud();
        }

        protected override void OnHiding()
        {
            _balanceSub?.Dispose();
            _balanceSub = null;
        }

        private static bool IsFirstOfTier(TiltwellApp app, LevelData level) =>
            app.Levels.Find(l => l.Tier == level.Tier) == level;

        private static bool HasBrownWell(LevelData level)
        {
            foreach (var row in level.Rows) if (row.IndexOf('k') >= 0) return true;
            return false;
        }

        private static bool IsFirstBrownLevel(TiltwellApp app, LevelData level) =>
            HasBrownWell(level) && app.Levels.Find(HasBrownWell) == level;

        public override bool HandleBack()
        {
            GoHome();
            return true;
        }

        private void Update()
        {
            if (!IsVisible) return;
#if TILTWELL_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return;
            if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) Input(Tilt.Up);
            else if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) Input(Tilt.Down);
            else if (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame) Input(Tilt.Left);
            else if (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame) Input(Tilt.Right);
            else if (k.zKey.wasPressedThisFrame) OnUndo();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow) || UnityEngine.Input.GetKeyDown(KeyCode.W)) Input(Tilt.Up);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow) || UnityEngine.Input.GetKeyDown(KeyCode.S)) Input(Tilt.Down);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow) || UnityEngine.Input.GetKeyDown(KeyCode.A)) Input(Tilt.Left);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow) || UnityEngine.Input.GetKeyDown(KeyCode.D)) Input(Tilt.Right);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Z)) OnUndo();
#endif
        }

        /// <summary>Applies a tilt (public so tests and a future replay system can drive the game).</summary>
        public void Input(Tilt tilt)
        {
            if (_finished || !IsVisible) return;
            if (board.IsAnimating) { _queued = tilt; return; } // keep one so fast swipes aren't lost
            Run(tilt).Forget();
        }

        private async Task Run(Tilt tilt)
        {
            var result = _session.Tilt(tilt);
            if (result == null) return;
            HideHintArrow();
            ShowMessage(null);
            Haptics.Play(HapticStrength.Light);
            RefreshHud();
            await board.Animate(result);
            if (this == null) return;
            foreach (var s in result.Steps)
                if (s.Fate == DropletFate.Delivered && AudioService.Instance != null) AudioService.Instance.PlaySfx(SoundIds.RewardCollect);
            await AfterMove();
            if (this != null && _queued.HasValue && !_finished)
            {
                var next = _queued.Value;
                _queued = null;
                await Run(next);
            }
        }

        private async Task AfterMove()
        {
            switch (_session.Status)
            {
                case PuzzleStatus.Solved:
                    _finished = true;
                    _queued = null;
                    long score = Math.Max(0, (_session.MoveLimit - _session.Moves) * 100L);
                    var ended = Core.LevelSession.Win(_session.Stars, score);
                    await Task.Delay(350);
                    if (this != null) await Navigation.ShowPopupAsync(ViewIds.LevelResult, ended, dismissible: false);
                    break;
                case PuzzleStatus.OutOfMoves:
                    _queued = null;
                    await OfferExtraMovesOrLose();
                    break;
                case PuzzleStatus.Stuck:
                    _queued = null;
                    ShowMessage(TiltwellStrings.Stuck);
                    break;
            }
            RefreshHud();
        }

        private async Task OfferExtraMovesOrLose()
        {
            var ads = Core.Ads;
            if (ads != null && ads.IsRewardedAvailable(TiltwellIds.ExtraMovesPlacement))
            {
                bool watch = await UIManager.Instance.ConfirmAsync(new ConfirmRequest
                {
                    TitleKey = TiltwellStrings.OutOfMovesTitle,
                    MessageKey = TiltwellStrings.ExtraMovesOffer,
                    MessageArgs = new object[] { TiltwellIds.ExtraMoves },
                    ConfirmKey = CoreStrings.Yes,
                    CancelKey = CoreStrings.No,
                });
                if (this == null) return;
                if (watch)
                {
                    var outcome = await ads.ShowRewardedAsync(TiltwellIds.ExtraMovesPlacement, _ => _session.AddMoves(TiltwellIds.ExtraMoves));
                    if (this == null) return;
                    if (outcome == RewardedAdOutcome.RewardGranted) { RefreshHud(); return; }
                }
            }
            _finished = true;
            var ended = Core.LevelSession.Lose("out_of_moves");
            await Navigation.ShowPopupAsync(ViewIds.LevelResult, ended, dismissible: false);
        }

        private void OnUndo()
        {
            if (_finished || board.IsAnimating || !_session.Undo()) return;
            board.Sync(_session.State);
            HideHintArrow();
            ShowMessage(null);
            RefreshHud();
        }

        private void OnRestart()
        {
            if (_finished || board.IsAnimating || _session.Moves == 0) return;
            Core.LevelSession.Restart();
            _session.Restart();
            board.Sync(_session.State);
            HideHintArrow();
            ShowMessage(null);
            RefreshHud();
        }

        private async void OnHint()
        {
            if (_finished || board.IsAnimating) return;
            var next = _session.Hint();
            if (!next.HasValue)
            {
                ShowMessage(TiltwellStrings.Stuck);
                return;
            }
            if (Core.Economy.GetBalance(TiltwellIds.Hint) > 0)
            {
                var tx = new EconomyTransaction("hint:" + Guid.NewGuid().ToString("N"), "hint_use").Debit(TiltwellIds.Hint, 1);
                if (Core.Economy.Apply(tx) == TransactionStatus.Success) ShowHintArrow(next.Value);
                return;
            }
            var ads = Core.Ads;
            if (ads != null && ads.IsRewardedAvailable(TiltwellIds.HintPlacement))
            {
                var outcome = await ads.ShowRewardedAsync(TiltwellIds.HintPlacement, _ => { });
                if (this != null && outcome == RewardedAdOutcome.RewardGranted) ShowHintArrow(next.Value);
                return;
            }
            ShowMessage(TiltwellStrings.NoHints);
        }

        private void ShowHintArrow(Tilt t)
        {
            if (hintArrow == null) return;
            hintArrow.gameObject.SetActive(true);
            hintArrow.localRotation = Quaternion.Euler(0, 0, t switch { Tilt.Up => 90f, Tilt.Left => 180f, Tilt.Down => 270f, _ => 0f });
        }

        private void HideHintArrow()
        {
            if (hintArrow != null) hintArrow.gameObject.SetActive(false);
        }

        private void ShowMessage(string key)
        {
            if (messageLabel == null) return;
            (messageBubble != null ? messageBubble : messageLabel.gameObject).SetActive(key != null);
            if (key != null) messageLabel.text = Core.Localization.Get(key);
        }

        private void RefreshHud()
        {
            if (_session == null) return;
            movesLabel.text = Core.Localization.Format(TiltwellStrings.Moves, _session.Moves, _session.MoveLimit);
            if (hintCountLabel != null) hintCountLabel.text = Core.Economy.GetBalance(TiltwellIds.Hint).ToString();
            undoButton.interactable = !_finished && _session.CanUndo;
            restartButton.interactable = !_finished && _session.Moves > 0;
        }

        private void GoHome()
        {
            if (board.IsAnimating) return;
            Core.LevelSession.Quit();
            Navigation.ResetToAsync(ViewIds.MainMenu).Forget();
        }
    }
}
