using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Plugins;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.HotKeys;
using Hearthstone_Deck_Tracker.Utility.Logging;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows.Controls;

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
                return @"When enabled, plugin automatically squelches the opponent at the start of the game.
To temporarily turn off the autosquelch, press Ctrl+Alt+D";
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
                return new Version(0, 2);
            }
        }

        public void OnButtonPress()
        {
        }

        private static bool Squelched { get; set; }

        private bool PluginRunning { get; set; }

        private bool AutosquelchDisabled { get; set; }

        private bool ShouldTrySquelch => PluginRunning && GameInProgress && !AutosquelchDisabled;

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
                        && Hearthstone_Deck_Tracker.API.Core.Game.CurrentGameMode != GameMode.None 
                        && Hearthstone_Deck_Tracker.API.Core.Game.CurrentGameMode != GameMode.Battlegrounds;
            }
        }

        public void OnLoad()
        {
            PluginRunning = true;

            HotKeyManager.RegisterHotkey(DefaultHotKey, ToggleAutosquelch, "Toggle Autosquelch");

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
            HotKeyManager.RemovePredefinedHotkey(DefaultHotKey);
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

            IntPtr hearthstoneWindow = User32.GetHearthstoneWindow();
            if (hearthstoneWindow == IntPtr.Zero)
            {
                return;
            }

            var HsRect = User32.GetHearthstoneRect(true);
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
            // Limit amount of tries (in case a game mode does not support squelching your opponent or else)
            const int maxTries = 4;
            int timesTried = 0;
            var previousMousePosition = User32.GetMousePos();
            do
            {
                if (!ShouldTrySquelch)
                {
                    Squelched = false;
                    return;
                }

                await MouseHelpers.ClickOnPoint(hearthstoneWindow, opponentHeroPosition, false);

                await Task.Delay(TimeSpan.FromMilliseconds(Config.Instance.OverlayMouseOverTriggerDelay));
                var capture = await ScreenCapture.CaptureHearthstoneAsync(squelchBubblePosition, lockWidth, lockHeight, hearthstoneWindow);
                squelchBubbleVisible = CalculateAverageLightness(capture) > minBrightness;
                if (!squelchBubbleVisible)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(Config.Instance.OverlayMouseOverTriggerDelay));
                    ++timesTried;
                }
            } while (!squelchBubbleVisible && timesTried <= maxTries);

            await MouseHelpers.ClickOnPoint(hearthstoneWindow, squelchBubblePosition, true);
            MouseHelpers.SetCursorPosition(previousMousePosition);
        }

        private readonly HotKey DefaultHotKey = new HotKey(ModifierKeys.Control | ModifierKeys.Alt, System.Windows.Forms.Keys.D);

        private void ToggleAutosquelch()
        {
            AutosquelchDisabled = !AutosquelchDisabled;

            // Notify that plugin is active/inactive
            var textBlock = new HearthstoneTextBlock();
            textBlock.FontSize = 14;
            textBlock.Text = "Autosquelch is now " + (AutosquelchDisabled ? "disabled" : "enabled");
            textBlock.Loaded += SetHorizontalPosition;
            Canvas.SetBottom(textBlock, 50);
            var overlay = Hearthstone_Deck_Tracker.API.Core.OverlayCanvas;
            textBlock.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            overlay.Children.Add(textBlock);
            Hearthstone_Deck_Tracker.API.Core.OverlayWindow.Update(false);

            const double notificationDurationSeconds = 1.5;
            Task.Delay(TimeSpan.FromSeconds(notificationDurationSeconds)).ContinueWith(_ =>
            {
                Hearthstone_Deck_Tracker.API.Core.OverlayCanvas.Children.Remove(textBlock);
                Hearthstone_Deck_Tracker.API.Core.OverlayWindow.Update(false);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private static void SetHorizontalPosition(object s, System.Windows.RoutedEventArgs e)
        {
            var sender = (s as HearthstoneTextBlock);
            var textBlockWidth = sender.ActualWidth;
            var canvasWidth = Hearthstone_Deck_Tracker.API.Core.OverlayCanvas.ActualWidth;
            Canvas.SetLeft(sender, (canvasWidth - textBlockWidth) / 2.0);
        }

        /// <summary>
        /// Calculate average brightness of the bitmap.
        /// </summary>
        /// <param name="bitmap">Bitmap to be processed.</param>
        /// <returns>Brightness from the range 0-1.</returns>
        internal static double CalculateAverageLightness(Bitmap bitmap)
        {
            double lum = 0;
            var width = bitmap.Width;
            var height = bitmap.Height;
            using (var tmpBmp = new Bitmap(bitmap))
            {
                var bppModifier = bitmap.PixelFormat == PixelFormat.Format24bppRgb ? 3 : 4;

                var srcData = tmpBmp.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                var stride = srcData.Stride;
                var scan0 = srcData.Scan0;

                //Luminance (standard, objective): (0.2126*R) + (0.7152*G) + (0.0722*B)
                //Luminance (perceived option 1): (0.299*R + 0.587*G + 0.114*B)
                //Luminance (perceived option 2, slower to calculate): sqrt( 0.241*R^2 + 0.691*G^2 + 0.068*B^2 )

                unsafe
                {
                    byte* p = (byte*)(void*)scan0;

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int idx = (y * stride) + x * bppModifier;
                            lum += (0.299 * p[idx + 2] + 0.587 * p[idx + 1] + 0.114 * p[idx]);
                        }
                    }
                }

                tmpBmp.UnlockBits(srcData);
            }
            var avgLum = lum / (width * height);
            return avgLum / 255.0;
        }
    }
}
