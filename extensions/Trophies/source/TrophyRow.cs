using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;

namespace Osiris.Extensions.Trophies
{
    public enum TrophyHighlight { None, Gold, Silver, Purple, Grey }

    // Presentation-only reveal state: never changes the catalogue or unlock state.
    public sealed class TrophyRow : INotifyPropertyChanged, ICommand
    {
        public Trophy Trophy { get; }
        private bool revealed;
        private readonly bool popup;
        public TrophyRow(Trophy trophy, bool allowReveal, TrophyHighlight highlight = TrophyHighlight.None) { Trophy = trophy; popup=allowReveal; CanReveal = allowReveal && trophy.Hidden && !trophy.UnlockedUtc.HasValue; Highlight = highlight == TrophyHighlight.None ? TrophyHighlight.None : trophy.UnlockedUtc.HasValue ? highlight : TrophyHighlight.Grey; }
        public TrophyHighlight Highlight { get; }
        public string Name => Trophy.Name;
        public string Category => Trophy.Hidden ? "Hidden" : "Achievements";
        public bool CanReveal { get; }
        public string DisplayDescription => revealed || (popup && (!Trophy.Hidden || Trophy.UnlockedUtc.HasValue)) ? (string.IsNullOrWhiteSpace(Trophy.Description) ? "Description unavailable" : Trophy.Description) : Trophy.DisplayDescription;
        public string UnlockDateText => popup && Trophy.UnlockedUtc.HasValue ? Trophy.UnlockedUtc.Value.ToLocalTime().ToString("g") : "";
        public string RarityText => Trophy.GlobalPercent.HasValue && Trophy.GlobalPercent >= 0 && Trophy.GlobalPercent <= 100 ? Trophy.GlobalPercent.Value.ToString("0.##", CultureInfo.CurrentCulture) + "%" : "";
        public string PlatformText => popup && Trophy.ExophaseUnlocks?.Count>0 ? "Earned on " + string.Join(" · ",Trophy.ExophaseUnlocks.Select(u=>u.Platform).Distinct()) + " (via Exophase)" : popup && Trophy.UnlockedUtc.HasValue && Trophy.NativePlatform!=null ? "Earned on "+Trophy.NativePlatform : "";
        public ICommand RevealCommand => this;
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object parameter) => CanReveal;
        public void Execute(object parameter) { if (!CanReveal) return; revealed = !revealed; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayDescription))); }
        public static ListCollectionView Group(IEnumerable<Trophy> trophies, bool reveal, int limit = int.MaxValue, bool rarity = false, bool lastAchieved = false)
        {
            var entries = trophies.ToList();
            var highlights = Highlights(entries);
            var ordered = entries.OrderBy(t => t.Hidden).ThenByDescending(t => t.UnlockedUtc.HasValue);
            var rows = (lastAchieved ? ordered.ThenByDescending(t => t.UnlockedUtc ?? DateTime.MinValue)
                                        .ThenBy(t => t.UnlockedUtc.HasValue && ValidRarity(t) ? t.GlobalPercent.Value : double.MaxValue)
                                        .ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
                         : rarity ? ordered.ThenBy(t => t.GlobalPercent ?? double.MaxValue).ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
                              : ordered.ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase))
                .ThenBy(t => t.Id, StringComparer.Ordinal).Take(limit).Select(t => Row(t, reveal, highlights)).ToList();
            var view = new ListCollectionView(rows);
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Category)));
            return view;
        }

        public static List<TrophyRow> Preview(IEnumerable<Trophy> trophies)
        {
            var entries = trophies.ToList();
            var highlights = Highlights(entries);
            // Latest earned first, irrespective of category. Fill remaining slots with
            // common non-secret achievements; use unknown/hidden entries only as fallback.
            return entries.OrderByDescending(t => t.UnlockedUtc.HasValue)
                .ThenByDescending(t => t.UnlockedUtc ?? DateTime.MinValue)
                .ThenBy(t => t.UnlockedUtc.HasValue && ValidRarity(t) ? t.GlobalPercent.Value : double.MaxValue)
                .ThenBy(t => t.Hidden)
                .ThenByDescending(t => ValidRarity(t) ? t.GlobalPercent.Value : -1)
                .ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(t => t.Id, StringComparer.Ordinal)
                .Take(3).Select(t => Row(t, false, highlights)).ToList();
        }

        private static bool ValidRarity(Trophy trophy) => trophy.GlobalPercent.HasValue && trophy.GlobalPercent.Value >= 0 && trophy.GlobalPercent.Value <= 100;
        private static Dictionary<Trophy, TrophyHighlight> Highlights(IEnumerable<Trophy> entries)
        {
            var ranks = entries.Where(ValidRarity).Distinct().OrderBy(t => t.GlobalPercent.Value)
                .ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(t => t.Id, StringComparer.Ordinal).Take(10).ToList();
            var result = new Dictionary<Trophy, TrophyHighlight>();
            for (var i = 0; i < ranks.Count; i++) result.Add(ranks[i], i == 0 ? TrophyHighlight.Purple : i < 5 ? TrophyHighlight.Gold : TrophyHighlight.Silver);
            return result;
        }
        private static TrophyRow Row(Trophy trophy, bool reveal, Dictionary<Trophy, TrophyHighlight> highlights) =>
            new TrophyRow(trophy, reveal, highlights.TryGetValue(trophy, out var tier) ? tier : TrophyHighlight.None);
    }
}
