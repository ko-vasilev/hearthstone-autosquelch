using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Exporting;
using Hearthstone_Deck_Tracker.Plugins;
using Hearthstone_Deck_Tracker.Utility;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace Autosquelch
{
    public class AutosquelchPlugin : IPlugin
    {
        public string Author
        {
            get
            {
                return "Vasilev Konstantin";
            }
        }

        public string ButtonText
        {
            get
            {
                return "";
            }
        }

        public string Description
        {
            get
            {
                return "When enabled, plugin automatically squelches the opponent at the start of the game.";
            }
        }

        public System.Windows.Controls.MenuItem MenuItem
        {
            get
            {
                return null;
            }
        }

        public string Name
        {
            get
            {
                return "Autosquelch";
            }
        }

        public Version Version
        {
            get
            {
                return new Version(0, 1);
            }
        }

        public void OnButtonPress()
        {
        }

        private bool Squelched { get; set; }

        private bool PluginRunning { get; set; }

        private bool GameInProgress
        {
            get
            {
                return Hearthstone_Deck_Tracker.API.Core.Game != null && Hearthstone_Deck_Tracker.API.Core.Game.IsRunning;
            }
        }

        private bool OpponentIsSquelchable
        {
            get
            {
                return Hearthstone_Deck_Tracker.API.Core.Game.CurrentGameMode != GameMode.Practice
                        && Hearthstone_Deck_Tracker.API.Core.Game.CurrentGameMode != GameMode.None;
            }
        }

        public void OnLoad()
        {
            Squelched = false;
            PluginRunning = true;

            GameEvents.OnGameStart.Add(() =>
            {
                Squelched = false;
            });
            GameEvents.OnTurnStart.Add(activePlayer =>
            {
                if (!Squelched)
                {
                    if (!User32.IsHearthstoneInForeground())
                    {
                        return;
                    }

                    if (!OpponentIsSquelchable)
                    {
                        return;
                    }

                    Squelched = true;
                    Task t = Squelch();
                }
            });
        }

        public void OnUnload()
        {
            PluginRunning = false;
        }

        public void OnUpdate()
        {
        }

        public async Task Squelch()
        {
            if (!User32.IsHearthstoneInForeground())
            {
                Squelched = false;
                return;
            }

			var FirstMousePos = Cursor.Position;
            IntPtr hearthstoneWindow = User32.GetHearthstoneWindow();
            var HsRect = User32.GetHearthstoneRect(false);
            var Ratio = (4.0 / 3.0) / ((double)HsRect.Width / HsRect.Height);
            Point opponentHeroPosition = new Point((int)Helper.GetScaledXPos(0.5, HsRect.Width, Ratio), (int)(0.17 * HsRect.Height));
            Point squelchBubblePosition = new Point((int)Helper.GetScaledXPos(0.4, HsRect.Width, Ratio), (int)(0.1 * HsRect.Height));
            // setting this as a "width" value relative to height, maybe not best solution?
            const double xScale = 0.051; // 55px @ height = 1080
            const double yScale = 0.025; // 27px @ height = 1080
            const double minBrightness = 0.67;

            var lockWidth = (int)Math.Round(HsRect.Height * xScale);
            var lockHeight = (int)Math.Round(HsRect.Height * yScale);
            bool squelchBubbleVisible = false;
            do
            {
                if (!PluginRunning || !GameInProgress)
                {
                    Squelched = false;
                    return;
                }

                await MouseHelpers.ClickOnPoint(hearthstoneWindow, opponentHeroPosition, false);

				await Task.Delay(TimeSpan.FromSeconds(0.3));
				var capture = await ScreenCapture.CaptureHearthstoneAsync(squelchBubblePosition, lockWidth, lockHeight, hearthstoneWindow);
                squelchBubbleVisible = HueAndBrightness.GetAverage(capture).Brightness > minBrightness;
                if (!squelchBubbleVisible)
                    await Task.Delay(TimeSpan.FromSeconds(0.5));
            } while (!squelchBubbleVisible);

            await MouseHelpers.ClickOnPoint(hearthstoneWindow, squelchBubblePosition, true);
			await Task.Delay(TimeSpan.FromSeconds(0.1));
			await MouseHelpers.MoveCursor(FirstMousePos);

		}
    }
}
