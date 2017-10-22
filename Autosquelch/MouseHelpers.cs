using Hearthstone_Deck_Tracker;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Hearthstone_Deck_Tracker.User32;
using static Hearthstone_Deck_Tracker.User32.MouseEventFlags;

namespace Autosquelch
{
    public class MouseHelpers
    {
        public static async Task ClickOnPoint(IntPtr wndHandle, Point clientPoint, bool leftMouseButton)
        {
            ClientToScreen(wndHandle, ref clientPoint);

            Cursor.Position = new Point(clientPoint.X, clientPoint.Y);
            Hearthstone_Deck_Tracker.Utility.Logging.Log.Debug("Clicking " + Cursor.Position);

            if (SystemInformation.MouseButtonsSwapped)
            {
                leftMouseButton = !leftMouseButton;
            }

            //mouse down
            if (leftMouseButton)
                mouse_event((uint)LeftDown, 0, 0, 0, UIntPtr.Zero);
            else
                mouse_event((uint)RightDown, 0, 0, 0, UIntPtr.Zero);

            //mouse up
            if (leftMouseButton)
                mouse_event((uint)LeftUp, 0, 0, 0, UIntPtr.Zero);
            else
                mouse_event((uint)RightUp, 0, 0, 0, UIntPtr.Zero);

        }
		public static async Task MoveCursor(Point clientPoint)
		{
			Cursor.Position = new Point(clientPoint.X, clientPoint.Y);
		}
	}
}
