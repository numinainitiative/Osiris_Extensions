using System;

namespace Playnite.Native;

public static class Winuser
{
	public const int GWL_STYLE = -16;

	public const int WS_SYSMENU = 524288;

	public const int WM_HOTKEY = 786;

	public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

	public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

	public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

	public static readonly IntPtr HWND_TOP = new IntPtr(0);

	public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

	public const uint WM_QUERYENDSESSION = 17u;

	public const uint WM_ENDSESSION = 22u;

	public const uint ENDSESSION_CLOSEAPP = 1u;

	public const uint WM_MOUSEACTIVATE = 33u;

	public const uint WM_MOUSEMOVE = 512u;

	public const uint WM_LBUTTONDOWN = 513u;

	public const uint WM_LBUTTONUP = 514u;

	public const uint WM_LBUTTONDBLCLK = 515u;

	public const uint WM_RBUTTONDOWN = 516u;

	public const uint WM_RBUTTONUP = 517u;

	public const uint WM_RBUTTONDBLCLK = 518u;

	public const uint WM_MBUTTONDOWN = 519u;

	public const uint WM_MBUTTONUP = 520u;

	public const uint WM_MBUTTONDBLCLK = 521u;

	public const uint MK_CONTROL = 8u;

	public const uint MK_LBUTTON = 1u;

	public const uint MK_MBUTTON = 16u;

	public const uint MK_RBUTTON = 2u;

	public const uint MK_SHIFT = 4u;

	public const uint MOD_NONE = 0u;

	public const uint MOD_ALT = 1u;

	public const uint MOD_CONTROL = 2u;

	public const uint MOD_SHIFT = 4u;

	public const uint MOD_WIN = 8u;

	public const uint WM_KEYDOWN = 256u;

	public const uint WM_KEYUP = 257u;

	public const uint WM_SYSKEYDOWN = 260u;

	public const uint WM_SYSKEYUP = 261u;

	public const uint VK_LBUTTON = 1u;

	public const uint VK_RBUTTON = 2u;

	public const uint VK_CANCEL = 3u;

	public const uint VK_MBUTTON = 4u;

	public const uint VK_XBUTTON1 = 5u;

	public const uint VK_XBUTTON2 = 6u;

	public const uint VK_0x07 = 7u;

	public const uint VK_BACK = 8u;

	public const uint VK_TAB = 9u;

	public const uint VK_0x0A = 10u;

	public const uint VK_0x0B = 11u;

	public const uint VK_CLEAR = 12u;

	public const uint VK_RETURN = 13u;

	public const uint VK_0x0E = 14u;

	public const uint VK_0x0F = 15u;

	public const uint VK_SHIFT = 16u;

	public const uint VK_CONTROL = 17u;

	public const uint VK_MENU = 18u;

	public const uint VK_PAUSE = 19u;

	public const uint VK_CAPITAL = 20u;

	public const uint VK_KANA = 21u;

	public const uint VK_HANGUEL = 21u;

	public const uint VK_HANGUL = 21u;

	public const uint VK_0x16 = 22u;

	public const uint VK_JUNJA = 23u;

	public const uint VK_FINAL = 24u;

	public const uint VK_HANJA = 25u;

	public const uint VK_KANJI = 25u;

	public const uint VK_0x1A = 26u;

	public const uint VK_ESCAPE = 27u;

	public const uint VK_CONVERT = 28u;

	public const uint VK_NONCONVERT = 29u;

	public const uint VK_ACCEPT = 30u;

	public const uint VK_MODECHANGE = 31u;

	public const uint VK_SPACE = 32u;

	public const uint VK_PRIOR = 33u;

	public const uint VK_NEXT = 34u;

	public const uint VK_END = 35u;

	public const uint VK_HOME = 36u;

	public const uint VK_LEFT = 37u;

	public const uint VK_UP = 38u;

	public const uint VK_RIGHT = 39u;

	public const uint VK_DOWN = 40u;

	public const uint VK_SELECT = 41u;

	public const uint VK_PRINT = 42u;

	public const uint VK_EXECUTE = 43u;

	public const uint VK_SNAPSHOT = 44u;

	public const uint VK_INSERT = 45u;

	public const uint VK_DELETE = 46u;

	public const uint VK_HELP = 47u;

	public const uint VK_0x30 = 48u;

	public const uint VK_0x31 = 49u;

	public const uint VK_0x32 = 50u;

	public const uint VK_0x33 = 51u;

	public const uint VK_0x34 = 52u;

	public const uint VK_0x35 = 53u;

	public const uint VK_0x36 = 54u;

	public const uint VK_0x37 = 55u;

	public const uint VK_0x38 = 56u;

	public const uint VK_0x39 = 57u;

	public const uint VK_0x3A = 58u;

	public const uint VK_0x3B = 59u;

	public const uint VK_0x3C = 60u;

	public const uint VK_0x3D = 61u;

	public const uint VK_0x3E = 62u;

	public const uint VK_0x3F = 63u;

	public const uint VK_0x40 = 64u;

	public const uint VK_0x41 = 65u;

	public const uint VK_0x42 = 66u;

	public const uint VK_0x43 = 67u;

	public const uint VK_0x44 = 68u;

	public const uint VK_0x45 = 69u;

	public const uint VK_0x46 = 70u;

	public const uint VK_0x47 = 71u;

	public const uint VK_0x48 = 72u;

	public const uint VK_0x49 = 73u;

	public const uint VK_0x4A = 74u;

	public const uint VK_0x4B = 75u;

	public const uint VK_0x4C = 76u;

	public const uint VK_0x4D = 77u;

	public const uint VK_0x4E = 78u;

	public const uint VK_0x4F = 79u;

	public const uint VK_0x50 = 80u;

	public const uint VK_0x51 = 81u;

	public const uint VK_0x52 = 82u;

	public const uint VK_0x53 = 83u;

	public const uint VK_0x54 = 84u;

	public const uint VK_0x55 = 85u;

	public const uint VK_0x56 = 86u;

	public const uint VK_0x57 = 87u;

	public const uint VK_0x58 = 88u;

	public const uint VK_0x59 = 89u;

	public const uint VK_0x5A = 90u;

	public const uint VK_LWIN = 91u;

	public const uint VK_RWIN = 92u;

	public const uint VK_APPS = 93u;

	public const uint VK_0x5E = 94u;

	public const uint VK_SLEEP = 95u;

	public const uint VK_NUMPAD0 = 96u;

	public const uint VK_NUMPAD1 = 97u;

	public const uint VK_NUMPAD2 = 98u;

	public const uint VK_NUMPAD3 = 99u;

	public const uint VK_NUMPAD4 = 100u;

	public const uint VK_NUMPAD5 = 101u;

	public const uint VK_NUMPAD6 = 102u;

	public const uint VK_NUMPAD7 = 103u;

	public const uint VK_NUMPAD8 = 104u;

	public const uint VK_NUMPAD9 = 105u;

	public const uint VK_MULTIPLY = 106u;

	public const uint VK_ADD = 107u;

	public const uint VK_SEPARATOR = 108u;

	public const uint VK_SUBTRACT = 109u;

	public const uint VK_DECIMAL = 110u;

	public const uint VK_DIVIDE = 111u;

	public const uint VK_F1 = 112u;

	public const uint VK_F2 = 113u;

	public const uint VK_F3 = 114u;

	public const uint VK_F4 = 115u;

	public const uint VK_F5 = 116u;

	public const uint VK_F6 = 117u;

	public const uint VK_F7 = 118u;

	public const uint VK_F8 = 119u;

	public const uint VK_F9 = 120u;

	public const uint VK_F10 = 121u;

	public const uint VK_F11 = 122u;

	public const uint VK_F12 = 123u;

	public const uint VK_F13 = 124u;

	public const uint VK_F14 = 125u;

	public const uint VK_F15 = 126u;

	public const uint VK_F16 = 127u;

	public const uint VK_F17 = 128u;

	public const uint VK_F18 = 129u;

	public const uint VK_F19 = 130u;

	public const uint VK_F20 = 131u;

	public const uint VK_F21 = 132u;

	public const uint VK_F22 = 133u;

	public const uint VK_F23 = 134u;

	public const uint VK_F24 = 135u;

	public const uint VK_0x88 = 136u;

	public const uint VK_0x89 = 137u;

	public const uint VK_0x8A = 138u;

	public const uint VK_0x8B = 139u;

	public const uint VK_0x8C = 140u;

	public const uint VK_0x8D = 141u;

	public const uint VK_0x8E = 142u;

	public const uint VK_0x8F = 143u;

	public const uint VK_NUMLOCK = 144u;

	public const uint VK_SCROLL = 145u;

	public const uint VK_0x92 = 146u;

	public const uint VK_0x93 = 147u;

	public const uint VK_0x94 = 148u;

	public const uint VK_0x95 = 149u;

	public const uint VK_0x96 = 150u;

	public const uint VK_0x97 = 151u;

	public const uint VK_0x98 = 152u;

	public const uint VK_0x99 = 153u;

	public const uint VK_0x9A = 154u;

	public const uint VK_0x9B = 155u;

	public const uint VK_0x9C = 156u;

	public const uint VK_0x9D = 157u;

	public const uint VK_0x9E = 158u;

	public const uint VK_0x9F = 159u;

	public const uint VK_LSHIFT = 160u;

	public const uint VK_RSHIFT = 161u;

	public const uint VK_LCONTROL = 162u;

	public const uint VK_RCONTROL = 163u;

	public const uint VK_LMENU = 164u;

	public const uint VK_RMENU = 165u;

	public const uint VK_BROWSER_BACK = 166u;

	public const uint VK_BROWSER_FORWARD = 167u;

	public const uint VK_BROWSER_REFRESH = 168u;

	public const uint VK_BROWSER_STOP = 169u;

	public const uint VK_BROWSER_SEARCH = 170u;

	public const uint VK_BROWSER_FAVORITES = 171u;

	public const uint VK_BROWSER_HOME = 172u;

	public const uint VK_VOLUME_MUTE = 173u;

	public const uint VK_VOLUME_DOWN = 174u;

	public const uint VK_VOLUME_UP = 175u;

	public const uint VK_MEDIA_NEXT_TRACK = 176u;

	public const uint VK_MEDIA_PREV_TRACK = 177u;

	public const uint VK_MEDIA_STOP = 178u;

	public const uint VK_MEDIA_PLAY_PAUSE = 179u;

	public const uint VK_LAUNCH_MAIL = 180u;

	public const uint VK_LAUNCH_MEDIA_SELECT = 181u;

	public const uint VK_LAUNCH_APP1 = 182u;

	public const uint VK_LAUNCH_APP2 = 183u;

	public const uint VK_0xB8 = 184u;

	public const uint VK_0xB9 = 185u;

	public const uint VK_OEM_1 = 186u;

	public const uint VK_OEM_PLUS = 187u;

	public const uint VK_OEM_COMMA = 188u;

	public const uint VK_OEM_MINUS = 189u;

	public const uint VK_OEM_PERIOD = 190u;

	public const uint VK_OEM_2 = 191u;

	public const uint VK_OEM_3 = 192u;

	public const uint VK_0xC1 = 193u;

	public const uint VK_0xC2 = 194u;

	public const uint VK_0xC3 = 195u;

	public const uint VK_0xC4 = 196u;

	public const uint VK_0xC5 = 197u;

	public const uint VK_0xC6 = 198u;

	public const uint VK_0xC7 = 199u;

	public const uint VK_0xC8 = 200u;

	public const uint VK_0xC9 = 201u;

	public const uint VK_0xCA = 202u;

	public const uint VK_0xCB = 203u;

	public const uint VK_0xCC = 204u;

	public const uint VK_0xCD = 205u;

	public const uint VK_0xCE = 206u;

	public const uint VK_0xCF = 207u;

	public const uint VK_0xD0 = 208u;

	public const uint VK_0xD1 = 209u;

	public const uint VK_0xD2 = 210u;

	public const uint VK_0xD3 = 211u;

	public const uint VK_0xD4 = 212u;

	public const uint VK_0xD5 = 213u;

	public const uint VK_0xD6 = 214u;

	public const uint VK_0xD7 = 215u;

	public const uint VK_0xD8 = 216u;

	public const uint VK_0xD9 = 217u;

	public const uint VK_0xDA = 218u;

	public const uint VK_OEM_4 = 219u;

	public const uint VK_OEM_5 = 220u;

	public const uint VK_OEM_6 = 221u;

	public const uint VK_OEM_7 = 222u;

	public const uint VK_OEM_8 = 223u;

	public const uint VK_0xE0 = 224u;

	public const uint VK_0xE1 = 225u;

	public const uint VK_OEM_102 = 226u;

	public const uint VK_0xE3 = 227u;

	public const uint VK_0xE4 = 228u;

	public const uint VK_PROCESSKEY = 229u;

	public const uint VK_0xE6 = 230u;

	public const uint VK_PACKET = 231u;

	public const uint VK_0xE8 = 232u;

	public const uint VK_0xE9 = 233u;

	public const uint VK_0xEA = 234u;

	public const uint VK_0xEB = 235u;

	public const uint VK_0xEC = 236u;

	public const uint VK_0xED = 237u;

	public const uint VK_0xEE = 238u;

	public const uint VK_0xEF = 239u;

	public const uint VK_0xF0 = 240u;

	public const uint VK_0xF1 = 241u;

	public const uint VK_0xF2 = 242u;

	public const uint VK_0xF3 = 243u;

	public const uint VK_0xF4 = 244u;

	public const uint VK_0xF5 = 245u;

	public const uint VK_ATTN = 246u;

	public const uint VK_CRSEL = 247u;

	public const uint VK_EXSEL = 248u;

	public const uint VK_EREOF = 249u;

	public const uint VK_PLAY = 250u;

	public const uint VK_ZOOM = 251u;

	public const uint VK_NONAME = 252u;

	public const uint VK_PA1 = 253u;

	public const uint VK_OEM_CLEAR = 254u;
}
