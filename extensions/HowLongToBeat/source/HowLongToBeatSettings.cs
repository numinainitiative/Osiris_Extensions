using System.Collections.Generic;
using Playnite.SDK;

namespace Osiris.Extensions.HowLongToBeat
{
    /// <summary>
    /// Supplies the Playnite settings transaction required by Osiris's shared
    /// extension-management host. HowLongToBeat has no extension-owned options
    /// yet; the host contributes the transactional Danger Zone.
    /// </summary>
    public sealed class HowLongToBeatSettings : ISettings
    {
        public void BeginEdit()
        {
        }

        public void CancelEdit()
        {
        }

        public void EndEdit()
        {
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }
    }
}
