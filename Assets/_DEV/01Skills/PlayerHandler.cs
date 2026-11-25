using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerHandler : MonoBehaviour
{
    [SerializeField] GameObject NotEnoughEnergy;
    [SerializeField] GameObject FinalUI;
    [SerializeField] TMPro.TMP_Text FinalScore;
    [SerializeField] GameObject PauseScreen;
    [SerializeField] string GameName;

    [Header("Editor Testing")]
    [Tooltip("Used only when no --token= command-line argument is present (e.g. running in the Editor).")]
    [SerializeField] string editorDebugToken;
    [Tooltip("Used only when no --lobbyPath= command-line argument is present (e.g. running in the Editor). Path to the lobby .exe, or a URL, to return to.")]
    [SerializeField] string editorDebugLobbyPath;

    public static float CoinRate { get; private set; }

    private bool _gameActive;

    private void Awake()
    {
        AuthTokenStore.Instance.Token = LaunchArgs.Get("token", editorDebugToken);
        DataTransferHandler.lobbyPath = LaunchArgs.Get("lobbyPath", editorDebugLobbyPath);
        DataTransferHandler.gamesManifestPath = LaunchArgs.Get("gamesManifest", "");
        GameName = LaunchArgs.Get("config", GameName);
    }

    private void Start()
    {
        StartCoroutine(LoadConfig_Co());
    }

    private IEnumerator LoadConfig_Co()
    {
        yield return Services.Game.FetchConfigs(AuthTokenStore.Instance.Token, r =>
        {
            if (r.Success) PlayerDataStore.Instance.SetConfigs(r.Configs);
        });

        var config = PlayerDataStore.Instance.GetConfig(GameName);
        if (config != null) CoinRate = config.coins_rate;

        _gameActive = true;
    }

    private void Update()
    {
        if (!_gameActive) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (Time.timeScale == 0)
            {
                ResumeLevel();
            }
            else
            {
                PauseLevel();
            }
        }
    }

    // Call this when the match ends (e.g. a flag capture, a round timer running out) with the
    // raw score earned this run. Converts it to coins via the backend-configured rate, reports
    // it to the server, and shows the final-score screen.
    public void EndGame(int score)
    {
        StartCoroutine(Services.Game.EndGame(AuthTokenStore.Instance.Token, score, null));
        Time.timeScale = 0;
        FinalUI.SetActive(true);
        FinalScore.text = (score * CoinRate).ToString("000") + "<color=#3CEEFE> COINS</color>\r\n";
    }

    public void ReturnToLobby()
    {
        NotEnoughEnergy.SetActive(false);
        Time.timeScale = 1;

        // Relaunches the lobby with the token, robot + skin (quoted) and games manifest; SwitchTo
        // keeps this window up until the lobby has drawn, then quits (see LobbyReturn).
        if (LobbyReturn.TrySwitch())
            return;

        // Reached only when there was nothing to hand off to, or the handoff failed to
        // start. Quitting is still the right move: staying open would strand the user in
        // a game they asked to leave.
        AppHandoff.Quit();
    }
    public void RestartLevel()
    {
        StartCoroutine(RestartGame_Co());
    }

    private IEnumerator RestartGame_Co()
    {
        string token = AuthTokenStore.Instance.Token;
        yield return Services.Game.EndGame(token, 0, null);
        GameStartResult startResult = null;
        yield return Services.Game.StartGame(token, GameName, r => startResult = r);
        if (startResult != null && startResult.Success)
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        else
        {
            _gameActive = false;
            NotEnoughEnergy.SetActive(true);
        }
    }
    public void PauseLevel()
    {
        PauseScreen.SetActive(true);
        Time.timeScale = 0;
    }
    public void ResumeLevel()
    {
        PauseScreen.SetActive(false);
        Time.timeScale = 1;
    }
}
