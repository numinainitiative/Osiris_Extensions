using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace Osiris.Extensions.FunGames
{
    public sealed class RouletteOption
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public Guid? GameId { get; set; }
        public RouletteOption Clone() => new RouletteOption { Name=Name,Description=Description,GameId=GameId };
    }
    public sealed class RouletteDefinition
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public List<RouletteOption> Options { get; set; } = new List<RouletteOption>();
        public RouletteDefinition Clone() => new RouletteDefinition { Id=Id, Name=Name, Options=Options.Select(o=>o.Clone()).ToList() };
        public override string ToString() => Name;
        public string Validate()
        {
            if(string.IsNullOrWhiteSpace(Name) || Name.Trim().Length>80) return "Give the roulette a name (up to 80 characters).";
            if(Options==null || Options.Count<2 || Options.Count>32) return "Add between 2 and 32 options.";
            if(Options.Any(o=>o==null || string.IsNullOrWhiteSpace(o.Name) || o.Name.Trim().Length>80)) return "Each option needs a name (up to 80 characters).";
            if(Options.Any(o=>(o.Description??"").Length>1000)) return "Keep each description within 1,000 characters.";
            return null;
        }
    }
    public sealed class RouletteStore
    {
        private readonly string path;
        private bool readFailed;
        private sealed class Document { public int SchemaVersion {get;set;}=1; public List<RouletteDefinition> Roulettes {get;set;}=new List<RouletteDefinition>(); }
        public RouletteStore(string folder) { path=Path.Combine(folder,"roulettes.json"); }
        public List<RouletteDefinition> Load()
        {
            if(!File.Exists(path)) { readFailed=false; return new List<RouletteDefinition>(); }
            try
            {
                if(new FileInfo(path).Length>8*1024*1024) throw new InvalidDataException("Saved roulettes file is too large.");
                var doc=JsonConvert.DeserializeObject<Document>(File.ReadAllText(path),new JsonSerializerSettings { MaxDepth=16,TypeNameHandling=TypeNameHandling.None });
                if(doc==null || doc.SchemaVersion!=1 || doc.Roulettes==null || doc.Roulettes.Count>100) throw new InvalidDataException("Unsupported roulettes data.");
                if(doc.Roulettes.Any(r=>r==null || r.Id==Guid.Empty || r.Validate()!=null) || doc.Roulettes.Select(r=>r.Id).Distinct().Count()!=doc.Roulettes.Count) throw new InvalidDataException("Saved roulettes data is invalid.");
                readFailed=false; return doc.Roulettes;
            }
            catch { readFailed=true; throw; }
        }
        public void Save(RouletteDefinition roulette)
        {
            if(readFailed) throw new IOException("Saved roulettes could not be read. The existing file has been preserved.");
            var error=roulette?.Validate(); if(roulette==null || error!=null) throw new ArgumentException(error??"Missing roulette.");
            var items=Load();
            var copy=roulette.Clone(); copy.Name=copy.Name.Trim();
            foreach(var option in copy.Options) { option.Name=option.Name.Trim(); option.Description=(option.Description??"").Trim(); }
            if(copy.Id==Guid.Empty) copy.Id=Guid.NewGuid();
            var index=items.FindIndex(r=>r.Id==copy.Id);
            if(items.Any(r=>r.Id!=copy.Id && string.Equals(r.Name,copy.Name,StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("A roulette with this name already exists. Choose another name.");
            if(index>=0) items[index]=copy; else { if(items.Count>=100) throw new ArgumentException("You can save up to 100 roulettes."); items.Add(copy); }
            Write(items);roulette.Id=copy.Id;
        }
        public bool Delete(Guid id)
        {
            if(readFailed)throw new IOException("Saved roulettes could not be read. Existing data has been preserved.");
            if(id==Guid.Empty)throw new ArgumentException("The default roulette cannot be deleted.");
            var items=Load();if(items.RemoveAll(r=>r.Id==id)==0)return false;
            Write(items);return true;
        }
        private void Write(List<RouletteDefinition> items)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary=path+".tmp-"+Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary,JsonConvert.SerializeObject(new Document {Roulettes=items},Formatting.Indented));
                if(File.Exists(path)) File.Replace(temporary,path,path+".bak"); else File.Move(temporary,path);
            }
            finally { if(File.Exists(temporary)) File.Delete(temporary); }
        }
    }
    // Portable files deliberately contain no local game IDs or library/profile data.
    public static class RouletteTransfer
    {
        private sealed class Option { public string Name {get;set;} public string Description {get;set;} }
        private sealed class Document { public int SchemaVersion {get;set;} public string Name {get;set;} public List<Option> Options {get;set;} }
        public static string Export(RouletteDefinition roulette)
        {
            if(roulette==null || roulette.Validate()!=null)throw new ArgumentException("Choose a valid roulette to export.");
            return JsonConvert.SerializeObject(new Document {SchemaVersion=1,Name=roulette.Name,Options=roulette.Options.Select(o=>new Option {Name=o.Name,Description=o.Description??""}).ToList()},Formatting.Indented);
        }
        public static RouletteDefinition Import(string json)
        {
            if(string.IsNullOrWhiteSpace(json) || json.Length>1024*1024)throw new InvalidDataException("The roulette file is empty or too large.");
            var doc=JsonConvert.DeserializeObject<Document>(json,new JsonSerializerSettings {MaxDepth=8,TypeNameHandling=TypeNameHandling.None});
            if(doc==null || doc.SchemaVersion!=1 || doc.Options==null || doc.Options.Any(o=>o==null))throw new InvalidDataException("Unsupported roulette file.");
            var roulette=new RouletteDefinition {Name=doc.Name,Options=doc.Options.Select(o=>new RouletteOption {Name=o.Name,Description=o.Description??""}).ToList()};
            var error=roulette.Validate();if(error!=null)throw new InvalidDataException(error);
            return roulette;
        }
    }
    public static class RouletteRandom
    {
        public static int Choose(int count)
        {
            if(count<1) throw new ArgumentOutOfRangeException(nameof(count));
            // Rejection sampling avoids modulo bias; every sector has equal probability.
            using(var random=RandomNumberGenerator.Create())
            {
                var bytes=new byte[4]; var limit=(1UL<<32)-((1UL<<32)%(uint)count); uint value;
                do { random.GetBytes(bytes); value=BitConverter.ToUInt32(bytes,0); } while(value>=limit);
                return (int)(value%(uint)count);
            }
        }
        public static double LandingPosition(int index,int count,double current)
        {
            if(count<2 || index<0 || index>=count) throw new ArgumentOutOfRangeException();
            // Card units, not pixels: resizing during a spin cannot change the result.
            var minimum=current+Math.Max(30,count*5);
            return Math.Ceiling((minimum-index)/count)*count+index;
        }
    }
}
