public class UserProfile
{
    public string UserId    { get; private set; }
    public string FirstName { get; private set; }
    public string LastName  { get; private set; }
    public string Email     { get; private set; }
    public string Avatar    { get; private set; }
    public string School    { get; private set; }
    public string TeamId    { get; private set; }
    public string TeamName  { get; private set; }
    public string TeamLogo  { get; private set; }

    public string FullName => (FirstName + " " + LastName).Trim();

    public static UserProfile Create(
        string userId, string firstName, string lastName,
        string email, string avatar, string school,
        string teamId, string teamName, string teamLogo) =>
        new UserProfile
        {
            UserId = userId, FirstName = firstName, LastName = lastName,
            Email = email, Avatar = avatar, School = school,
            TeamId = teamId, TeamName = teamName, TeamLogo = teamLogo
        };

    public static UserProfile Admin() => new UserProfile { UserId = "admin", FirstName = "Admin" };
    public static UserProfile Guest() => new UserProfile { UserId = "guest", FirstName = "Guest" };
}
