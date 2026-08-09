using Playnite.SDK;
using Playnite.SDK.Events;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

namespace XboxLibrary.Services
{
    internal sealed class XboxAuthenticationWindow : IDisposable
    {
        private const double PreferredWidth = 1225;
        private const double PreferredHeight = 700;
        private const double HeaderHeight = 50;
        private const double AddressWidth = 775;

        private readonly IWebView webView;
        private readonly Window window;
        private readonly TextBox addressBox;

        public XboxAuthenticationWindow(IWebView webView)
        {
            this.webView = webView ?? throw new ArgumentNullException(nameof(webView));
            window = webView.WindowHost ?? throw new InvalidOperationException("The Xbox sign-in browser has no window host.");

            addressBox = BuildWindow();
            webView.LoadingChanged += WebView_LoadingChanged;
        }

        public void Navigate(string address)
        {
            addressBox.Text = address ?? string.Empty;
            webView.Navigate(address);
        }

        public bool? OpenDialog()
        {
            using (MainWindowDimmer.Show(window.Owner))
            {
                return webView.OpenDialog();
            }
        }

        public void Dispose()
        {
            webView.LoadingChanged -= WebView_LoadingChanged;
        }

        private TextBox BuildWindow()
        {
            var originalContent = window.Content as UIElement;
            if (originalContent == null)
            {
                throw new InvalidOperationException("The Xbox sign-in browser has no visual content.");
            }

            // The inherited Playnite browser places a second, unstyled address field
            // above the browser. The Osiris header below replaces it.
            var inheritedAddressBox = FindDescendant<TextBox>(originalContent);
            if (inheritedAddressBox != null)
            {
                inheritedAddressBox.Visibility = Visibility.Collapsed;
            }

            window.Content = null;
            window.Width = Math.Min(PreferredWidth, Math.Max(760, SystemParameters.WorkArea.Width - 80));
            window.Height = Math.Min(PreferredHeight, Math.Max(480, SystemParameters.WorkArea.Height - 80));
            window.MinWidth = 760;
            window.MinHeight = 480;
            window.SizeToContent = SizeToContent.Manual;
            window.WindowStartupLocation = window.Owner == null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner;
            window.WindowStyle = WindowStyle.None;
            window.ResizeMode = ResizeMode.CanResize;
            window.Background = Brush("#050505");
            window.BorderThickness = new Thickness(0);

            WindowChrome.SetWindowChrome(window, new WindowChrome
            {
                CaptionHeight = 0,
                CornerRadius = new CornerRadius(8),
                GlassFrameThickness = new Thickness(0),
                ResizeBorderThickness = new Thickness(6),
                UseAeroCaptionButtons = false
            });

            // Override Playnite's standard window template so its title bar and the
            // "Playnite" title suffix are not rendered around the Osiris browser.
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.ContentSourceProperty, "Content");
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch);
            window.Template = new ControlTemplate(window.GetType()) { VisualTree = presenter };

            var rootBorder = new Border
            {
                Background = Brush("#050505"),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(2),
                SnapsToDevicePixels = true
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(HeaderHeight) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootBorder.Child = root;

            var header = BuildHeader(out var osirisAddressBox);
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            Grid.SetRow(originalContent, 1);
            root.Children.Add(originalContent);

            // Chromium can visually cover a border drawn by its parent. Keep the
            // browser inset and draw this final outline above every child so the
            // frame remains continuous on all four edges.
            var framedRoot = new Grid
            {
                Background = Brush("#050505"),
                ClipToBounds = true,
                SnapsToDevicePixels = true
            };
            framedRoot.Children.Add(rootBorder);
            framedRoot.Children.Add(new Border
            {
                BorderBrush = Brush("#474747"),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0.5),
                IsHitTestVisible = false,
                SnapsToDevicePixels = true
            });

            window.Content = framedRoot;
            return osirisAddressBox;
        }

        private Grid BuildHeader(out TextBox osirisAddressBox)
        {
            var header = new Grid
            {
                Background = Brush("#050505"),
                Cursor = Cursors.Arrow
            };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(AddressWidth) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 138 });

            osirisAddressBox = new TextBox
            {
                IsReadOnly = true,
                IsReadOnlyCaretVisible = true,
                Height = 30,
                Margin = new Thickness(0, 10, 0, 10),
                Padding = new Thickness(12, 4, 12, 4),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Foreground = Brush("#B8BBC3"),
                Background = Brush("#0B0B0B"),
                BorderBrush = Brush("#242424"),
                BorderThickness = new Thickness(1),
                FontSize = 13,
                TextAlignment = TextAlignment.Center,
                Focusable = true,
                Cursor = Cursors.IBeam
            };
            Grid.SetColumn(osirisAddressBox, 1);
            header.Children.Add(osirisAddressBox);

            var controls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetColumn(controls, 2);
            header.Children.Add(controls);

            var minimize = CreateWindowButton(WindowControlIcon.Minimize, "Minimize");
            minimize.Click += (_, __) => window.WindowState = WindowState.Minimized;
            controls.Children.Add(minimize);

            var maximize = CreateWindowButton(WindowControlIcon.Maximize, "Maximize or restore");
            maximize.Click += (_, __) => ToggleMaximize();
            controls.Children.Add(maximize);

            var close = CreateWindowButton(WindowControlIcon.Close, "Close", true);
            close.Click += (_, __) => window.Close();
            controls.Children.Add(close);

            return header;
        }

        private Button CreateWindowButton(WindowControlIcon icon, string toolTip, bool closeButton = false)
        {
            var button = new Button
            {
                Width = 46,
                Height = HeaderHeight - 2,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = Brush("#C8CBD2"),
                Content = CreateLucideWindowIcon(icon),
                ToolTip = toolTip,
                Focusable = false
            };

            var border = new FrameworkElementFactory(typeof(Border), "ButtonBorder");
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);

            var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(
                Border.BackgroundProperty,
                Brush(closeButton ? "#C42B1C" : "#1C1C1C"),
                "ButtonBorder"));
            template.Triggers.Add(hover);
            button.Template = template;
            return button;
        }

        private static Viewbox CreateLucideWindowIcon(WindowControlIcon icon)
        {
            string pathData;
            switch (icon)
            {
                case WindowControlIcon.Minimize:
                    pathData = "M5 12 H19";
                    break;
                case WindowControlIcon.Maximize:
                    pathData = "M5 3 H19 A2 2 0 0 1 21 5 V19 A2 2 0 0 1 19 21 H5 A2 2 0 0 1 3 19 V5 A2 2 0 0 1 5 3 Z";
                    break;
                default:
                    pathData = "M18 6 L6 18 M6 6 L18 18";
                    break;
            }

            var canvas = new Canvas
            {
                Width = 24,
                Height = 24,
                IsHitTestVisible = false
            };
            canvas.Children.Add(new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(pathData),
                Stroke = Brush("#C8CBD2"),
                StrokeThickness = 2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = Brushes.Transparent
            });

            var iconSize = icon == WindowControlIcon.Minimize
                ? 20
                : icon == WindowControlIcon.Close ? 22 : 16;

            return new Viewbox
            {
                Width = iconSize,
                Height = iconSize,
                Stretch = Stretch.Uniform,
                Child = canvas,
                IsHitTestVisible = false
            };
        }

        private void ToggleMaximize()
        {
            window.WindowState = window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void WebView_LoadingChanged(object sender, WebViewLoadingChangedEventArgs e)
        {
            if (e.IsLoading)
            {
                return;
            }

            var address = webView.GetCurrentAddress();
            window.Dispatcher.BeginInvoke(new Action(() => addressBox.Text = address ?? string.Empty));
        }

        private static T FindDescendant<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            var childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (var index = 0; index < childCount; index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                if (child is T match)
                {
                    return match;
                }

                var nestedMatch = FindDescendant<T>(child);
                if (nestedMatch != null)
                {
                    return nestedMatch;
                }
            }

            return null;
        }

        private static SolidColorBrush Brush(string value)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        }

        private enum WindowControlIcon
        {
            Minimize,
            Maximize,
            Close
        }

        private sealed class MainWindowDimmer : Adorner, IDisposable
        {
            private readonly Border overlay;
            private readonly AdornerLayer layer;

            private MainWindowDimmer(UIElement adornedElement, AdornerLayer layer)
                : base(adornedElement)
            {
                this.layer = layer;
                overlay = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(185, 0, 0, 0)),
                    IsHitTestVisible = true
                };

                AddVisualChild(overlay);
                IsHitTestVisible = true;
            }

            protected override int VisualChildrenCount => 1;

            public static IDisposable Show(Window owner)
            {
                var target = owner?.Content as UIElement;
                if (target == null)
                {
                    return EmptyDisposable.Instance;
                }

                var adornerLayer = AdornerLayer.GetAdornerLayer(target);
                if (adornerLayer == null)
                {
                    return EmptyDisposable.Instance;
                }

                var dimmer = new MainWindowDimmer(target, adornerLayer);
                adornerLayer.Add(dimmer);
                adornerLayer.Update(target);
                return dimmer;
            }

            public void Dispose()
            {
                layer.Remove(this);
            }

            protected override Visual GetVisualChild(int index)
            {
                if (index != 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return overlay;
            }

            protected override Size MeasureOverride(Size constraint)
            {
                overlay.Measure(constraint);
                return constraint;
            }

            protected override Size ArrangeOverride(Size finalSize)
            {
                overlay.Arrange(new Rect(finalSize));
                return finalSize;
            }

            private sealed class EmptyDisposable : IDisposable
            {
                public static readonly EmptyDisposable Instance = new EmptyDisposable();

                public void Dispose()
                {
                }
            }
        }
    }
}
