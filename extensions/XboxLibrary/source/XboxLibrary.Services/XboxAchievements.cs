using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Playnite.SDK.Data;

namespace XboxLibrary.Services;

public partial class XboxAccountClient
{
    public sealed class AchievementTitle { public string id; }
    public sealed class AchievementProgress { public string timeUnlocked; }
    public sealed class Achievement
    {
        public string id, serviceConfigId, name, description, progressState;
        public bool isRevoked;
        public AchievementProgress progression;
        public List<AchievementTitle> titleAssociations;
    }
    public sealed class AchievementPaging { public string continuationToken; }
    public sealed class AchievementResponse { public List<Achievement> achievements; public AchievementPaging pagingInfo; }
    public async Task<string> GetAchievementsForOsiris(string titleId, CancellationToken token)
    {
        if (!uint.TryParse(titleId,out _)) throw new InvalidDataException("Invalid Xbox title ID.");
        var auth=GetSavedXstsTokens();
        var account=auth?.DisplayClaims?.xui?[0]?.xid;
        if (!ulong.TryParse(account,out _)) throw new InvalidDataException("Connect your Xbox Library account first.");
        using var client=new HttpClient(new HttpClientHandler { AllowAutoRedirect=false }) { Timeout=TimeSpan.FromSeconds(30) };
        SetAuthenticationHeaders(client.DefaultRequestHeaders,auth);
        var results=new List<Achievement>(); var tokens=new HashSet<string>(); var ids=new HashSet<string>();
        string continuation=null;
        for (var page=0;page<50;page++)
        {
            token.ThrowIfCancellationRequested();
            var url="https://achievements.xboxlive.com/users/xuid("+account+")/achievements?titleId="+titleId+"&maxItems=100&unlockedOnly=false&types=Persistent";
            if (continuation!=null) url+="&continuationToken="+Uri.EscapeDataString(continuation);
            using var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new InvalidDataException("Xbox achievement sync unavailable. Reconnect Xbox Library if needed.");
            using var input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var output=new MemoryStream(); var buffer=new byte[8192]; int read;
            while ((read=await input.ReadAsync(buffer,0,buffer.Length,token).ConfigureAwait(false))>0)
            { if (output.Length+read>5*1024*1024) throw new InvalidDataException("Xbox response too large."); output.Write(buffer,0,read); }
            var parsed=Serialization.FromJson<AchievementResponse>(System.Text.Encoding.UTF8.GetString(output.ToArray()));
            if (parsed?.achievements==null || parsed.pagingInfo==null) throw new InvalidDataException("Invalid Xbox achievements response.");
            foreach (var achievement in parsed.achievements)
            {
                if (string.IsNullOrWhiteSpace(achievement.id) || !ids.Add(achievement.serviceConfigId+":"+achievement.id)) throw new InvalidDataException("Duplicate Xbox achievement response.");
                results.Add(achievement);
            }
            continuation=parsed.pagingInfo?.continuationToken;
            if (string.IsNullOrEmpty(continuation)) return Serialization.ToJson(new { Account=account, TitleId=titleId, Achievements=results });
            if (!tokens.Add(continuation)) throw new InvalidDataException("Invalid Xbox pagination.");
        }
        throw new InvalidDataException("Xbox achievement catalogue exceeded the page limit.");
    }
}
