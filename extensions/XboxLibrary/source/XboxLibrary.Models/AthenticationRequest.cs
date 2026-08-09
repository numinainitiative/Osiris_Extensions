namespace XboxLibrary.Models;

public class AthenticationRequest
{
	public class AthenticationRequestProperties
	{
		public string AuthMethod { get; set; } = "RPS";

		public string SiteName { get; set; } = "user.auth.xboxlive.com";

		public string RpsTicket { get; set; }
	}

	public string RelyingParty { get; set; } = "http://auth.xboxlive.com";

	public string TokenType { get; set; } = "JWT";

	public AthenticationRequestProperties Properties { get; set; } = new AthenticationRequestProperties();
}
