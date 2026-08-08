using System;

namespace Playnite.Native;

[Serializable]
public struct POINT(int x, int y)
{
	private int _x = x;

	private int _y = y;

	public int X
	{
		get
		{
			return _x;
		}
		set
		{
			_x = value;
		}
	}

	public int Y
	{
		get
		{
			return _y;
		}
		set
		{
			_y = value;
		}
	}

	public override bool Equals(object obj)
	{
		if (obj is POINT pOINT)
		{
			if (pOINT._x == _x)
			{
				return pOINT._y == _y;
			}
			return false;
		}
		return base.Equals(obj);
	}

	public override int GetHashCode()
	{
		return _x.GetHashCode() ^ _y.GetHashCode();
	}

	public static bool operator ==(POINT a, POINT b)
	{
		if (a._x == b._x)
		{
			return a._y == b._y;
		}
		return false;
	}

	public static bool operator !=(POINT a, POINT b)
	{
		return !(a == b);
	}
}
