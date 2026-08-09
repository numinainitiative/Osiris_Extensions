using System;

namespace XboxLibrary.Models;

public class AuthenticationData
{
	public string AccessToken;

	public string RefreshToken;

	public int ExpiresIn;

	public DateTime CreationDate;

	public string UserId;

	public string TokenType;
}
