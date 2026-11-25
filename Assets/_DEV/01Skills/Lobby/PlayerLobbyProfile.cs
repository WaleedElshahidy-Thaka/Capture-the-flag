using Fusion;
using UnityEngine;

// The lobby choices of the player driving this car - robot, skin and level - replicated so every
// peer shows each car with its owner's robot (CarRobotAppearance) and level (LobbyUI seat slots).
// The name is not here: it already syncs through PlayerMatchState.DisplayName.
//
//   Player cars: the input-authority client sends its values to the host once it owns the car
//                (polled like PlayerMatchState's ownership, so host-migration hand-backs resend),
//                and resends the level whenever PlayerDataStore progress changes (after /games/end).
//   AI bots:     the host gives them a random robot before spawning (BotFill -> AssignRandomBot).
//
// Cosmetic data only: nothing in gameplay reads it. Put it on the PlayerCar root.
public class PlayerLobbyProfile : NetworkBehaviour
{
    [Tooltip("Robots AI bots can be given (the shared lobby catalogue).")]
    [SerializeField] BotSkinLibrary skinLibrary;

    [Networked] public NetworkString<_32> BotName { get; set; }
    [Networked] public NetworkString<_32> SkinName { get; set; }
    [Networked] public int Level { get; set; }

    bool sentAsOwner;

    // Local player's level: server progress once known, else --level.
    public static int LocalLevel =>
        PlayerDataStore.Instance.HasProgress ? PlayerDataStore.Instance.Level : DataTransferHandler.playerLevel;

    // Level shown on every UI: bots and unknown (0) levels show as 1.
    public static int DisplayLevel(int level) => Mathf.Max(1, level);
    public static string LevelLabel(int level) => "LV. " + DisplayLevel(level);

    // A car's synced level, 0 if it has no profile (yet).
    public static int LevelOf(Component car) =>
        car != null && car.TryGetComponent(out PlayerLobbyProfile p) && p.Object != null && p.Object.IsValid ? p.Level : 0;

    void OnEnable() => PlayerDataStore.OnProgressChanged += OnProgressChanged;
    void OnDisable() => PlayerDataStore.OnProgressChanged -= OnProgressChanged;

    public override void Spawned() => sentAsOwner = false;

    public override void Render()
    {
        bool mine = Object.HasInputAuthority;
        if (mine && !sentAsOwner)
            RPC_SetProfile(DataTransferHandler.selectedBotName ?? "", DataTransferHandler.selectedSkinName ?? "", LocalLevel);
        sentAsOwner = mine;
    }

    void OnProgressChanged()
    {
        if (Object != null && Object.IsValid && Object.HasInputAuthority) RPC_SetLevel(LocalLevel);
    }

    // Host only, before the bot is spawned (networked state is writable in onBeforeSpawned).
    public void AssignRandomBot()
    {
        if (skinLibrary == null || skinLibrary.bots.Count == 0) return;
        var set = skinLibrary.bots[Random.Range(0, skinLibrary.bots.Count)];
        if (set == null) return;
        BotName = set.botName;
        SkinName = "";       // default skin = the material as authored
        Level = 0;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    void RPC_SetProfile(string botName, string skinName, int level)
    {
        BotName = botName;
        SkinName = skinName;
        Level = Mathf.Max(0, level);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    void RPC_SetLevel(int level) => Level = Mathf.Max(0, level);
}
