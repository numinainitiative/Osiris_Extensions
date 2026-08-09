using Playnite.SDK;
using Playnite.SDK.Data;
using FlowHttp;
using SteamCommon.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Threading;

namespace SteamCommon
{
    class SteamWeb
    {
        private static ILogger logger = LogManager.GetLogger();
        private const string steamGameSearchUrl = @"https://store.steampowered.com/search/?term={0}&ignore_preferences=1&category1=998&ndl=1";

        public static List<GenericItemOption> GetSteamSearchGenericItemOptions(string searchTerm)
        {
            return GetSteamSearchResults(searchTerm).Select(x => new GenericItemOption(x.Name, x.GameId)).ToList();
        }

        public static string GetSteamIdFromSearch(string searchTerm, string steamApiCountry = null, CancellationToken cancelToken = default)
        {
            var normalizedName = searchTerm.NormalizeGameName();
            var results = GetSteamSearchResults(normalizedName);
            results.ForEach(a => a.Name = a.Name.NormalizeGameName());

            var matchingGameName = normalizedName.Satinize();
            var exactMatch = results.FirstOrDefault(x => x.Name.Satinize() == matchingGameName);
            if (!(exactMatch is null))
            {
                logger.Info($"Found steam id for search {searchTerm} via steam search, Id: {exactMatch.GameId}");
                return exactMatch.GameId;
            }

            var bestAvailableMatch = results.FirstOrDefault();
            if (!(bestAvailableMatch is null))
            {
                logger.Info($"Using best Steam search match for {searchTerm}: {bestAvailableMatch.Name}, Id: {bestAvailableMatch.GameId}");
                return bestAvailableMatch.GameId;
            }

            logger.Info($"Steam id for search {searchTerm} not found");
            return null;
        }

        public static List<StoreSearchResult> GetSteamSearchResults(string searchTerm, string steamApiCountry = null, CancellationToken cancelToken = default)
        {
            var results = new List<StoreSearchResult>();
            var searchPageSrc = HttpRequestFactory.GetHttpRequest()
                .WithUrl(GetStoreSearchUrl(searchTerm, steamApiCountry))
                .DownloadString(cancelToken);
            if (searchPageSrc.IsSuccess)
            {
                foreach (Match gameMatch in Regex.Matches(
                    searchPageSrc.Content,
                    @"<a\b(?<attributes>[^>]*\bclass\s*=\s*([""'])[^""']*\bsearch_result_row\b[^""']*\2[^>]*)>(?<content>[\s\S]*?)</a\s*>",
                    RegexOptions.IgnoreCase))
                {
                    var attributes = gameMatch.Groups["attributes"].Value;
                    var content = gameMatch.Groups["content"].Value;
                    if (!GetHtmlAttribute(attributes, "data-ds-packageid").IsNullOrEmpty())
                    {
                        continue;
                    }

                    // Game Data
                    var gameId = GetHtmlAttribute(attributes, "data-ds-appid");
                    if (gameId.IsNullOrEmpty())
                    {
                        continue;
                    }

                    var title = GetElementContentByClass(content, "title");
                    var releaseDate = GetElementContentByClass(content, "search_released");

                    // Prices Data
                    var discountPercentage = 0;
                    double priceFinal = 0;
                    double priceOriginal = 0;
                    var isDiscounted = false;
                    string currency = null;
                    var isReleased = false;
                    var isFree = false;

                    if (!GetElementContentByClass(content, "search_discount_and_price").IsNullOrWhiteSpace())
                    {
                        // Game has pricing data
                        var discountBlock = GetOpeningTagByClass(content, "discount_block");
                        if (!discountBlock.IsNullOrEmpty())
                        {
                            if (int.TryParse(GetHtmlAttribute(discountBlock, "data-discount"), out var parsedDiscount))
                            {
                                discountPercentage = parsedDiscount;
                            }

                            if (int.TryParse(GetHtmlAttribute(discountBlock, "data-price-final"), out var parsedFinalPrice))
                            {
                                priceFinal = parsedFinalPrice * 0.01;
                            }
                        }

                        priceOriginal = GetSearchOriginalPrice(priceFinal, discountPercentage);
                        isDiscounted = priceFinal != priceOriginal && priceOriginal != 0;
                        GetCurrencyFromSearchPriceHtml(content, out currency, out isReleased, out isFree);
                    }

                    //Urls
                    var storeUrl = GetHtmlAttribute(attributes, "href");
                    var capsule = Regex.Match(
                        content,
                        @"<[^>]*\bclass\s*=\s*([""'])[^""']*\bsearch_capsule\b[^""']*\1[^>]*>[\s\S]*?<img\b(?<attributes>[^>]*)>",
                        RegexOptions.IgnoreCase);
                    var capsuleUrl = capsule.Success
                        ? GetHtmlAttribute(capsule.Groups["attributes"].Value, "src")
                        : null;

                    results.Add(new StoreSearchResult
                    {
                        Name = HttpUtility.HtmlDecode(title),
                        Description = HttpUtility.HtmlDecode(releaseDate),
                        GameId = gameId,
                        PriceOriginal = priceOriginal,
                        PriceFinal = priceFinal,
                        IsDiscounted = isDiscounted,
                        DiscountPercentage = discountPercentage,
                        StoreUrl = storeUrl,
                        IsFree = isFree,
                        IsReleased = isReleased,
                        Currency = currency,
                        BannerImageUrl = capsuleUrl
                    });
                }
            }

            logger.Debug($"Obtained {results.Count} games from Steam search term {searchTerm}");
            return results;
        }

        private static void GetCurrencyFromSearchPriceHtml(
            string content,
            out string currency,
            out bool isReleased,
            out bool isFree)
        {
            isReleased = false;
            isFree = false;

            var price = GetElementContentByClass(content, "discount_final_price");
            currency = !price.IsNullOrEmpty() ? GetCurrencyFromPriceString(price) : null;

            if (HasClasses(content, "search_discount_block", "no_discount"))
            {
                // Non discounted item
                isReleased = true;
                isFree = currency.IsNullOrEmpty();
                return;
            }

            if (!GetOpeningTagByClass(content, "search_discount_block").IsNullOrEmpty())
            {
                // Discounted item
                isReleased = true;
                isFree = currency.IsNullOrEmpty();
                return;
            }
        }

        private static string GetHtmlAttribute(string html, string attribute)
        {
            var match = Regex.Match(
                html ?? string.Empty,
                @"\b" + Regex.Escape(attribute) + @"\s*=\s*(?:([""'])(?<quoted>.*?)\1|(?<bare>[^\s>]+))",
                RegexOptions.IgnoreCase);
            var value = match.Groups["quoted"].Success
                ? match.Groups["quoted"].Value
                : match.Groups["bare"].Value;
            return HttpUtility.HtmlDecode(value);
        }

        private static string GetElementContentByClass(string html, string className)
        {
            var match = Regex.Match(
                html ?? string.Empty,
                @"<(?<tag>[a-z0-9]+)\b[^>]*\bclass\s*=\s*([""'])(?=[^""']*\b" + Regex.Escape(className) + @"\b)[^""']*\2[^>]*>(?<content>[\s\S]*?)</\k<tag>\s*>",
                RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["content"].Value : string.Empty;
        }

        private static string GetOpeningTagByClass(string html, string className)
        {
            var match = Regex.Match(
                html ?? string.Empty,
                @"<[^>]*\bclass\s*=\s*([""'])(?=[^""']*\b" + Regex.Escape(className) + @"\b)[^""']*\1[^>]*>",
                RegexOptions.IgnoreCase);
            return match.Success ? match.Value : string.Empty;
        }

        private static bool HasClasses(string html, params string[] classNames)
        {
            foreach (Match tag in Regex.Matches(
                html ?? string.Empty,
                @"<[^>]*\bclass\s*=\s*([""'])(?<classes>[^""']*)\1[^>]*>",
                RegexOptions.IgnoreCase))
            {
                var classes = tag.Groups["classes"].Value;
                if (classNames.All(name => Regex.IsMatch(classes, @"(?:^|\s)" + Regex.Escape(name) + @"(?:\s|$)", RegexOptions.IgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetCurrencyFromPriceString(string priceString)
        {
            if (!Regex.IsMatch(priceString, @"\d"))
            {
                // Game is free
                return null;
            }

            return Regex.Match(priceString, @"[^\s]+").Value;
        }

        private static string GetStoreSearchUrl(string searchTerm, string steamApiCountry)
        {
            var searchUrl = string.Format(steamGameSearchUrl, searchTerm.EscapeDataString());
            if (!steamApiCountry.IsNullOrEmpty())
            {
                searchUrl += $"&cc={steamApiCountry}";
            }

            return searchUrl;
        }

        private static double GetSearchOriginalPrice(double priceFinal, int discountPercentage)
        {
            if (discountPercentage == 0)
            {
                return priceFinal;
            }

            return (100 * priceFinal) / (100 - discountPercentage);
        }

        private const string steamAppDetailsMask = @"https://store.steampowered.com/api/appdetails?appids={0}";
        public static SteamAppDetails GetSteamAppDetails(string steamId, CancellationToken cancelToken = default)
        {
            var url = string.Format(steamAppDetailsMask, steamId);
            var request = HttpRequestFactory.GetHttpRequest().WithUrl(url);
            var downloadedString = request.DownloadString(cancelToken);
            if (downloadedString.IsSuccess)
            {
                var parsedData = Serialization.FromJson<Dictionary<string, SteamAppDetails>>(downloadedString.Content);
                if (parsedData.Keys?.Any() == true)
                {
                    var response = parsedData[parsedData.Keys.First()];
                    if (response.success == true && response.data != null)
                    {
                        return response;
                    }
                }
            }

            return null;
        }
    }
}
