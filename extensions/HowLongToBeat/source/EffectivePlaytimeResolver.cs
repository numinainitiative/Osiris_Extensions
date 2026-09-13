using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Playnite.SDK.Models;

namespace Osiris.Extensions.HowLongToBeat
{
    internal static class EffectivePlaytimeResolver
    {
        private const string ExophasePluginTypeName =
            "Osiris.Extensions.Exophase.ExophasePlugin";
        private const string EffectivePlaytimeMethodName =
            "GetEffectivePlaytimeForOsiris";

        public static ulong Resolve(Game game)
        {
            return Resolve(game, TryResolveFromExophase);
        }

        internal static ulong Resolve(
            Game game,
            Func<Guid, ulong?> externalResolver)
        {
            var nativePlaytime = game?.Playtime ?? 0UL;
            if (game == null || externalResolver == null)
            {
                return nativePlaytime;
            }

            try
            {
                return externalResolver(game.Id) ?? nativePlaytime;
            }
            catch
            {
                return nativePlaytime;
            }
        }

        private static ulong? TryResolveFromExophase(Guid gameId)
        {
            var pluginType = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(ExophasePluginTypeName, false))
                .FirstOrDefault(type => type != null);
            var currentProperty = pluginType?.GetProperty(
                "Current",
                BindingFlags.Public | BindingFlags.Static);
            var plugin = currentProperty?.GetValue(null, null);
            var method = pluginType?.GetMethod(
                EffectivePlaytimeMethodName,
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(string) },
                null);
            if (plugin == null || method == null)
            {
                return null;
            }

            try
            {
                var value = method.Invoke(
                    plugin,
                    new object[] { gameId.ToString("D") });
                return value == null
                    ? (ulong?)null
                    : Convert.ToUInt64(value, CultureInfo.InvariantCulture);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }
    }
}
