namespace XboxLibrary.Models;

public class RefreshTokenResponse
{
	public string token_type;

	public int expires_in;

	public string scope;

	public string access_token;

	public string refresh_token;

	public string user_id;
}
