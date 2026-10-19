using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using PlayniteExtensions.Common;
class Program
{
    static void Main()
    {
        var assembly = typeof(GameNameMatcher).Assembly;
        var provider = assembly.GetTypes().Single(t => t.Name == "UniversalSteamMetadataProvider");
        var resultType = assembly.GetTypes().Single(t => t.Name == "StoreSearchResult");
        var listType = typeof(List<>).MakeGenericType(resultType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType);
        Action<string,uint> add = (name,id) => { var entry=Activator.CreateInstance(resultType); resultType.GetProperty("Name").SetValue(entry,name); resultType.GetProperty("GameId").SetValue(entry,id); list.Add(entry); };
        var match = provider.GetMethod("MatchEditionVariant",BindingFlags.Static|BindingFlags.NonPublic);
        add("Call of Duty®: Advanced Warfare - Gold Edition",209650);
        if((uint)match.Invoke(null,new object[]{"Call of Duty: Advanced Warfare",list})!=209650) throw new Exception("Gold edition match failed");
        add("Call of Duty: Advanced Warfare - Deluxe Edition",123);
        if((uint)match.Invoke(null,new object[]{"Call of Duty: Advanced Warfare",list})!=0) throw new Exception("Ambiguous edition accepted");
        if(GameNameMatcher.ToEditionIndependentKey("Modern Warfare Remastered")==GameNameMatcher.ToEditionIndependentKey("Modern Warfare")) throw new Exception("Remaster incorrectly merged");
        list.Clear(); add("Call of Duty: Advanced Warfare - Multiplayer",123);
        if((uint)match.Invoke(null,new object[]{"Call of Duty: Advanced Warfare",list})!=0) throw new Exception("Multiplayer incorrectly matched");
        var search = assembly.GetTypes().Single(t => t.Name == "UniversalSteamMetadata").GetMethod("GetSearchResults");
        var live=search.Invoke(null,new object[]{"Call of Duty Advanced Warfare"});
        if((uint)match.Invoke(null,new object[]{"Call of Duty: Advanced Warfare",live})!=209650) throw new Exception("Live background match failed");
        Console.WriteLine("5 Steam matching checks passed, including live Advanced Warfare -> 209650.");
    }
}
