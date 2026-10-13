// Result of IQuickMatchService.StartQuickMatch (Ok/Fail convention, as in Future_DEV's GameModels).
public class StartQuickMatchResult
{
    public bool Success { get; private set; }
    public IActiveQuickMatchSession Session { get; private set; }
    public string Error { get; private set; }

    public static StartQuickMatchResult Ok(IActiveQuickMatchSession session) => new StartQuickMatchResult { Success = true, Session = session };
    public static StartQuickMatchResult Fail(string error) => new StartQuickMatchResult { Success = false, Error = error };
}
