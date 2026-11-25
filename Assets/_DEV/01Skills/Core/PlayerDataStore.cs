// Single source of truth for the current player — profile data (from login) + progress data (from /users/progress).
// Also holds game configs (global, not cleared on logout).
using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDataStore
{
    private static PlayerDataStore _instance;
    public static PlayerDataStore Instance => _instance ??= new PlayerDataStore();

    private UserProfile    _profile;
    private PlayerProgress _progress;
    private readonly List<GameConfig> _configs = new List<GameConfig>();

    // Fires every time SetProgress is called — subscribe to reactively update any UI that displays progress data.
    public static event Action OnProgressChanged;
    // Fires with the new premium state whenever progress is set or cleared.
    public static event Action<bool> OnPremiumStatusChanged;

    public bool HasProfile   => _profile  != null;
    public bool HasProgress  => _progress != null;
    public bool ConfigsLoaded { get; private set; }

    public void SetProfile(UserProfile profile) => _profile = profile;

    public void SetProgress(PlayerProgress progress)
    {
        _progress = progress;
        OnProgressChanged?.Invoke();
        OnPremiumStatusChanged?.Invoke(IsPremium);
        Debug.Log($"PlayerDataStore: SetProgress called. Coins: {Coins}, Chips: {Chips}, Energy: {Energy}/{MaxEnergy}, Level: {Level}, Experience: {Experience}/{MaxExperience}, UserItems: {string.Join(", ", UserItems)}");
    }

    // configs intentionally kept — they are not user-specific
    public void Clear()
    {
        _profile  = null;
        _progress = null;
        OnPremiumStatusChanged?.Invoke(false);
    }

    // ── Game Configs ─────────────────────────────────────────────────────────
    public void SetConfigs(List<GameConfig> configs)
    {
        _configs.Clear();
        _configs.AddRange(configs);
        ConfigsLoaded = true;
    }

    public GameConfig GetConfig(string name) => _configs.Find(c => c.name == name);

    // ── Profile ──────────────────────────────────────────────────────────────
    public string UserId    => _profile?.UserId    ?? "";
    public string FirstName => _profile?.FirstName ?? "";
    public string LastName  => _profile?.LastName  ?? "";
    public string FullName  => _profile?.FullName  ?? "";
    public string Email     => _profile?.Email     ?? "";
    public string Avatar    => _profile?.Avatar    ?? "";
    public string School    => _profile?.School    ?? "";
    public string TeamId    => _profile?.TeamId    ?? "";
    public string TeamName  => _profile?.TeamName  ?? "";
    public string TeamLogo  => _profile?.TeamLogo  ?? "";

    // ── Progress ─────────────────────────────────────────────────────────────
    public int Coins         => _progress?.coins         ?? 0;
    public int Chips         => _progress?.chips         ?? 0;
    public int Energy        => _progress?.energy        ?? 0;
    public int MaxEnergy     => _progress?.maxEnergy     ?? 0;
    public int RegenRate     => _progress?.regenRate     ?? 0;
    public int Level         => _progress?.level         ?? 0;
    public int Experience    => _progress?.experience    ?? 0;
    public int MaxExperience => _progress?.maxExperience ?? 0;

    public List<string>   UserItems => _progress?.userItems ?? new List<string>();
    public bool           IsPremium => UserItems.Contains("PremiumPackage");

    public UserProfile    Profile  => _profile;
    public PlayerProgress Progress => _progress;

    // ── Mini updates (local only — call the matching server endpoint separately) ─
    // Each method mutates the in-memory progress and fires OnProgressChanged so
    // any subscribed UI (e.g. PlayerUI_Data) updates automatically.

    public void AddEnergy(int amount)
    {
        if (_progress == null) return;
        _progress.energy = Math.Min(_progress.energy + amount, _progress.maxEnergy);
        OnProgressChanged?.Invoke();
    }

    public void ConsumeEnergy(int amount)
    {
        if (_progress == null) return;
        _progress.energy = Math.Max(_progress.energy - amount, 0);
        OnProgressChanged?.Invoke();
    }

    public void AddCoins(int amount)
    {
        if (_progress == null) return;
        _progress.coins += amount;
        OnProgressChanged?.Invoke();
    }

    public void AddChips(int amount)
    {
        if (_progress == null) return;
        _progress.chips += amount;
        OnProgressChanged?.Invoke();
    }

    public void AddUserItem(string itemId)
    {
        if (_progress == null) return;
        if (_progress.userItems == null) _progress.userItems = new List<string>();
        _progress.userItems.Add(itemId);
        OnProgressChanged?.Invoke();
    }

    // Zeroes out all progress for guest/offline mode.
    public void SetGuestProgress() => SetProgress(new PlayerProgress());
}
