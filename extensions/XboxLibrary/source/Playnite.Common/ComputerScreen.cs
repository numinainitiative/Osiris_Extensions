using System.Drawing;
using System.Windows.Forms;

namespace Playnite.Common;

public class ComputerScreen
{
	public Rectangle WorkingArea { get; private set; }

	public bool Primary { get; private set; }

	public string DeviceName { get; private set; }

	public Rectangle Bounds { get; private set; }

	public int BitsPerPixel { get; private set; }

	public ComputerScreen()
	{
	}

	public ComputerScreen(Screen screen)
	{
		WorkingArea = screen.WorkingArea;
		Primary = screen.Primary;
		DeviceName = screen.DeviceFriendlyName();
		Bounds = screen.Bounds;
		BitsPerPixel = screen.BitsPerPixel;
	}
}
