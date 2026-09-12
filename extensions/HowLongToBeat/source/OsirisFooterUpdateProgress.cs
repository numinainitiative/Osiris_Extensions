using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace Osiris.Extensions.HowLongToBeat
{
    /// <summary>
    /// Optional bridge to Osiris's bottom-panel update surface. The extension
    /// remains loadable outside Osiris because the theme is discovered only at
    /// runtime and no compile-time theme dependency is introduced.
    /// </summary>
    internal sealed class OsirisFooterUpdateProgress
    {
        private const BindingFlags StaticMethodFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        private readonly Window owner;
        private readonly Type updateActionsType;
        private bool finished;

        public CancellationToken CancellationToken { get; }

        private OsirisFooterUpdateProgress(
            Window owner,
            Type updateActionsType,
            CancellationToken cancellationToken)
        {
            this.owner = owner;
            this.updateActionsType = updateActionsType;
            CancellationToken = cancellationToken;
        }

        public static OsirisFooterUpdateProgress TryStart(
            string title,
            string detail)
        {
            var owner = Application.Current?.MainWindow;
            if (owner == null)
            {
                return null;
            }

            try
            {
                var updateActionsType = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(assembly => assembly.GetType(
                        "OsirisTheme.OsirisUpdateActions",
                        false))
                    .FirstOrDefault(type => type != null);
                var begin = updateActionsType?.GetMethod(
                    "BeginFooterUpdateProcess",
                    StaticMethodFlags,
                    null,
                    new[] { typeof(Window) },
                    null);
                if (begin == null)
                {
                    return null;
                }

                var cancellationToken = (CancellationToken)begin.Invoke(
                    null,
                    new object[] { owner });
                var progress = new OsirisFooterUpdateProgress(
                    owner,
                    updateActionsType,
                    cancellationToken);
                progress.Report(title, detail);
                return progress;
            }
            catch
            {
                return null;
            }
        }

        public void Report(string title, string detail)
        {
            if (finished)
            {
                return;
            }

            Invoke(
                "ShowFooterUpdateProgress",
                new[]
                {
                    typeof(Window),
                    typeof(string),
                    typeof(string),
                    typeof(double),
                    typeof(int),
                    typeof(int)
                },
                owner,
                title,
                detail,
                0d,
                1,
                1);
        }

        public void Complete(string message)
        {
            if (finished)
            {
                return;
            }

            finished = true;
            Invoke(
                "CompleteFooterUpdateProcess",
                new[] { typeof(Window), typeof(string) },
                owner,
                message);
        }

        public void Cancel()
        {
            if (finished)
            {
                return;
            }

            finished = true;
            Invoke(
                "ShowFooterUpdateCancelled",
                new[] { typeof(Window) },
                owner);
        }

        private void Invoke(
            string methodName,
            Type[] parameterTypes,
            params object[] arguments)
        {
            try
            {
                updateActionsType
                    .GetMethod(
                        methodName,
                        StaticMethodFlags,
                        null,
                        parameterTypes,
                        null)
                    ?.Invoke(null, arguments);
            }
            catch
            {
                // Bottom-panel presentation is optional. Database work remains
                // available if the host theme changes or is not Osiris.
            }
        }
    }
}
