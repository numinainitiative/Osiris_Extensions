using System.Collections.Generic;

namespace XboxLibrary.Models;

public class AuhtorizationRequest
{
	public class AuhtorizationRequestProperties
	{
		public string SandboxId { get; set; } = "RETAIL";

		public List<string> UserTokens { get; set; }
	}

	public string RelyingParty { get; set; } = "http://xboxlive.com";

	public string TokenType { get; set; } = "JWT";

	public AuhtorizationRequestProperties Properties { get; set; } = new AuhtorizationRequestProperties();
}
