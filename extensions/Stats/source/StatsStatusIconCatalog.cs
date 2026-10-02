using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Osiris.Extensions.Stats
{
    // Exact canonical Game Details geometries from ThemeSource/Code/OsirisGameStatus.cs.
    internal static class StatsStatusIconCatalog
    {
        private static readonly Dictionary<string, Geometry> Icons = CreateIconGeometries();

        internal static Geometry Get(string statusName)
        {
            Geometry icon;
            return Icons.TryGetValue(statusName ?? string.Empty, out icon)
                ? icon
                : Icons["Not Played"];
        }

        private static Dictionary<string, Geometry> CreateIconGeometries()
        {
            return new Dictionary<string, Geometry>(StringComparer.OrdinalIgnoreCase)
            {
                ["Not Played"] = Combine(
                    "M11.017,2.814 A1,1 0 0 1 12.983,2.814 L14.034,8.372 A2,2 0 0 0 15.628,9.966 L21.186,11.017 A1,1 0 0 1 21.186,12.983 L15.628,14.034 A2,2 0 0 0 14.034,15.628 L12.983,21.186 A1,1 0 0 1 11.017,21.186 L9.966,15.628 A2,2 0 0 0 8.372,14.034 L2.814,12.983 A1,1 0 0 1 2.814,11.017 L8.372,9.966 A2,2 0 0 0 9.966,8.372 Z",
                    "M20,2 V6", "M22,4 H18", null, new EllipseGeometry(new Point(4, 20), 2, 2)),
                ["Playing"] = Combine(
                    "M13,19 L19,13",
                    "M14.5,17.5 L3.586,6.586 A2,2 0 0 1 3,5.172 V3 H5.172 A2,2 0 0 1 6.586,3.586 L17.5,14.5",
                    "M14.828,6.172 L17.414,3.586 A2,2 0 0 1 18.828,3 H21 V5.172 A2,2 0 0 1 20.414,6.586 L17.828,9.172",
                    "M16,16 L20,20", "M19,21 L21,19", "M5,14 L9,18", "M5,21 L3,19", "M7.5,16.5 L4,20"),
                ["Played"] = Combine(
                    "M11,19 L5,13", "M5,21 L3,19", "M8,16 L4,20",
                    "M9.5,17.5 L20.414,6.586 A2,2 0 0 0 21,5.172 V3 H18.828 A2,2 0 0 0 17.414,3.586 L6.5,14.5"),
                ["Abandoned"] = Combine(
                    "M15,10 V11",
                    "M7.528,20.472 A1.6,1.6 0 0 1 9.805,20.472 L10.862,21.528 A1.6,1.6 0 0 0 13.138,21.528 L14.195,20.472 A1.6,1.6 0 0 1 16.472,20.472 L17.586,21.586 A1.4,1.4 0 0 0 20,20.586 V10 A8,8 0 0 0 4,10 V20.586 A1.4,1.4 0 0 0 6.414,21.586 Z",
                    "M9,10 V11"),
                ["Completed"] = Combine(
                    "M4,20 A2,2 0 0 1 6,18 H18 A2,2 0 0 1 20,20 V21 A1,1 0 0 1 19,22 H5 A1,1 0 0 1 4,21 Z",
                    "M12.474,5.943 L14.041,11.283 A1,1 0 0 0 15.791,11.611 L18.407,8.209",
                    "M20,9 L17,18",
                    "M5.594,8.209 L8.209,11.612 A1,1 0 0 0 9.959,11.283 L11.526,5.943",
                    "M7,18 L4,9", null,
                    new EllipseGeometry(new Point(12, 4), 2, 2),
                    new EllipseGeometry(new Point(20, 7), 2, 2),
                    new EllipseGeometry(new Point(4, 7), 2, 2)),
                ["Beaten"] = Combine(
                    "M10,14.66 V17 A1,1 0 0 1 9,18 A2,2 0 0 0 7,20 V22",
                    "M14,14.66 V17 A1,1 0 0 0 15,18 A2,2 0 0 1 17,20 V22",
                    "M17.916,10 H19.5 A2.5,2.5 0 0 0 22,7.5 V5 A1,1 0 0 0 21,4 H18",
                    "M4,22 H20",
                    "M6,9 A6,6 0 0 0 18,9 V3 A1,1 0 0 0 17,2 H7 A1,1 0 0 0 6,3 Z",
                    "M6.084,10 H4.5 A2.5,2.5 0 0 1 2,7.5 V5 A1,1 0 0 1 3,4 H6")
            };
        }

        private static Geometry Combine(params object[] parts)
        {
            var group = new GeometryGroup { FillRule = FillRule.Nonzero };
            foreach (var part in parts)
            {
                if (part is string path && !string.IsNullOrWhiteSpace(path))
                    group.Children.Add(Geometry.Parse(path));
                else if (part is Geometry geometry)
                    group.Children.Add(geometry);
            }
            group.Freeze();
            return group;
        }
    }
}

