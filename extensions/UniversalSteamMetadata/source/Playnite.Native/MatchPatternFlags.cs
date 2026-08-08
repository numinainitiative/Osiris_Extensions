using System;

namespace Playnite.Native;

[Flags]
public enum MatchPatternFlags : uint
{
	Normal = 0u,
	Multiple = 1u,
	DontStripSpaces = 0x10000u
}
