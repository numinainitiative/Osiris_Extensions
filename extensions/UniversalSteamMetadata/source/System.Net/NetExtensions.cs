namespace System.Net;

public static class NetExtensions
{
	public static bool IsSuccess(this HttpStatusCode statusCode)
	{
		if (statusCode >= HttpStatusCode.OK)
		{
			return statusCode < HttpStatusCode.MultipleChoices;
		}
		return false;
	}
}
