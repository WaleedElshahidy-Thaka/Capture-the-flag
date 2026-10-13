// Composition root for matchmaking: the one place the backend is chosen.
// Use `new QuickMatchLocalService()` for offline UI work, `new QuickMatchFusionService()` for real play.
// (Not named "Services" - Future_DEV/Scripts/Core/Services.cs already uses that name.)
public static class MatchmakingServices
{
    public static readonly IQuickMatchService QuickMatch = new QuickMatchFusionService();
}
