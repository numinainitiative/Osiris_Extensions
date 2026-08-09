using System;
using System.Collections.Generic;

namespace XboxLibrary.Models;

public class AuthorizationData
{
	public class DisplayClaimsData
	{
		public class XuiData
		{
			public string uhs;

			public string usr;

			public string utr;

			public string prv;

			public string xid;

			public string gtg;
		}

		public List<XuiData> xui;
	}

	public string Token;

	public DateTime IssueInstant;

	public DateTime NotAfter;

	public DisplayClaimsData DisplayClaims;
}
