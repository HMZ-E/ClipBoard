using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Shapes;
using Forms = System.Windows.Forms;

namespace ClipBoardWindows
{
    public sealed class MainWindow : Window
    {
        readonly HistoryStore store;
        readonly ClipboardService service;
        readonly Grid root = new Grid();
        readonly Grid layout = new Grid();
        readonly Canvas backdrop = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
        readonly TranslateTransform glow1Move = new TranslateTransform();
        readonly TranslateTransform glow2Move = new TranslateTransform();
        readonly GradientStop glow1Color = new GradientStop();
        readonly GradientStop glow2Color = new GradientStop();
        readonly LinearGradientBrush moodBrush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        readonly TextBox search = new TextBox();
        readonly StackPanel pins = new StackPanel { Orientation = Orientation.Horizontal };
        readonly StackPanel history = new StackPanel();
        readonly StackPanel pinnedSection = new StackPanel();
        readonly ScrollViewer scroll = new ScrollViewer();
        readonly TextBlock footer = new TextBlock();
        readonly Button undo;
        readonly DispatcherTimer clock;
        readonly Dictionary<string, Border> cards = new Dictionary<string, Border>();
        readonly HashSet<string> expanded = new HashSet<string>();
        readonly Dictionary<string, TextBlock> times = new Dictionary<string, TextBlock>();
        readonly Dictionary<string, TextBlock> copyLabels = new Dictionary<string, TextBlock>();
        readonly HashSet<string> deleting = new HashSet<string>();
        Grid overlay;
        Clip preview;
        string selected;
        DateTime feedbackUntil;
        string feedback;
        bool closing, dialogOpen;
        bool hiding, drifting;
        int panelVersion;
        string palette, copiedId;
        DateTime copiedUntil;
        double scrollTarget;
        bool wheelAnimating;
        static readonly DependencyProperty AnimatedScrollOffsetProperty = DependencyProperty.Register("AnimatedScrollOffset", typeof(double), typeof(MainWindow), new PropertyMetadata(0.0, delegate(DependencyObject value, DependencyPropertyChangedEventArgs args) { ((MainWindow)value).scroll.ScrollToVerticalOffset((double)args.NewValue); }));
        internal bool BackdropRunning { get { return drifting; } }
        internal double CurrentScrollOffset { get { return scroll.VerticalOffset; } }
        internal MotionLevel MotionSetting { get { return Motion.Level(store.State.Settings.Motion); } }
        internal string SelectedId { get { return selected; } }
        internal Clip PreviewClip { get { return preview; } }
        internal FrameworkElement OverlayElement { get { return overlay; } }
        internal Border CardElement(string id) { return cards[id]; }
        Brush muted = Brush("#A7ABB8");
        public MainWindow(HistoryStore historyStore, ClipboardService clipboardService)
        {
            store = historyStore; service = clipboardService;
            Title = "ClipBoard"; Width = 410; Height = 650; MinWidth = 340;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; Topmost = true;
            FontFamily = new FontFamily("Segoe UI"); FontSize = 13; Foreground = Brushes.White;
            Resources[typeof(Button)] = ButtonStyle();
            Resources[typeof(ScrollBar)] = ScrollBarStyle();
            var frame = new Border { CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(1), BorderBrush = Brush("#454957"), Child = root, ClipToBounds = true };
            Content = frame;
            frame.SizeChanged += delegate { frame.Clip = new RectangleGeometry(new Rect(0, 0, frame.ActualWidth, frame.ActualHeight), 20, 20); };
            for (int i = 0; i < 3; i++) moodBrush.GradientStops.Add(new GradientStop(Colors.Transparent, i * .5));
            frame.Background = moodBrush; root.Children.Add(backdrop);
            AddGlow(glow1Color, glow1Move, -150, -100);
            AddGlow(glow2Color, glow2Move, 100, 160);
            for (int i = 0; i < 5; i++) layout.RowDefinitions.Add(new RowDefinition { Height = i == 3 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
            root.Children.Add(layout); layout.Margin = new Thickness(20, 18, 20, 12);
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 18) };
            var close = ActionButton("\uE8BB", "Hide ClipBoard (Esc)", HidePanel); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            var settings = ActionButton("\uE713", "Mood and settings", ShowSettings); DockPanel.SetDock(settings, Dock.Right); header.Children.Add(settings);
            var logo = new Image { Width = 185, Height = 39, HorizontalAlignment = HorizontalAlignment.Left, Stretch = Stretch.Uniform };
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ClipboardLogo.png")) {
                if (stream != null) { var bmp = new BitmapImage(); bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.StreamSource = stream; bmp.EndInit(); bmp.Freeze(); logo.Source = bmp; }
            }
            AutomationProperties.SetName(logo, "ClipBoard"); header.Children.Add(logo);
            header.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) {
                DependencyObject target = e.OriginalSource as DependencyObject;
                while (target != null && target != header) { if (target is Button) return; target = VisualTreeHelper.GetParent(target); }
                if (e.LeftButton == MouseButtonState.Pressed) { try { DragMove(); } catch (InvalidOperationException) {} }
            };
            Place(layout, header, 0);
            search.Background = Brushes.Transparent; search.BorderThickness = new Thickness(0); search.Foreground = Brushes.White; search.CaretBrush = Brushes.White;
            search.FontSize = 15; search.Padding = new Thickness(0, 9, 0, 9); search.VerticalContentAlignment = VerticalAlignment.Center;
            search.ToolTip = "Search text, links, code, colors, and images"; AutomationProperties.SetName(search, "Search clipboard history");
            var searchGrid = new Grid(); searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) }); searchGrid.ColumnDefinitions.Add(new ColumnDefinition());
            var magnify = Label("\uE721", 17, muted); magnify.FontFamily = new FontFamily("Segoe MDL2 Assets"); magnify.VerticalAlignment = VerticalAlignment.Center;
            searchGrid.Children.Add(magnify); Grid.SetColumn(search, 1); searchGrid.Children.Add(search);
            var hint = Label("Search…", 15, Brush("#7E8390")); hint.IsHitTestVisible = false; hint.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(hint, 1); searchGrid.Children.Add(hint);
            search.TextChanged += delegate { hint.Visibility = search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; Refresh(); };
            var searchFrame = new Border { Background = Brush("#7010141C"), BorderBrush = Brush("#454957"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 0, 12, 0), Child = searchGrid, Margin = new Thickness(0, 0, 0, 16) };
            Place(layout, searchFrame, 1);
            pins.Margin = new Thickness(0, 8, 0, 12);
            pinnedSection.Children.Add(Label("PINNED", 10, muted));
            pinnedSection.Children.Add(new ScrollViewer { Content = pins, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
            Place(layout, pinnedSection, 2);
            scroll.Content = history; scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled; scroll.Focusable = false;
            scroll.ScrollChanged += delegate { UpdateDepth(); }; Place(layout, scroll, 3);
            scroll.PreviewMouseWheel += SmoothWheel;
            scroll.PreviewMouseLeftButtonDown += delegate { CancelScrollAnimation(); };
            var foot = new DockPanel { Margin = new Thickness(0, 12, 0, 0), MinHeight = 24 };
            undo = TextButton("Undo", delegate { store.Undo(); }); undo.Foreground = Brush("#80CBC4"); DockPanel.SetDock(undo, Dock.Right); foot.Children.Add(undo);
            footer.FontSize = 10; footer.Foreground = muted; footer.VerticalAlignment = VerticalAlignment.Center; footer.TextWrapping = TextWrapping.Wrap; foot.Children.Add(footer); Place(layout, foot, 4);
            store.Changed += Refresh; service.Feedback += SetFeedback;
            PreviewKeyDown += HandleKey;
            Deactivated += delegate { if (!dialogOpen) HidePanel(); };
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (!closing) { e.Cancel = true; HidePanel(); } };
            IsVisibleChanged += delegate { UpdateBackdrop(); };
            clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            clock.Tick += delegate { store.ExpireUndo(); if (IsVisible) { UpdateFooter(); foreach (var pair in times) { var clip = store.State.Items.FirstOrDefault(c => c.Id == pair.Key); if (clip != null) pair.Value.Text = Age(clip); } if (copiedId != null && DateTime.UtcNow > copiedUntil) { var clip = store.State.Items.FirstOrDefault(c => c.Id == copiedId); if (clip != null && copyLabels.ContainsKey(copiedId)) copyLabels[copiedId].Text = (clip.IsPinned ? "●  " : "") + clip.Kind; copiedId = null; } } };
            clock.Start(); ApplyMood(); Refresh();
        }
        static void Place(Grid parent, UIElement element, int row) { Grid.SetRow(element, row); parent.Children.Add(element); }
        internal static SolidColorBrush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        internal static TextBlock Label(string text, double size, Brush color) { return new TextBlock { Text = text, FontSize = size, Foreground = color }; }
        static Style ButtonStyle()
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White)); style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent)); style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(7))); style.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
            var border = new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7)); border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(FrameworkElement.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty)); presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            border.AppendChild(presenter); style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(Button)) { VisualTree = border }));
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true }; hover.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#28FFFFFF"))); style.Triggers.Add(hover);
            var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true }; focus.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#4080CBC4"))); style.Triggers.Add(focus);
            return style;
        }
        static Style ScrollBarStyle()
        {
            return (Style)System.Windows.Markup.XamlReader.Parse(@"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ScrollBar}'>
 <Setter Property='Width' Value='7'/><Setter Property='Background' Value='Transparent'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ScrollBar}'>
  <Track x:Name='PART_Track' Orientation='{TemplateBinding Orientation}' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{TemplateBinding Value}' ViewportSize='{TemplateBinding ViewportSize}' IsDirectionReversed='True'>
   <Track.DecreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageUpCommand}' Focusable='False'><RepeatButton.Template><ControlTemplate><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
   <Track.Thumb><Thumb><Thumb.Template><ControlTemplate><Border Background='#68707E' CornerRadius='4'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
   <Track.IncreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageDownCommand}' Focusable='False'><RepeatButton.Template><ControlTemplate><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
  </Track>
  <ControlTemplate.Triggers><Trigger Property='Orientation' Value='Horizontal'><Setter TargetName='PART_Track' Property='IsDirectionReversed' Value='False'/></Trigger></ControlTemplate.Triggers>
 </ControlTemplate></Setter.Value></Setter>
 <Style.Triggers><Trigger Property='Orientation' Value='Horizontal'><Setter Property='Width' Value='Auto'/><Setter Property='Height' Value='6'/></Trigger></Style.Triggers>
</Style>");
        }
        Button ActionButton(string glyph, string name, Action action)
        {
            var button = TextButton(glyph, action); button.FontFamily = new FontFamily("Segoe MDL2 Assets"); button.FontSize = 14; button.Foreground = muted;
            button.ToolTip = name; AutomationProperties.SetName(button, name); return button;
        }
        Button TextButton(string text, Action action)
        {
            var button = new Button { Content = text }; button.Click += delegate { action(); };
            button.MouseEnter += delegate { Motion.Hover(button, MotionSetting, true); };
            button.MouseLeave += delegate { Motion.Hover(button, MotionSetting, false); }; return button;
        }
        public void Toggle() { if (IsVisible && !hiding) HidePanel(); else ShowPanel(); }
        internal void HidePanel()
        {
            if (!IsVisible || hiding) return;
            hiding = true; int version = ++panelVersion;
            ((FrameworkElement)Content).IsHitTestVisible = false;
            SetBackdropRunning(false); CancelScrollAnimation();
            Motion.Exit((FrameworkElement)Content, MotionSetting, delegate {
                if (!hiding || version != panelVersion) return;
                Hide(); hiding = false; RemoveOverlay(); Motion.Reset((FrameworkElement)Content);
            });
        }
        public void ShowPanel()
        {
            bool entering = !IsVisible || hiding; hiding = false; ++panelVersion;
            ((FrameworkElement)Content).IsHitTestVisible = true;
            var area = Forms.Screen.FromPoint(Forms.Control.MousePosition).WorkingArea;
            var handle = new WindowInteropHelper(this).EnsureHandle();
            var source = HwndSource.FromHwnd(handle);
            double scale = source.CompositionTarget.TransformToDevice.M22;
            Height = Math.Min(650, area.Height / scale - 24); Width = Math.Min(410, area.Width / scale - 24);
            Show();
            // Position in physical pixels; Windows/WPF handle the target monitor's DPI change.
            NativePosition.SetWindowPos(handle, IntPtr.Zero, area.Right - (int)(Width * scale) - 12, area.Bottom - (int)(Height * scale) - 12, 0, 0, 0x15);
            Activate(); Native.SetForegroundWindow(handle); search.Focus();
            if (entering) {
                Refresh(); Motion.Enter((FrameworkElement)Content, MotionSetting, 0, true);
                int order = 0; foreach (var clip in store.Visible(search.Text)) { Border card; if (cards.TryGetValue(clip.Id, out card)) Motion.Enter(card, MotionSetting, Math.Min(order++, 6) * 45); }
            }
            UpdateBackdrop();
        }
        public void SetFeedback(string message) { feedback = message; feedbackUntil = DateTime.UtcNow.AddSeconds(3); UpdateFooter(); }
        void UpdateFooter()
        {
            undo.Visibility = store.CanUndo ? Visibility.Visible : Visibility.Collapsed;
            footer.Text = store.Error ?? (store.CanUndo ? "Item deleted · Ctrl+Z to undo" : DateTime.UtcNow < feedbackUntil ? feedback : store.State.Settings.Paused ? "Capture paused · resume in Settings" : "↑↓ Select   Enter Copy   Space Preview · Ctrl+Shift+V");
        }
        public void Refresh()
        {
            var previous = new HashSet<string>(cards.Keys);
            var positions = new Dictionary<string, Point>();
            if (IsVisible) foreach (var pair in cards) if (pair.Value.ActualHeight > 0) positions[pair.Key] = pair.Value.TranslatePoint(new Point(), root);
            ApplyMood(); pins.Children.Clear(); history.Children.Clear(); cards.Clear(); times.Clear(); copyLabels.Clear();
            var visible = store.Visible(search.Text).ToList();
            if (!visible.Any(c => c.Id == selected)) selected = visible.Count > 0 ? visible[0].Id : null;
            var pinned = visible.Where(c => c.IsPinned).ToList(); pinnedSection.Visibility = pinned.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            ((TextBlock)pinnedSection.Children[0]).Text = "PINNED · " + pinned.Count;
            foreach (var clip in pinned) pins.Children.Add(Card(clip, true));
            foreach (var clip in visible.Where(c => !c.IsPinned)) {
                var card = Card(clip, false); history.Children.Add(card);
            }
            if (history.Children.Count == 0) {
                var empty = new StackPanel { Margin = new Thickness(14, 35, 14, 28), HorizontalAlignment = HorizontalAlignment.Center };
                var icon = Label("\uE8A5", 32, Brush("#80CBC4")); icon.FontFamily = new FontFamily("Segoe MDL2 Assets"); icon.HorizontalAlignment = HorizontalAlignment.Center; empty.Children.Add(icon);
                var title = Label(search.Text.Length > 0 ? "No matches" : pinned.Count > 0 ? "Your favorites are pinned above" : "Keep the good stuff close", 16, Brushes.White); title.Margin = new Thickness(0, 16, 0, 8); title.TextAlignment = TextAlignment.Center; empty.Children.Add(title);
                var help = Label(search.Text.Length > 0 ? "Try another word or search for ‘image’." : "Copy text or an image to start your history.\nOpen from the tray or press Ctrl+Shift+V.", 12, muted); help.TextAlignment = TextAlignment.Center; help.TextWrapping = TextWrapping.Wrap; empty.Children.Add(help); history.Children.Add(empty);
            }
            UpdateSelection(); UpdateFooter();
            if (IsVisible) foreach (var pair in cards) {
                var card = pair.Value; Point oldPosition;
                if (!previous.Contains(pair.Key)) Motion.Enter(card, MotionSetting);
                else if (MotionSetting == MotionLevel.Full && positions.TryGetValue(pair.Key, out oldPosition)) {
                    Point before = oldPosition;
                    card.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate {
                        if (card.Parent == null) return;
                        Point after = card.TranslatePoint(new Point(), root); var parts = Motion.Parts(card);
                        if (Math.Abs(before.Y - after.Y) > 1) Motion.To(parts.EntryOffset, TranslateTransform.YProperty, 0, 320, Math.Max(-180, Math.Min(180, before.Y - after.Y)));
                        if (Math.Abs(before.X - after.X) > 1) Motion.To(parts.EntryOffset, TranslateTransform.XProperty, 0, 320, Math.Max(-180, Math.Min(180, before.X - after.X)));
                    }));
                }
            }
            UpdateBackdrop();
        }
        Border Card(Clip clip, bool pin)
        {
            var content = new StackPanel();
            var top = new DockPanel { LastChildFill = true };
            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            actions.Children.Add(ActionButton("\uE8C8", "Copy item", delegate { Select(clip); CopyClip(clip); }));
            if (!pin) actions.Children.Add(ActionButton("\uE718", "Pin item", delegate { store.TogglePin(clip); }));
            if (!pin) actions.Children.Add(ActionButton("\uE74D", "Delete item", delegate { DeleteClip(clip); }));
            DockPanel.SetDock(actions, Dock.Right); top.Children.Add(actions);
            var kind = Label((pin ? "●  " : "") + clip.Kind, 11, muted); kind.VerticalAlignment = VerticalAlignment.Center; kind.FontWeight = FontWeights.SemiBold; top.Children.Add(kind); content.Children.Add(top);
            copyLabels[clip.Id] = kind;
            var body = new StackPanel { Margin = new Thickness(0, 7, 0, 5) };
            if (clip.IsImage) {
                try { body.Children.Add(new Image { Source = clip.Image(pin ? 280 : 700), MaxHeight = pin ? 57 : 145, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center }); }
                catch (Exception ex) { if (!(ex is IOException || ex is ArgumentException || ex is NotSupportedException || ex is InvalidOperationException)) throw; body.Children.Add(Label("Image unavailable", 12, muted)); }
            } else {
                var line = new DockPanel(); string hex = Presentation.Hex(clip);
                if (hex != null) line.Children.Add(new Border { Width = pin ? 22 : 30, Height = pin ? 22 : 30, Background = Brush(hex), CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 0, 10, 0) });
                string text = clip.Text; int limit = pin ? 140 : expanded.Contains(clip.Id) ? 6000 : 700;
                if (text.Length > limit) text = text.Substring(0, limit) + "…";
                var copyText = Label(text, pin ? 12 : 14, Brushes.White); copyText.TextWrapping = TextWrapping.Wrap;
                copyText.MaxHeight = pin ? 44 : expanded.Contains(clip.Id) ? 320 : 66;
                if (clip.Kind == "Code") copyText.FontFamily = new FontFamily("Consolas"); line.Children.Add(copyText); body.Children.Add(line);
                string domain = Presentation.Domain(clip); if (domain != null && !pin) { var domainText = Label(domain, 10, Brush("#80CBC4")); domainText.Margin = new Thickness(0, 7, 0, 0); body.Children.Add(domainText); }
            }
            var bodyButton = new Button { Content = body, Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch, Focusable = false };
            AutomationProperties.SetName(bodyButton, clip.IsImage ? "Preview image" : "Copy " + clip.Kind.ToLowerInvariant());
            bodyButton.Click += delegate { Select(clip); if (clip.IsImage) ShowPreview(clip); else CopyClip(clip); }; content.Children.Add(bodyButton);
            if (!pin) {
                var bottom = new DockPanel();
                var age = Label(Age(clip), 10, Brush("#878D9A")); age.VerticalAlignment = VerticalAlignment.Center; DockPanel.SetDock(age, Dock.Right); bottom.Children.Add(age); times[clip.Id] = age;
                var previewButton = TextButton("Preview", delegate { ShowPreview(clip); }); previewButton.FontSize = 10; previewButton.Foreground = muted; previewButton.Padding = new Thickness(0, 7, 10, 0); DockPanel.SetDock(previewButton, Dock.Left); bottom.Children.Add(previewButton);
                if (!clip.IsImage && (clip.Text.Length > 150 || clip.Text.Count(c => c == '\n') > 2)) {
                    var expand = TextButton(expanded.Contains(clip.Id) ? "Collapse" : "Expand", delegate { if (!expanded.Add(clip.Id)) expanded.Remove(clip.Id); Refresh(); }); expand.FontSize = 10; expand.Foreground = muted; expand.Padding = new Thickness(0, 7, 0, 0); bottom.Children.Add(expand);
                }
                content.Children.Add(bottom);
            }
            var card = new Border { Child = content, CornerRadius = new CornerRadius(13), Padding = new Thickness(12, 8, 12, 10), BorderThickness = new Thickness(1), Background = Brush("#C01A1D26"), BorderBrush = Brush("#424653"), Margin = pin ? new Thickness(0, 0, 8, 2) : new Thickness(0, 2, 6, 10) };
            if (pin) { card.Width = 164; card.MinHeight = 103; }
            Motion.Parts(card);
            card.MouseEnter += delegate { HoverCard(card, true); }; card.MouseLeave += delegate { HoverCard(card, false); };
            var menu = new ContextMenu();
            AddMenu(menu, "Copy", delegate { CopyClip(clip); }); AddMenu(menu, "Preview", delegate { ShowPreview(clip); });
            AddMenu(menu, clip.IsPinned ? "Unpin" : "Pin", delegate { store.TogglePin(clip); }); AddMenu(menu, "Delete", delegate { DeleteClip(clip); }); card.ContextMenu = menu;
            cards[clip.Id] = card; return card;
        }
        static void AddMenu(ContextMenu menu, string name, Action action) { var item = new MenuItem { Header = name }; item.Click += delegate { action(); }; menu.Items.Add(item); }
        static string Age(Clip clip)
        {
            var age = DateTime.UtcNow - clip.CreatedAt;
            return age.TotalSeconds < 60 ? "Just now" : age.TotalMinutes < 60 ? (int)age.TotalMinutes + " min ago" : age.TotalHours < 24 ? (int)age.TotalHours + " hr ago" : clip.CreatedAt.ToLocalTime().ToString("MMM d, HH:mm");
        }
        void Select(Clip clip) { selected = clip.Id; Focus(); UpdateSelection(); }
        void UpdateSelection() { foreach (var card in cards) card.Value.BorderBrush = Brush(card.Key == selected ? "#80CBC4" : "#424653"); }
        internal void Navigate(int direction)
        {
            var visible = store.Visible(search.Text).ToList(); if (visible.Count == 0) return;
            int index = visible.FindIndex(c => c.Id == selected); index = Math.Max(0, Math.Min(visible.Count - 1, index < 0 ? 0 : search.IsKeyboardFocused && direction > 0 ? index : index + direction));
            selected = visible[index].Id; Focus(); UpdateSelection(); cards[selected].BringIntoView();
        }
        void HandleKey(object sender, KeyEventArgs e)
        {
            bool editing = Keyboard.FocusedElement is TextBox;
            if (e.Key == Key.Escape) { if (overlay != null) CloseOverlay(); else if (search.Text.Length > 0) search.Clear(); else HidePanel(); e.Handled = true; return; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z && !editing && overlay == null) { store.Undo(); e.Handled = true; return; }
            if (Keyboard.Modifiers != ModifierKeys.None) return;
            if (overlay != null) { if (preview != null && e.Key == Key.Enter && !editing) { CopyClip(preview); e.Handled = true; } return; }
            if (editing) { if (search.IsKeyboardFocused && e.Key == Key.Down) { Navigate(1); e.Handled = true; } return; }
            if (e.Key == Key.Down || e.Key == Key.Up) { Navigate(e.Key == Key.Down ? 1 : -1); e.Handled = true; }
            var clip = store.State.Items.FirstOrDefault(c => c.Id == selected); if (clip == null) return;
            if (e.Key == Key.Enter) { CopyClip(clip); e.Handled = true; }
            if (e.Key == Key.Space) { ShowPreview(clip); e.Handled = true; }
            if (e.Key == Key.Delete) { DeleteClip(clip); e.Handled = true; }
        }
        void UpdateDepth()
        {
            if (MotionSetting != MotionLevel.Full) return;
            double strength = store.State.Settings.Intensity == "Soft" ? .37 : store.State.Settings.Intensity == "Bold" ? 1 : .65;
            foreach (var card in cards.Values.Where(c => c.Parent == history)) {
                if (card.ActualHeight <= 0) continue;
                var y = card.TranslatePoint(new Point(0, 0), scroll).Y;
                double progress = Math.Min(1, Math.Max(Math.Max(0, 15 - y), Math.Max(0, y + card.ActualHeight - scroll.ViewportHeight + 15)) / card.ActualHeight);
                var parts = Motion.Parts(card); double scale = 1 - progress * .13 * strength;
                Motion.To(parts.DepthScale, ScaleTransform.ScaleXProperty, scale, 100);
                Motion.To(parts.DepthScale, ScaleTransform.ScaleYProperty, scale, 100);
                Motion.To(parts.DepthOffset, TranslateTransform.YProperty, (y < 0 ? 1 : -1) * progress * 18 * strength, 100);
            }
        }
        internal void ApplyMood()
        {
            var p = store.State.Settings; string[] colors;
            if (p.Mood == "Sunset") colors = new[] { "#6C3544", "#36223D", "#181923" };
            else if (p.Mood == "Midnight") colors = new[] { "#192741", "#24213D", "#11141E" };
            else if (p.Mood == "Custom") colors = new[] { SafeColor(p.Color1), SafeColor(p.Color2), SafeColor(p.Color3) };
            else colors = new[] { "#383B43", "#20232B", "#14171D" };
            string key = string.Join("/", colors); if (key == palette) return;
            var level = palette == null ? MotionLevel.Off : MotionSetting;
            for (int i = 0; i < 3; i++) Motion.ColorTo(moodBrush.GradientStops[i], (Color)ColorConverter.ConvertFromString(colors[i]), level);
            Color first = (Color)ColorConverter.ConvertFromString(colors[0]); first.A = 150;
            Color second = (Color)ColorConverter.ConvertFromString(colors[1]); second.A = 130;
            Motion.ColorTo(glow1Color, first, level); Motion.ColorTo(glow2Color, second, level); palette = key;
        }
        static string SafeColor(string color) { return System.Text.RegularExpressions.Regex.IsMatch(color ?? "", "^#[0-9a-fA-F]{6}$") ? color : "#182038"; }
        void AddGlow(GradientStop color, TranslateTransform move, double x, double y)
        {
            color.Offset = 0;
            var brush = new RadialGradientBrush(); brush.GradientStops.Add(color); brush.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            var glow = new Ellipse { Width = 530, Height = 530, Fill = brush, RenderTransform = move, Opacity = .7 };
            Canvas.SetLeft(glow, x); Canvas.SetTop(glow, y); backdrop.Children.Add(glow);
        }
        void UpdateBackdrop() { SetBackdropRunning(IsVisible && !hiding); }
        internal void SetBackdropRunning(bool visible)
        {
            bool running = visible && MotionSetting == MotionLevel.Full;
            if (drifting == running) return;
            drifting = running; Motion.Drift(glow1Move, running, false); Motion.Drift(glow2Move, running, true);
        }
        void HoverCard(Border card, bool over)
        {
            Motion.Hover(card, MotionSetting, over);
            var brush = (SolidColorBrush)card.Background; Color before = brush.Color;
            Color next = Brush(over ? "#EE2C3442" : "#C01A1D26").Color; brush.Color = next;
            brush.BeginAnimation(SolidColorBrush.ColorProperty, MotionSetting == MotionLevel.Off ? null : new ColorAnimation(before, next, TimeSpan.FromMilliseconds(180)) { FillBehavior = FillBehavior.Stop });
        }
        internal bool CopyClip(Clip clip)
        {
            if (!service.Copy(clip)) return false;
            copiedId = clip.Id; copiedUntil = DateTime.UtcNow.AddSeconds(2);
            TextBlock label; if (copyLabels.TryGetValue(clip.Id, out label)) label.Text = "Copied ✓";
            Border card;
            if (cards.TryGetValue(clip.Id, out card) && MotionSetting != MotionLevel.Off) {
                var brush = (SolidColorBrush)card.Background;
                brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Brush("#CF397568").Color, brush.Color, TimeSpan.FromMilliseconds(650)) { FillBehavior = FillBehavior.Stop });
                if (MotionSetting == MotionLevel.Full) {
                    var parts = Motion.Parts(card);
                    Motion.To(parts.HoverScale, ScaleTransform.ScaleXProperty, 1, 350, 1.04);
                    Motion.To(parts.HoverScale, ScaleTransform.ScaleYProperty, 1, 350, 1.04);
                }
            }
            return true;
        }
        internal void DeleteClip(Clip clip)
        {
            if (!deleting.Add(clip.Id)) return;
            Action remove = delegate { deleting.Remove(clip.Id); store.Delete(clip); };
            Border card;
            if (IsVisible && cards.TryGetValue(clip.Id, out card)) Motion.Exit(card, MotionSetting, remove); else remove();
        }
        void SmoothWheel(object sender, MouseWheelEventArgs e)
        {
            if (MotionSetting != MotionLevel.Full || scroll.ScrollableHeight <= 0) return;
            double current = wheelAnimating ? scrollTarget : scroll.VerticalOffset;
            SmoothScrollTo(current - e.Delta / 120.0 * 88); e.Handled = true;
        }
        internal void SmoothScrollTo(double target)
        {
            scrollTarget = Math.Max(0, Math.Min(scroll.ScrollableHeight, target));
            if (MotionSetting != MotionLevel.Full) { CancelScrollAnimation(); scroll.ScrollToVerticalOffset(scrollTarget); return; }
            double from = scroll.VerticalOffset;
            SetValue(AnimatedScrollOffsetProperty, scrollTarget); wheelAnimating = true;
            var animation = new DoubleAnimation(from, scrollTarget, TimeSpan.FromMilliseconds(220)) { FillBehavior = FillBehavior.Stop, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            animation.Completed += delegate { wheelAnimating = false; };
            BeginAnimation(AnimatedScrollOffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }
        void CancelScrollAnimation()
        {
            double current = scroll.VerticalOffset;
            BeginAnimation(AnimatedScrollOffsetProperty, null); SetValue(AnimatedScrollOffsetProperty, current); wheelAnimating = false;
        }
        Grid NewOverlay(string title)
        {
            bool replacing = overlay != null; RemoveOverlay(); overlay = new Grid { Background = Brush("#151820"), Margin = new Thickness(1) }; root.Children.Add(overlay);
            overlay.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); overlay.RowDefinitions.Add(new RowDefinition()); overlay.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var head = new DockPanel { Margin = new Thickness(20, 18, 20, 16) };
            var close = ActionButton("\uE8BB", "Close (Esc)", CloseOverlay); DockPanel.SetDock(close, Dock.Right); head.Children.Add(close);
            var name = Label(title, 18, Brushes.White); name.FontWeight = FontWeights.SemiBold; name.VerticalAlignment = VerticalAlignment.Center; head.Children.Add(name); Place(overlay, head, 0);
            layout.IsEnabled = false; Focus(); if (!replacing) Motion.Enter(overlay, MotionSetting, 0, true); return overlay;
        }
        internal void CloseOverlay()
        {
            var old = overlay; if (old == null) return;
            overlay = null; preview = null; old.IsHitTestVisible = false;
            Motion.Exit(old, MotionSetting, delegate { root.Children.Remove(old); }); layout.IsEnabled = true; Focus();
        }
        void RemoveOverlay() { if (overlay != null) root.Children.Remove(overlay); overlay = null; preview = null; layout.IsEnabled = true; }
        internal void ShowPreview(Clip clip)
        {
            Select(clip); var grid = NewOverlay(clip.Kind + " preview"); preview = clip;
            var viewer = new ScrollViewer { Margin = new Thickness(20, 0, 20, 10), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            if (clip.IsImage) {
                try { viewer.Content = new Image { Source = clip.Image(1200), Stretch = Stretch.Uniform, MaxHeight = Height - 160 }; }
                catch (Exception ex) { if (!(ex is IOException || ex is ArgumentException || ex is NotSupportedException || ex is InvalidOperationException)) throw; viewer.Content = Label("Image unavailable", 14, muted); }
            } else {
                viewer.Content = new TextBox { Text = clip.Text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new Thickness(0), FontFamily = new FontFamily(clip.Kind == "Code" ? "Consolas" : "Segoe UI"), FontSize = 14, Padding = new Thickness(5) };
            }
            Place(grid, viewer, 1);
            var bottom = new DockPanel { Margin = new Thickness(20, 10, 20, 18) };
            var copy = TextButton("Copy", delegate { if (CopyClip(clip)) ((Button)bottom.Children[0]).Content = "Copied ✓"; }); copy.Background = Brush("#35534F"); DockPanel.SetDock(copy, Dock.Right); bottom.Children.Add(copy);
            var hint = Label("Esc to close · Enter to copy", 11, muted); hint.VerticalAlignment = VerticalAlignment.Center; bottom.Children.Add(hint); Place(grid, bottom, 2);
        }
        public void ShowSettings()
        {
            var grid = NewOverlay("Mood & settings");
            var stack = new StackPanel { Margin = new Thickness(20, 0, 20, 0) };
            var p = store.State.Settings;
            stack.Children.Add(Label("MOOD", 10, muted));
            var choices = new UniformGridCompat(4);
            foreach (string name in new[] { "Chrome", "Sunset", "Midnight", "Custom" }) {
                string mood = name;
                var button = TextButton(name, delegate { p.Mood = mood; store.UpdateSettings(); ShowSettings(); });
                button.Background = Brush(p.Mood == name ? "#35534F" : "#252A35"); button.Margin = new Thickness(0, 10, 5, 18); button.FontSize = 11; choices.Add(button);
            }
            stack.Children.Add(choices.Grid);
            if (p.Mood == "Custom") {
                AddColor(stack, "First glow", p.Color1, delegate(string value) { p.Color1 = value; });
                AddColor(stack, "Second glow", p.Color2, delegate(string value) { p.Color2 = value; });
                AddColor(stack, "Background", p.Color3, delegate(string value) { p.Color3 = value; });
            }
            var intensityLabel = Label("SCROLL INTENSITY", 10, muted); intensityLabel.Margin = new Thickness(0, 12, 0, 8); stack.Children.Add(intensityLabel);
            var intensity = new ComboBox { ItemsSource = new[] { "Soft", "Balanced", "Bold" }, SelectedItem = p.Intensity, Padding = new Thickness(7), Margin = new Thickness(0, 0, 0, 14) };
            intensity.SelectionChanged += delegate { p.Intensity = (string)intensity.SelectedItem; store.UpdateSettings(); }; stack.Children.Add(intensity);
            stack.Children.Add(Label("ANIMATIONS", 10, muted));
            var motion = new ComboBox { ItemsSource = new[] { "Full", "Reduced", "Off" }, SelectedItem = p.Motion ?? "Full", Padding = new Thickness(7), Margin = new Thickness(0, 8, 0, 14) };
            motion.SelectionChanged += delegate {
                p.Motion = (string)motion.SelectedItem; store.UpdateSettings();
                if (MotionSetting != MotionLevel.Full) { CancelScrollAnimation(); Motion.Reset((FrameworkElement)Content); if (overlay != null) Motion.Reset(overlay); }
                if (MotionSetting == MotionLevel.Off) { foreach (var stop in moodBrush.GradientStops) stop.BeginAnimation(GradientStop.ColorProperty, null); glow1Color.BeginAnimation(GradientStop.ColorProperty, null); glow2Color.BeginAnimation(GradientStop.ColorProperty, null); }
            };
            stack.Children.Add(motion);
            var pause = new CheckBox { Content = "Pause clipboard capture", IsChecked = p.Paused, Foreground = Brushes.White, Margin = new Thickness(0, 10, 0, 12) };
            pause.Click += delegate { p.Paused = pause.IsChecked == true; service.SkipCurrent(); store.UpdateSettings(); }; stack.Children.Add(pause);
            var startup = new CheckBox { Content = "Start with Windows", IsChecked = Program.StartupEnabled(), Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 18) };
            startup.Click += delegate {
                try { Program.SetStartup(startup.IsChecked == true); }
                catch (Exception ex) { if (!(ex is UnauthorizedAccessException || ex is System.Security.SecurityException || ex is IOException)) throw; startup.IsChecked = Program.StartupEnabled(); SetFeedback("Could not update Windows startup settings."); }
            }; stack.Children.Add(startup);
            var local = Label("History stays on this PC and is encrypted for your Windows account. Up to 50 recent items; pinned favorites are kept separately.\n\nShortcut: Ctrl+Shift+V", 12, muted); local.TextWrapping = TextWrapping.Wrap; stack.Children.Add(local);
            var clear = TextButton("Clear all history and pins…", delegate {
                dialogOpen = true;
                try { if (MessageBox.Show(this, "Delete all history and pinned favorites? This cannot be undone.", "Clear ClipBoard", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) { store.Clear(); SetFeedback("History and pins cleared."); } }
                finally { dialogOpen = false; Activate(); }
            }); clear.Foreground = Brush("#E8A4A4"); clear.Margin = new Thickness(0, 20, 0, 0); stack.Children.Add(clear);
            var dataPath = Label(Program.DataFolder, 10, muted); dataPath.TextWrapping = TextWrapping.Wrap; dataPath.Margin = new Thickness(0, 14, 0, 14); stack.Children.Add(dataPath);
            Place(grid, new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, 1);
            var done = TextButton("Done", CloseOverlay); done.Margin = new Thickness(20, 12, 20, 18); done.Background = Brush("#292F3B"); Place(grid, done, 2);
        }
        void AddColor(StackPanel stack, string name, string hex, Action<string> set)
        {
            var button = TextButton(name + "    " + hex, delegate {
                dialogOpen = true;
                using (var dialog = new Forms.ColorDialog()) {
                    dialog.Color = System.Drawing.ColorTranslator.FromHtml(SafeColor(hex)); dialog.FullOpen = true;
                    try { if (dialog.ShowDialog(new DialogOwner(new WindowInteropHelper(this).Handle)) == Forms.DialogResult.OK) { set(System.Drawing.ColorTranslator.ToHtml(dialog.Color)); store.UpdateSettings(); ShowSettings(); } }
                    finally { dialogOpen = false; Activate(); }
                }
            }); button.Background = Brush(SafeColor(hex));
            var color = ((SolidColorBrush)button.Background).Color;
            if (.299 * color.R + .587 * color.G + .114 * color.B > 145) button.Foreground = Brush("#161923");
            button.Margin = new Thickness(0, 0, 0, 8); stack.Children.Add(button);
        }
        internal void Finish() { closing = true; SetBackdropRunning(false); CancelScrollAnimation(); clock.Stop(); service.Feedback -= SetFeedback; store.Changed -= Refresh; Close(); }
        sealed class DialogOwner : Forms.IWin32Window { public IntPtr Handle { get; private set; } public DialogOwner(IntPtr handle) { Handle = handle; } }
        sealed class UniformGridCompat
        {
            public Grid Grid = new Grid(); int next;
            public UniformGridCompat(int count) { for (int i = 0; i < count; i++) Grid.ColumnDefinitions.Add(new ColumnDefinition()); }
            public void Add(UIElement child) { System.Windows.Controls.Grid.SetColumn(child, next++); Grid.Children.Add(child); }
        }
        static class NativePosition { [System.Runtime.InteropServices.DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags); }
    }
}
