using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq; // for OfType
using System.ComponentModel; // for INotifyPropertyChanged
using System.Collections.Specialized; // for NotifyCollectionChangedEventArgs
using System.Windows.Media.Imaging;
using System.IO;

namespace ScheduleApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new ScheduleApp.ViewModels.MainViewModel();

            // Auto-save hookup: subscribe to setup collection / property notifications.
            var vm = DataContext as ScheduleApp.ViewModels.MainViewModel;
            if (vm != null && vm.Setup != null)
            {
                // Top-level setup property changes (SchoolName, SaveFolder, etc.)
                vm.Setup.PropertyChanged += Setup_PropertyChanged;

                // Collections: additions/removals/moves
                vm.Setup.Teachers.CollectionChanged += SetupCollectionChanged;
                vm.Setup.Supports.CollectionChanged += SetupCollectionChanged;
                vm.Setup.Preferences.CollectionChanged += SetupCollectionChanged;

                // If items implement INotifyPropertyChanged, listen for item-level changes
                foreach (var it in vm.Setup.Teachers.OfType<INotifyPropertyChanged>())
                    it.PropertyChanged += Item_PropertyChanged;
                foreach (var it in vm.Setup.Supports.OfType<INotifyPropertyChanged>())
                    it.PropertyChanged += Item_PropertyChanged;
                foreach (var it in vm.Setup.Preferences.OfType<INotifyPropertyChanged>())
                    it.PropertyChanged += Item_PropertyChanged;
            }

            // Make the schedule title image's edge color transparent at runtime.
            // This will replace the Image.Source with a processed WriteableBitmap.
            MakeScheduleTitleImageEdgeTransparent();
        }

        // Ensure popup and toggle stay in sync: when popup closes (outside click or StaysOpen=false),
        // untoggle the hamburger button.
        private void HamburgerPopup_Closed(object sender, System.EventArgs e)
        {
            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;
        }

        // Navigate to the Schedule tab (top-level index 0) and close popup.
        private void MenuSchedule_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl != null)
                MainTabControl.SelectedIndex = 0; // Schedule

            // Also select the top-level Schedule inner tab "By Support"
            if (ScheduleViewInnerTabControl != null)
                ScheduleViewInnerTabControl.SelectedIndex = 0; // By Support

            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;
        }

        // Navigate to the Print Preview inner tab (inside Schedule) and close popup.
        private void MenuPreview_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl != null)
                MainTabControl.SelectedIndex = 0; // Schedule

            if (ScheduleViewInnerTabControl != null)
                ScheduleViewInnerTabControl.SelectedIndex = 2; // Print Preview

            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;
        }

        // New: button on Schedule page to show the Print Preview inner tab
        private void SchedulePreview_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl != null)
                MainTabControl.SelectedIndex = 0; // ensure top-level Schedule selected

            if (ScheduleViewInnerTabControl != null)
                ScheduleViewInnerTabControl.SelectedIndex = 2; // Print Preview
        }

        // Renamed button now opens Team Lineup (top-level tab index 1)
        private void MenuTeachers_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl != null)
                MainTabControl.SelectedIndex = 1; // Team Lineup

            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;
        }

        // Navigate to Preferences top-level tab (was School Details)
        private void MenuPreferences_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl != null)
                MainTabControl.SelectedIndex = 2; // Preferences (top-level)

            // close hamburger
            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;
        }

        // Algorithm placeholder: close popup and leave developer to wire actual behavior.
        private void MenuAlgorithm_Click(object sender, RoutedEventArgs e)
        {
            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;
            // TODO: wire actual algorithm dialog or action
        }

        // New: Exit application
        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;

            // Gracefully shut down the application
            Application.Current.Shutdown();
        }

        // Open the About (formerly Help) window with reduced content.
        private void AboutMenu_Click(object sender, RoutedEventArgs e)
        {
            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;

            var about = new AboutWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            about.ShowDialog();
        }

        // Title-bar mouse handling: drag or double-click to toggle maximize/restore
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximizeRestore();
            }
            else
            {
                try
                {
                    DragMove();
                }
                catch
                {
                    // ignore if DragMove fails (e.g. during maximized state transitions)
                }
            }
        }

        private void ToggleMaximizeRestore()
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;

            UpdateMaximizeIcon();
        }

        private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximizeRestore();
        }

        private void UpdateMaximizeIcon()
        {
            if (MaximizeButton == null) return;

            MaximizeButton.ApplyTemplate();
            // fully qualify Path control type to avoid ambiguity with System.IO.Path
            var icon = MaximizeButton.Template.FindName("PART_Icon", MaximizeButton) as System.Windows.Shapes.Path;
            if (icon == null) return;

            var geomKey = WindowState == WindowState.Maximized ? "RestoreGeometry" : "MaximizeGeometry";
            if (TryFindResource(geomKey) is Geometry g)
                icon.Data = g;
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Prevent starting a drag when maximized.
        private void ResizeThumb_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                e.Handled = true;
                return;
            }
        }

        private void ResizeThumb_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Prevent starting a drag when maximized (same behavior as existing ResizeThumb_PreviewMouseDown)
            if (WindowState == WindowState.Maximized)
            {
                e.Handled = true;
                return;
            }
        }

        // Edge handlers
        private void ThumbLeft_DragDelta(object sender, DragDeltaEventArgs e) => ResizeFrom("Left", e.HorizontalChange, e.VerticalChange);
        private void ThumbRight_DragDelta(object sender, DragDeltaEventArgs e) => ResizeFrom("Right", e.HorizontalChange, e.VerticalChange);
        private void ThumbTop_DragDelta(object sender, DragDeltaEventArgs e) => ResizeFrom("Top", e.HorizontalChange, e.VerticalChange);
        private void ThumbBottom_DragDelta(object sender, DragDeltaEventArgs e) => ResizeFrom("Bottom", e.HorizontalChange, e.VerticalChange);

        // Corner handlers
        private void ThumbTopLeft_DragDelta(object sender, DragDeltaEventArgs e) => ResizeFrom("TopLeft", e.HorizontalChange, e.VerticalChange);
        private void ThumbTopRight_DragDelta(object sender, DragDeltaEventArgs e) => ResizeFrom("TopRight", e.HorizontalChange, e.VerticalChange);
        private void ThumbBottomLeft_DragDelta(object sender, DragDeltaEventArgs e) => ResizeFrom("BottomLeft", e.HorizontalChange, e.VerticalChange);
        private void ThumbBottomRight_DragDelta(object sender, DragDeltaEventArgs e) => ResizeFrom("BottomRight", e.HorizontalChange, e.VerticalChange);

        // Shared helper that applies the resize. Deltas are in device-independent units (DIPs).
        private void ResizeFrom(string direction, double horizChange, double vertChange)
        {
            try
            {
                if (WindowState == WindowState.Maximized) return;

                double curWidth = Math.Max(0.0, this.ActualWidth);
                double curHeight = Math.Max(0.0, this.ActualHeight);

                double newLeft = this.Left;
                double newTop = this.Top;
                double newWidth = curWidth;
                double newHeight = curHeight;

                double minW = (this.MinWidth > 0.0) ? this.MinWidth : 100.0;
                double minH = (this.MinHeight > 0.0) ? this.MinHeight : 100.0;

                switch (direction)
                {
                    case "Left":
                        newWidth = Math.Max(minW, curWidth - horizChange);
                        newLeft = this.Left + (curWidth - newWidth);
                        break;

                    case "Right":
                        newWidth = Math.Max(minW, curWidth + horizChange);
                        break;

                    case "Top":
                        newHeight = Math.Max(minH, curHeight - vertChange);
                        newTop = this.Top + (curHeight - newHeight);
                        break;

                    case "Bottom":
                        newHeight = Math.Max(minH, curHeight + vertChange);
                        break;

                    case "TopLeft":
                        newWidth = Math.Max(minW, curWidth - horizChange);
                        newLeft = this.Left + (curWidth - newWidth);
                        newHeight = Math.Max(minH, curHeight - vertChange);
                        newTop = this.Top + (curHeight - newHeight);
                        break;

                    case "TopRight":
                        newWidth = Math.Max(minW, curWidth + horizChange);
                        newHeight = Math.Max(minH, curHeight - vertChange);
                        newTop = this.Top + (curHeight - newHeight);
                        break;

                    case "BottomLeft":
                        newWidth = Math.Max(minW, curWidth - horizChange);
                        newLeft = this.Left + (curWidth - newWidth);
                        newHeight = Math.Max(minH, curHeight + vertChange);
                        newTop = this.Top + (curHeight - newHeight);
                        break;

                    case "BottomRight":
                        newWidth = Math.Max(minW, curWidth + horizChange);
                        newHeight = Math.Max(minH, curHeight + vertChange);
                        break;

                    default:
                        return;
                }

                if (newWidth >= minW)
                {
                    this.Width = newWidth;
                    this.Left = newLeft;
                }

                if (newHeight >= minH)
                {
                    this.Height = newHeight;
                    this.Top = newTop;
                }
            }
            catch
            {
                // Keep UI stable if anything unexpected happens.
            }
        }

        // Load from compiled WPF resources (Resource build action) and make edge color transparent.
        private void MakeScheduleTitleImageEdgeTransparent()
        {
            try
            {
                // If none of the targets exist, nothing to do
                if (ScheduleTitleImage == null && TitleBarIcon == null && TeamLineupTitleImage == null && PreferencesTitleImage == null)
                    return;

                // Resource key observed earlier is lowercase 'washingtonstate.png'
                var packUri = new Uri("/ScheduleApp;component/washingtonstate.png", UriKind.Relative);
                var resourceInfo = Application.GetResourceStream(packUri);
                if (resourceInfo == null)
                {
                    // fallback: try pack://application:,,, form
                    resourceInfo = Application.GetResourceStream(new Uri("pack://application:,,,/washingtonstate.png", UriKind.Absolute));
                    if (resourceInfo == null) return;
                }

                using (var stream = resourceInfo.Stream)
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    bmp.StreamSource = stream;
                    bmp.EndInit();
                    bmp.Freeze();

                    if (bmp.PixelWidth == 0 || bmp.PixelHeight == 0) return;

                    var conv = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
                    int width = conv.PixelWidth;
                    int height = conv.PixelHeight;
                    var pf = PixelFormats.Bgra32;
                    int stride = (width * pf.BitsPerPixel + 7) / 8;
                    byte[] pixels = new byte[stride * height];
                    conv.CopyPixels(pixels, stride, 0);

                    // Sample border pixels (top, bottom, left, right) and pick the most frequent color.
                    var counts = new System.Collections.Generic.Dictionary<int, int>();
                    void CountPixel(int x, int y)
                    {
                        int i = (y * stride) + (x * 4);
                        if (i < 0 || i + 2 >= pixels.Length) return;
                        int b = pixels[i + 0];
                        int g = pixels[i + 1];
                        int r = pixels[i + 2];
                        int key = (r << 16) | (g << 8) | b;
                        if (counts.ContainsKey(key)) counts[key]++; else counts[key] = 1;
                    }

                    // top & bottom rows
                    for (int x = 0; x < width; x++)
                    {
                        CountPixel(x, 0);
                        CountPixel(x, Math.Max(0, height - 1));
                    }

                    // left & right columns (avoid double-sampling corners excessively)
                    for (int y = 1; y < height - 1; y++)
                    {
                        CountPixel(0, y);
                        CountPixel(Math.Max(0, width - 1), y);
                    }

                    if (counts.Count == 0) return;

                    // choose most common border color
                    int mostCommonKey = 0;
                    int bestCount = 0;
                    foreach (var kv in counts)
                    {
                        if (kv.Value > bestCount)
                        {
                            bestCount = kv.Value;
                            mostCommonKey = kv.Key;
                        }
                    }

                    byte edgeR = (byte)((mostCommonKey >> 16) & 0xFF);
                    byte edgeG = (byte)((mostCommonKey >> 8) & 0xFF);
                    byte edgeB = (byte)(mostCommonKey & 0xFF);

                    // Tolerance to allow near-matching colors (adjust if needed)
                    const int tol = 12;

                    // Apply alpha=0 to pixels matching the sampled border color within tolerance
                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        int db = Math.Abs(pixels[i + 0] - edgeB);
                        int dg = Math.Abs(pixels[i + 1] - edgeG);
                        int dr = Math.Abs(pixels[i + 2] - edgeR);

                        if (db <= tol && dg <= tol && dr <= tol)
                        {
                            pixels[i + 3] = 0; // transparent
                        }
                    }

                    var wb = new WriteableBitmap(width, height, conv.DpiX, conv.DpiY, pf, null);
                    wb.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
                    wb.Freeze();

                    // Assign processed image to all places that exist
                    if (ScheduleTitleImage != null)
                        ScheduleTitleImage.Source = wb;

                    if (TitleBarIcon != null)
                        TitleBarIcon.Source = wb;

                    if (TeamLineupTitleImage != null)
                        TeamLineupTitleImage.Source = wb;

                    if (PreferencesTitleImage != null)
                        PreferencesTitleImage.Source = wb;
                }
            }
            catch
            {
                // keep original images if anything goes wrong
            }
        }

        // Existing wiring methods (Setup_PropertyChanged, SetupCollectionChanged, Item_PropertyChanged, etc.)
        // ... (leave your existing methods unchanged) ...
    }
}
