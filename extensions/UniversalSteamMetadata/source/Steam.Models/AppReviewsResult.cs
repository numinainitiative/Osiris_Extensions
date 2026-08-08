namespace Steam.Models;

public class AppReviewsResult
{
	public class QuerySummary
	{
		public int num_reviews { get; set; }

		public int review_score { get; set; }

		public string review_score_desc { get; set; }

		public int total_positive { get; set; }

		public int total_negative { get; set; }

		public int total_reviews { get; set; }
	}

	public int success { get; set; }

	public QuerySummary query_summary { get; set; }
}
