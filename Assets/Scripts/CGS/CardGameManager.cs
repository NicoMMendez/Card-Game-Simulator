/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Events;

using CardGameDef;
using CardGameView;
using CGS.Menus;
using CGS.Play;
using CGS.Play.Multiplayer;

namespace CGS
{
    public class CardGameManager : MonoBehaviour
    {
        public const bool IsMessengerDebugLogVerbose = false;
        public const string GameId = "GameId";
        public const string PlayerPrefDefaultGame = "DefaultGame";
        public const string SelectorPrefabName = "Game Selection Menu";
        public const string MessengerPrefabName = "Popup";
        public const string SpinnerPrefabName = "Spinner";
        public const string GameSelectionErrorPrompt = "Could not select the card game because it is not recognized! Try selecting a different card game?";
        public const string BranchCallbackErrorMessage = "Branch Callback Error!: ";
        public const string GameDownLoadErrorMessage = "Error downloading game!: ";
        public const string GameLoadErrorMessage = "Error loading game!: ";
        public const string GameLoadErrorPrompt = "Error loading game! The game may be corrupted. Delete (note that any decks would also be deleted)?";
        public const string GameDeleteErrorMessage = "Error deleting game!: ";
        public const string CardsLoadedMessage = "{0} cards loaded!";
        public const string CardsLoadingMessage = "{0} cards loading...";
        public const int CardsLoadingMessageThreshold = 60;
        public const int PixelsPerInch = 100;

        public static CardGameManager Instance
        {
            get
            {
                if (IsQuitting) return null;
                if (_instance != null) return _instance;
                GameObject cardGameManager = GameObject.FindGameObjectWithTag(Tags.CardGameManager);
                if (cardGameManager == null)
                {
                    cardGameManager = new GameObject(Tags.CardGameManager) { tag = Tags.CardGameManager };
                    cardGameManager.transform.position = Vector3.zero;
                }
                _instance = cardGameManager.GetOrAddComponent<CardGameManager>();
                return _instance;
            }
        }
        private static CardGameManager _instance;

        public static CardGame Current { get; private set; } = CardGame.Invalid;
        public static bool IsQuitting { get; private set; } = false;

        public SortedDictionary<string, CardGame> AllCardGames { get; } = new SortedDictionary<string, CardGame>();
        public List<UnityAction> OnSceneActions { get; } = new List<UnityAction>();

        public SortedList<string, string> GamesListing => new SortedList<string, string>(AllCardGames.ToDictionary(game => game.Key, game => game.Value.Name));

        public LobbyDiscovery Discovery => _discovery ?? (_discovery = gameObject.GetOrAddComponent<LobbyDiscovery>());
        private LobbyDiscovery _discovery;

        public GameSelectionMenu Selector
        {
            get
            {
                if (_selector != null) return _selector;
                _selector = Instantiate(Resources.Load<GameObject>(SelectorPrefabName)).GetOrAddComponent<GameSelectionMenu>();
                _selector.transform.SetParent(null);
                return _selector;
            }
        }
        private GameSelectionMenu _selector;

        public Popup Messenger
        {
            get
            {
                if (_messenger != null) return _messenger;
                _messenger = Instantiate(Resources.Load<GameObject>(MessengerPrefabName)).GetOrAddComponent<Popup>();
                _messenger.transform.SetParent(transform);
                return _messenger;
            }
        }
        private Popup _messenger;

        public SpinningLoadingPanel Spinner
        {
            get
            {
                if (_spinner != null) return _spinner;
                _spinner = Instantiate(Resources.Load<GameObject>(SpinnerPrefabName)).GetOrAddComponent<SpinningLoadingPanel>();
                _spinner.transform.SetParent(transform);
                return _spinner;
            }
        }
        private SpinningLoadingPanel _spinner;

        // TODO: IMPROVE PERFORMANCE; MAYBE TRACK THIS USING OBSERVER PATTERN?
        public Canvas TopCardCanvas
        {
            get
            {
                Canvas topCanvas = null;
                foreach (GameObject canvas in GameObject.FindGameObjectsWithTag(Tags.CardCanvas))
                    if (canvas.activeSelf && (topCanvas == null || canvas.GetComponent<Canvas>().sortingOrder > topCanvas.sortingOrder))
                        topCanvas = canvas.GetComponent<Canvas>();
                return topCanvas;
            }
        }

        // TODO: IMPROVE PERFORMANCE; MAYBE TRACK THIS USING OBSERVER PATTERN?
        public Canvas TopMenuCanvas
        {
            get
            {
                Canvas topCanvas = null;
                foreach (GameObject canvas in GameObject.FindGameObjectsWithTag(Tags.MenuCanvas))
                    if (canvas.activeSelf && (topCanvas == null || canvas.GetComponent<Canvas>().sortingOrder > topCanvas.sortingOrder))
                        topCanvas = canvas.GetComponent<Canvas>();
                return topCanvas;
            }
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            CardGame.Invalid.CoroutineRunner = this;
            DontDestroyOnLoad(gameObject);

            if (!Directory.Exists(CardGame.GamesDirectoryPath))
                CreateDefaultCardGames();
            LookupCardGames();

            if (Debug.isDebugBuild)
                Application.logMessageReceived += ShowLogToUser;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            ResetToPreferredCardGame();
        }

        private void CreateDefaultCardGames()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            UnityExtensionMethods.ExtractAndroidStreamingAssets(CardGame.GamesDirectoryPath);
#else
            UnityExtensionMethods.CopyDirectory(Application.streamingAssetsPath, CardGame.GamesDirectoryPath);
#endif
        }

        private void LookupCardGames()
        {
            if (!Directory.Exists(CardGame.GamesDirectoryPath) || Directory.GetDirectories(CardGame.GamesDirectoryPath).Length < 1)
                CreateDefaultCardGames();

            foreach (string gameDirectory in Directory.GetDirectories(CardGame.GamesDirectoryPath))
            {
                string gameDirectoryName = gameDirectory.Substring(CardGame.GamesDirectoryPath.Length + 1);
                // TODO: C#7 Tuple
                var tuple = CardGame.Decode(gameDirectoryName);
                string gameName = tuple.Item1;
                string gameUrl = tuple.Item2;
                CardGame newCardGame = new CardGame(this, gameName, gameUrl);
                newCardGame.ReadProperties();
                AllCardGames[newCardGame.Id] = newCardGame;
            }
        }

        void ShowLogToUser(string logString, string stackTrace, LogType type)
        {
            if (IsMessengerDebugLogVerbose || !LogType.Log.Equals(type))
                Messenger.Show(logString);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SetupGameScene();
        }

        void OnSceneUnloaded(Scene scene)
        {
            OnSceneActions.Clear();
        }

        public void BranchCallbackWithParams(Dictionary<string, object> parameters, string error)
        {
            if (error != null)
            {
                Debug.LogError(BranchCallbackErrorMessage + error);
                Messenger.Show(BranchCallbackErrorMessage + error);
                return;
            }

            object gameId;
            if (parameters.TryGetValue(GameId, out gameId))
                SelectCardGame((string)gameId);
        }

        public void ResetToPreferredCardGame()
        {
            CardGame currentGame;
            Current = AllCardGames.TryGetValue(PlayerPrefs.GetString(PlayerPrefDefaultGame), out currentGame) && string.IsNullOrEmpty(currentGame.Error)
                 ? currentGame : (AllCardGames.FirstOrDefault().Value ?? CardGame.Invalid);
        }

        public IEnumerator DownloadCardGame(string gameUrl)
        {
            Spinner.Show();

            CardGame newGame = new CardGame(this, CardGame.DefaultName, gameUrl);
            yield return newGame.Download();
            newGame.Load(UpdateCardGame, LoadCards);

            if (string.IsNullOrEmpty(newGame.Error))
            {
                AllCardGames[newGame.Id] = newGame;
                SelectCardGame(newGame.Id);
            }
            else
            {
                Debug.LogError(GameDownLoadErrorMessage + Current.Error);
                Messenger.Show(GameDownLoadErrorMessage + Current.Error);
            }

            Spinner.Hide();
        }

        public IEnumerator UpdateCardGame(CardGame cardGame)
        {
            if (cardGame == null)
                cardGame = Current;

            Spinner.Show();
            yield return cardGame.Download();
            Spinner.Hide();

            // Notify about the failed update, but otherwise ignore errors
            if (!string.IsNullOrEmpty(cardGame.Error))
            {
                Debug.LogError(GameDownLoadErrorMessage + cardGame.Error);
                Messenger.Show(GameDownLoadErrorMessage + cardGame.Error);
                cardGame.ClearError();
            }

            cardGame.Load(UpdateCardGame, LoadCards);
            if (cardGame == Current)
                SetupGameScene();
        }

        public IEnumerator LoadCards(CardGame cardGame)
        {
            if (cardGame == null)
                cardGame = Current;

            for (int page = cardGame.AllCardsUrlPageCountStartIndex; page < cardGame.AllCardsUrlPageCountStartIndex + cardGame.AllCardsUrlPageCount; page++)
            {
                cardGame.LoadCards(page);
                if (page == cardGame.AllCardsUrlPageCountStartIndex && cardGame.AllCardsUrlPageCount > CardsLoadingMessageThreshold)
                    Messenger.Show(string.Format(CardsLoadingMessage, cardGame.Name));
                yield return null;
            }

            if (!string.IsNullOrEmpty(cardGame.Error))
                Debug.LogError(GameLoadErrorMessage + cardGame.Error);
            else if (cardGame.AllCardsUrlPageCount > CardsLoadingMessageThreshold)
                Messenger.Show(string.Format(CardsLoadedMessage, cardGame.Name));
        }

        public void SelectLeft()
        {
            string prevGameId = AllCardGames.Keys.Last();
            SortedDictionary<string, CardGame>.Enumerator allCardGamesEnum = AllCardGames.GetEnumerator();
            bool found = false;
            while (!found && allCardGamesEnum.MoveNext())
            {
                if (!allCardGamesEnum.Current.Key.Equals(Current.Id))
                    prevGameId = allCardGamesEnum.Current.Key;
                else
                    found = true;
            }
            SelectCardGame(prevGameId);
        }

        public void SelectRight()
        {
            string nextGameId = Current.Id;
            SortedDictionary<string, CardGame>.Enumerator allCardGamesEnum = AllCardGames.GetEnumerator();
            bool found = false;
            while (!found && allCardGamesEnum.MoveNext())
                if (allCardGamesEnum.Current.Key.Equals(Current.Id))
                    found = true;
            if (allCardGamesEnum.MoveNext())
                nextGameId = allCardGamesEnum.Current.Key;
            else if (found)
                nextGameId = AllCardGames.Keys.First();
            SelectCardGame(nextGameId);
        }

        public void SelectCardGame(string gameId)
        {
            if (string.IsNullOrEmpty(gameId) || !AllCardGames.ContainsKey(gameId))
            {
                // TODO: C#7 Tuple
                string gameUrl = CardGame.Decode(gameId).Item2;
                if (!Uri.IsWellFormedUriString(gameUrl, UriKind.Absolute))
                {
                    Debug.LogError(GameSelectionErrorPrompt);
                    Messenger.Show(GameSelectionErrorPrompt);
                    Selector.Show();
                }
                else
                    StartCoroutine(DownloadCardGame(gameUrl));
                return;
            }

            Current = AllCardGames[gameId];
            SetupGameScene();
        }

        public void SetupGameScene()
        {
            if (!Current.HasLoaded)
            {
                Current.Load(UpdateCardGame, LoadCards);
                if (Current.IsDownloading)
                    return;
            }

            if (!string.IsNullOrEmpty(Current.Error))
            {
                Debug.LogError(GameLoadErrorMessage + Current.Error);
                Messenger.Ask(GameLoadErrorPrompt, IgnoreErroredGame, DeleteGame);
                return;
            }

            // Now is the safest time to set this game as the preferred default game for the player
            PlayerPrefs.SetString(PlayerPrefDefaultGame, Current.Id);

            // We treat the CardInfoViewer specially for now...
            CardInfoViewer.Instance?.ResetInfo();
            // Otherwise, each scene element is responsible for registering with OnSceneActions
            for (int i = OnSceneActions.Count - 1; i >= 0; i--)
                if (OnSceneActions[i] == null)
                    OnSceneActions.RemoveAt(i);
            foreach (UnityAction action in OnSceneActions)
                action();
        }

        public void IgnoreErroredGame()
        {
            Current.ClearError();
            ResetToPreferredCardGame();
            SetupGameScene();
            Selector.Show();
        }

        public void DeleteGame()
        {
            try
            {
                Directory.Delete(Current.GameDirectoryPath, true);
                AllCardGames.Remove(Current.Id);
                ResetToPreferredCardGame();
                SetupGameScene();
                Selector.Show();
            }
            catch (Exception ex)
            {
                Debug.LogError(GameDeleteErrorMessage + ex.Message);
            }
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnApplicationQuit()
        {
            IsQuitting = true;
        }
    }
}
