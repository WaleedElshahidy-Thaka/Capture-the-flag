using UnityEngine;

public interface IAuthTokenStore
{
    string Token { get; set; }
    bool HasToken { get; }
    void Clear();
}

public class AuthTokenStore : IAuthTokenStore
{
    private static AuthTokenStore _instance;
    public static AuthTokenStore Instance => _instance ??= new AuthTokenStore();

    private string _token = "";

    public string Token
    {
        get => _token;
        set
        {
            _token = value ?? "";
            Debug.Log($"[AuthTokenStore] Token updated, length={_token.Length}");
        }
    }

    public bool HasToken => !string.IsNullOrEmpty(_token);

    public void Clear()
    {
        _token = "";
        Debug.Log("[AuthTokenStore] Token cleared.");
    }
}
