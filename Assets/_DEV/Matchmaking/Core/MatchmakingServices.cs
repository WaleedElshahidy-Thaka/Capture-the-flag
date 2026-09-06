// Composition root for the Matchmaking feature - named MatchmakingServices, not Services,
// because Future_DEV/Scripts/Core/Services.cs already claims that name and both compile into
// the same Assembly-CSharp (no .asmdef boundary in this project). Same one-field pattern:
// swap the `quickMatch` field to change backend, no other code changes.
public static class MatchmakingServices
{
    // Switched to the real Fusion backend to test two real instances (.exe + Editor). Swap
    // back to new QuickMatchLocalService() for solo/no-network UI iteration.
    static readonly IQuickMatchService quickMatch = new QuickMatchFusionService();
    public static readonly IQuickMatchService QuickMatch = quickMatch;
}
