using System;
using System.Linq;
using System.Reflection;
using Playnite.SDK.Models;

namespace Osiris.Extensions
{
    // Older hosts keep their original independent-page behaviour. Resolve the
    // optional host contract at call time, after the theme has been loaded.
    public static class EditionMirrorBridge
    {
        private static Type Host => AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "OsirisTheme")?
            .GetType("OsirisTheme.EditionPageSharing", false);

        public static Guid ResolveOwner(Guid gameId, string page)
        {
            var method = Host?.GetMethod("ResolveOwner", BindingFlags.Public | BindingFlags.Static);
            return method == null ? gameId : (Guid)method.Invoke(null, new object[] { gameId, page });
        }

        public static Game ResolveGame(Game game, string page)
        {
            if (game == null) return null;
            var method = Host?.GetMethod("ResolveGame", BindingFlags.Public | BindingFlags.Static);
            return method == null ? game : (Game)method.Invoke(null, new object[] { game, page });
        }
    }
}
