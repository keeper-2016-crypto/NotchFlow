using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Microsoft.Win32;
using NotchFlow.Animations;
using NotchFlow.Configuration;
using NotchFlow.Services;
using NotchFlow.ViewModels;

namespace NotchFlow.Views;
public partial class IslandWindow : Window
{
    readonly IslandViewModel vm;
    NativeBackdrop? nativeBackdrop;
    readonly AudioAnalysisService audioAnalysis=new();
    readonly PopupInteractionManager interactions = new();
    readonly VolumeService? volume;
    AppSettings settings;
    bool expanded, pinned, showingToast, closing, updatingVolume;
    int transition;
    ContextMenu? mediaSourceMenu;
    public bool IsMediaSourceSelectorOpen { get; private set; }
    readonly DispatcherTimer hover = new() { Interval = TimeSpan.FromMilliseconds(150) };
    readonly DispatcherTimer collapse = new() { Interval = TimeSpan.FromMilliseconds(380) };
    readonly DispatcherTimer toastTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    public event Action? SettingsRequested;
    public bool IsExpanded => expanded;

    public IslandWindow(IslandViewModel vm, AppSettings settings, VolumeService? volume = null)
    {
        this.vm = vm; this.settings = settings; this.volume = volume;
        InitializeComponent(); DataContext = vm;
        GlassLayer.Surface=Capsule;
        Capsule.MouseEnter+=(_,_)=>GlassLayer.Hover(true);
        Capsule.MouseLeave+=(_,_)=>GlassLayer.Hover(false);
        Capsule.AddHandler(UIElement.PreviewMouseDownEvent,new MouseButtonEventHandler((_,_)=>GlassLayer.Press()),true);
        RibbonLayer.Surface=Capsule;audioAnalysis.Changed+=RibbonLayer.AcceptAudio;EffectsLayer.Surface=Capsule;audioAnalysis.Changed+=EffectsLayer.AcceptAudio;
        interactions.Changed+=InteractionChanged;
        SystemVolume.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,new MouseButtonEventHandler(BeginVolumeDrag),true);
        SystemVolume.PreviewMouseMove+=MoveVolumeDrag;
        SystemVolume.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent,new MouseButtonEventHandler(EndVolumeDrag),true);
        SystemVolume.GotMouseCapture+=(_,_)=>interactions.Set(SystemVolume,true);
        SystemVolume.LostMouseCapture+=(_,_)=>Dispatcher.BeginInvoke(new Action(()=>interactions.Set(SystemVolume,SystemVolume.IsMouseCaptureWithin)));
        hover.Tick += HoverTick; collapse.Tick += CollapseTick; toastTimer.Tick += ToastTick;
        vm.PropertyChanged += MediaChanged;
        SourceInitialized += InitializedWindow;
        SystemEvents.DisplaySettingsChanged += DisplayChanged;
        Deactivated += (_, _) => { if(!HasInteractiveOverlay && settings.CollapseOnLeave && !pinned && !Capsule.IsMouseOver) collapse.Start(); };
        if (volume != null) volume.StateChanged += VolumeStateChanged;
        SystemVolume.IsEnabled = MuteButton.IsEnabled = false;
    }
    void InitializedWindow(object? sender, EventArgs args)
    {
        WindowService.Initialize(this);
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(Hook);
        nativeBackdrop=new NativeBackdrop(this,Capsule);
        LayoutUpdated+=BackdropLayout;IsVisibleChanged+=BackdropVisibility;
        Apply(settings); SetExpanded(!settings.StartCollapsed);
    }
    IntPtr Hook(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if(msg==0x0047)SyncBackdrop(true);
        if (msg == 0x02E0) Dispatcher.BeginInvoke(new Action(() => { if (!closing) Apply(settings); }));
        return IntPtr.Zero;
    }
    void DisplayChanged(object? sender, EventArgs args)
    {
        if (!closing) Dispatcher.BeginInvoke(new Action(() => { if (!closing) Apply(settings); }));
    }
    public void Apply(AppSettings value)
    {
        settings = value; audioAnalysis.Configure(settings.AudioAnalysisEnabled,settings.AudioSensitivity); Capsule.IsNotch = settings.AttachToTop;
        double fittedScale = Math.Min(settings.Scale, WindowService.MaximumScale(settings, 440, 432));
        Width = 440 * fittedScale; Height = 432 * fittedScale;
        Capsule.LayoutTransform = new ScaleTransform(fittedScale, fittedScale);
        Capsule.Margin=new Thickness(0,settings.MediaVisualizer=="Neon Ribbon"&&!settings.AttachToTop?10*fittedScale:0,0,0);
        Capsule.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.CapsuleColor)) { Opacity = settings.BackgroundEffect is "Acrylic Glass" or "Liquid Glass" ? 1 : settings.Opacity };
        if(settings.MediaVisualizer=="Neon Ribbon")Capsule.Background=Brushes.Black;
        if(settings.BackgroundEffect=="Liquid Glass")Capsule.Background=Brushes.Transparent;
        Capsule.Effect = settings.Shadow ? new DropShadowEffect { BlurRadius = 12, ShadowDepth = 3, Opacity = .22 } : null;
        Topmost = settings.AlwaysOnTop; vm.Personal.AnimationsEnabled = settings.Animations && !settings.ReduceMotion; vm.Weather.Configure(settings.WeatherCity); vm.SetEnabled(settings.MediaEnabled);
        if (!settings.VolumeEnabled && showingToast) { showingToast = false; toastTimer.Stop(); }
        RenderState(); WindowService.Position(this, settings); UpdateAccent(); UpdateActivity();
        try{nativeBackdrop?.Enable(settings.BackgroundEffect=="Liquid Glass"&&settings.GlassBackdropBlur);}catch(Exception ex){Log.Error(ex);nativeBackdrop?.Dispose();nativeBackdrop=null;}
    }
    void BackdropLayout(object? sender,EventArgs e)=>SyncBackdrop(false);
    void BackdropVisibility(object sender,DependencyPropertyChangedEventArgs e)=>SyncBackdrop(true);
    void SyncBackdrop(bool force)
    {
        try{nativeBackdrop?.Sync(force);}
        catch(Exception ex){Log.Error(ex);nativeBackdrop?.Dispose();nativeBackdrop=null;}
    }
    int ContentHeight => vm.SelectedTab switch { "Weather" => 320, "Notes" => 390, "Tasks" => 350, _ => 150 };
    int Duration => settings.Animations && !settings.ReduceMotion && SystemParameters.ClientAreaAnimation ? settings.AnimationMs : 0;
    void Resize()
    {
        Motion.To(Capsule, WidthProperty, (showingToast ? 260 : expanded ? 400 : vm.UseClock ? 176 : 212) + (settings.AttachToTop ? 20 : 0), Duration);
        Motion.To(Capsule, HeightProperty, showingToast ? 40 : expanded ? ContentHeight : 36, Duration);
        var radius = new CornerRadius(expanded && !showingToast ? settings.CornerRadius : 18);
        if (Duration == 0) { Capsule.BeginAnimation(Border.CornerRadiusProperty, null); Capsule.CornerRadius = radius; }
        else Capsule.BeginAnimation(Border.CornerRadiusProperty, new CornerRadiusAnimation { From = Capsule.CornerRadius, To = radius, Duration = TimeSpan.FromMilliseconds(Duration) });
        UpdateClip();
    }
    void CapsuleSizeChanged(object sender, SizeChangedEventArgs args) => UpdateClip();
    void UpdateClip()
    {
        if (ClipRoot == null) return;
        RibbonLayer?.InvalidateVisual();GlassLayer?.InvalidateVisual();
        double r = Math.Min(Capsule.CornerRadius.TopLeft, Capsule.ActualHeight / 2);
        ClipRoot.Clip = Capsule.Outline(Math.Max(0, Capsule.ActualWidth - 1.4), Math.Max(0, Capsule.ActualHeight - 1.4));
    }
    void Fade(FrameworkElement element, bool visible, int version, int delay = 0)
    {
        element.IsHitTestVisible = visible;
        if (Duration == 0) { element.BeginAnimation(OpacityProperty, null); element.Opacity = visible ? 1 : 0; element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed; return; }
        if (visible) element.Visibility = Visibility.Visible;
        var fade = new DoubleAnimation(visible ? 1 : 0, TimeSpan.FromMilliseconds(Duration * .65))
        { BeginTime = TimeSpan.FromMilliseconds(delay), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        fade.Completed += (_, _) => { if (transition == version && !visible) element.Visibility = Visibility.Collapsed; };
        element.BeginAnimation(OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
    }
    void RenderState()
    {
        vm.SetVisible(expanded && !showingToast);
        Expanded.Height = ContentHeight;
        WindowService.SetInteractive(this, expanded && !showingToast && vm.SelectedTab is "Notes" or "Tasks");
        int version = ++transition;
        Resize();
        Fade(Compact, !expanded && !showingToast && !vm.UseClock, version);
        Fade(ClockCompact, !expanded && !showingToast && vm.UseClock, version);
        Fade(Expanded, expanded && !showingToast, version, expanded ? Duration / 4 : 0);
        Fade(Toast, showingToast, version);
        Fade(PlayerContent, vm.SelectedTab == "Music", version);
        Fade(WeatherPane, vm.SelectedTab == "Weather", version);
        Fade(NotesPane, vm.SelectedTab == "Notes", version);
        Fade(TasksPane, vm.SelectedTab == "Tasks", version);
        vm.SetVisible(expanded && !showingToast);
        UpdateActivity();
    }
    public void Reveal() { Show(); SetExpanded(true); }
    void SetExpanded(bool value,[System.Runtime.CompilerServices.CallerMemberName] string reason="") { if(!value)Log.Collapse(reason,HasInteractiveOverlay); if(!value && HasInteractiveOverlay)return; expanded = value; RenderState(); }
    void EnterCapsule(object sender, MouseEventArgs args) { collapse.Stop(); if (!expanded && !showingToast) hover.Start(); }
    void HoverTick(object? sender, EventArgs args) { hover.Stop(); if (Capsule.IsMouseOver && !showingToast) SetExpanded(true); }
    void LeaveCapsule(object sender, MouseEventArgs args) { hover.Stop(); if (!HasInteractiveOverlay && settings.CollapseOnLeave && !pinned) collapse.Start(); }
    void CollapseTick(object? sender, EventArgs args) { collapse.Stop(); if (!HasInteractiveOverlay && settings.CollapseOnLeave && !pinned && !Capsule.IsMouseOver && !SystemVolume.IsMouseCaptureWithin && !(IsKeyboardFocusWithin && vm.SelectedTab is "Notes" or "Tasks")) SetExpanded(false); }
    void OpenClick(object sender, MouseButtonEventArgs args) { hover.Stop(); SetExpanded(true); }
    void SelectTab(object sender, RoutedEventArgs args) { if(sender is Button button && button.Tag is string tab) { vm.Personal.Flush(); vm.SelectedTab=tab; if(tab is "Notes" or "Tasks") Activate(); } }
    public bool TrySaveUserData() => vm.Personal.Flush();
    void PinClick(object sender, RoutedEventArgs args) { pinned = !pinned; PinPath.Stroke = pinned ? Brushes.White : new SolidColorBrush(Color.FromRgb(133,133,135)); }
    async void SourceClick(object sender,RoutedEventArgs args)
    {
        if(IsMediaSourceSelectorOpen)return;
        var menu=new ContextMenu{Style=(Style)FindResource("SourceMenu"),PlacementTarget=(Button)sender};
        foreach(var source in vm.MediaSources){
            var captured=source;string id=source.SourceAppUserModelId;string lower=id.ToLowerInvariant();
            string name=lower.Contains("spotify")?"Spotify":lower.Contains("yandex")?"Yandex Music":lower.Contains("chrome")?"Chrome":lower.Contains("msedge")?"Edge":lower.Contains("firefox")?"Firefox":id;
            var lines=new StackPanel();
            lines.Children.Add(new TextBlock{Text=name,FontSize=14,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,TextWrapping=TextWrapping.Wrap,MaxWidth=290});
            var detail=new TextBlock{Text="Медиаисточник",FontSize=11,Foreground=new SolidColorBrush(Color.FromRgb(200,202,211)),TextWrapping=TextWrapping.Wrap,MaxWidth=290,Margin=new Thickness(0,3,0,0)};
            lines.Children.Add(detail);
            var item=new MenuItem{Header=lines,Style=(Style)FindResource("SourceItem"),IsChecked=source==vm.SelectedSource};
            item.Click+=(_,_)=>{vm.SelectSource(captured);menu.IsOpen=false;};menu.Items.Add(item);
            _=FillSourceDetail();
            async System.Threading.Tasks.Task FillSourceDetail(){try{var metadata=await captured.TryGetMediaPropertiesAsync();detail.Text=string.IsNullOrWhiteSpace(metadata.Title)?"Без названия":metadata.Title;}catch{detail.Text="Название недоступно";}}
        }
        if(menu.Items.Count==0)menu.Items.Add(new MenuItem{Header=new TextBlock{Text="Нет доступных медиасессий"},Style=(Style)FindResource("SourceItem"),IsEnabled=false});
        mediaSourceMenu=menu;
        menu.Closed+=(_,_)=>{
            if(mediaSourceMenu!=menu)return;
            mediaSourceMenu=null;IsMediaSourceSelectorOpen=false;interactions.Set(menu,false);
            collapse.Stop();
            if(!closing && settings.CollapseOnLeave && !pinned && !Capsule.IsMouseOver)collapse.Start();
        };
        IsMediaSourceSelectorOpen=true;interactions.Set(menu,true);collapse.Stop();hover.Stop();SetExpanded(true);
        try{menu.IsOpen=true;}
        catch{mediaSourceMenu=null;IsMediaSourceSelectorOpen=false;interactions.Set(menu,false);if(settings.CollapseOnLeave&&!pinned&&!Capsule.IsMouseOver)collapse.Start();throw;}
        await System.Threading.Tasks.Task.CompletedTask;
    }
    void InteractionChanged()
    {
        collapse.Stop();hover.Stop();
        if(interactions.IsActive)
        {
            if(showingToast){showingToast=false;toastTimer.Stop();}
            if(!expanded)SetExpanded(true);else RenderState();
        }
        else if(!closing && settings.CollapseOnLeave && !pinned && !Capsule.IsMouseOver)collapse.Start();
    }
    public void SetSettingsInteraction(object owner,bool active)=>interactions.Set(owner,active);
    void SettingsClick(object sender, RoutedEventArgs args) => SettingsRequested?.Invoke();
    void VolumeStateChanged(int level, bool muted, bool available)
    {
        updatingVolume = true;
        SystemVolume.IsEnabled = MuteButton.IsEnabled = available;
        SystemVolume.Value = level;
        MuteButton.Opacity = muted ? .45 : 1;
        updatingVolume = false;
    }
    void BeginVolumeDrag(object sender,MouseButtonEventArgs e)
    {
        if(!SystemVolume.IsEnabled)return;
        interactions.Set(SystemVolume,true);
        if(!Mouse.Capture(SystemVolume,CaptureMode.Element)){interactions.Set(SystemVolume,false);return;}
        SetVolumeFromPointer(e);e.Handled=true;
    }
    void MoveVolumeDrag(object sender,MouseEventArgs e)
    {
        if(!SystemVolume.IsMouseCaptured)return;
        if(e.LeftButton!=MouseButtonState.Pressed)return;
        SetVolumeFromPointer(e);e.Handled=true;
    }
    void EndVolumeDrag(object sender,MouseButtonEventArgs e)
    {
        if(!SystemVolume.IsMouseCaptured)return;
        SetVolumeFromPointer(e);Mouse.Capture(null);interactions.Set(SystemVolume,false);e.Handled=true;
    }
    void SetVolumeFromPointer(MouseEventArgs e)
    {
        double fraction=Math.Clamp(e.GetPosition(SystemVolume).X/Math.Max(1,SystemVolume.ActualWidth),0,1);
        SystemVolume.Value=SystemVolume.Minimum+fraction*(SystemVolume.Maximum-SystemVolume.Minimum);
    }
    void VolumeSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> args)
    {
        if (!updatingVolume && volume != null && SystemVolume.IsEnabled) volume.SetLevel((int)Math.Round(args.NewValue));
    }
    void MuteClick(object sender, RoutedEventArgs args) => volume?.ToggleMute();
    public void ShowVolume(int value, bool mute)
    {
        if (!settings.VolumeEnabled || expanded || HasInteractiveOverlay || SystemVolume.IsMouseCaptureWithin) return;
        ToastText.Text = mute ? "Звук выключен" : $"Громкость  {value}%";
        VolumeBar.Visibility = Visibility.Visible;
        Motion.To(VolumeBar, System.Windows.Controls.Primitives.RangeBase.ValueProperty, mute ? 0 : value, Duration);
        ShowToast();
    }
    public void ShowMessage(string text) { ToastText.Text = text; VolumeBar.Visibility = Visibility.Collapsed; ShowToast(); }
    void ShowToast() { if(HasInteractiveOverlay)return; showingToast = true; hover.Stop(); RenderState(); toastTimer.Stop(); toastTimer.Start(); }
    void ToastTick(object? sender, EventArgs args)
    {
        toastTimer.Stop(); showingToast = false;
        SetExpanded(pinned || Capsule.IsMouseOver || (!settings.CollapseOnLeave && expanded));
    }
    void MediaChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(vm.UseClock) or nameof(vm.SelectedTab)) RenderState();
        if (args.PropertyName == nameof(vm.Playing)) UpdateActivity();
        if (args.PropertyName == nameof(vm.Artwork)) UpdateAccent();

    }
    void UpdateAccent()
    {
        var brush = new SolidColorBrush(AlbumColor.Accent(settings,AlbumColor.Palette(vm.Artwork)));
        ArtistText.Foreground = brush; ElapsedText.Foreground=brush; RemainingText.Foreground=brush; SpeakerPath.Stroke=brush; ProgressWave.Accent = brush; PlayIcon.Fill=brush;
        GlassLayer.Configure(settings,vm.Artwork);
        RibbonLayer.Configure(settings,vm.Artwork,vm.Playing);
        EffectsLayer.Visibility=settings.MediaVisualizer=="Neon Ribbon"?Visibility.Collapsed:Visibility.Visible;
        EffectsLayer.Configure(settings,vm.Artwork,expanded,vm.HasMediaSession && !showingToast && (expanded ? vm.SelectedTab=="Music" : !vm.UseClock),vm.Playing);
        foreach (var bar in new[] { Bar0, Bar1, Bar2, Bar3 }) bar.Background = brush;
    }
    void UpdateActivity()
    {
        if (ProgressWave == null) return;
        GlassLayer.Configure(settings,vm.Artwork);
        RibbonLayer.Configure(settings,vm.Artwork,vm.Playing);
        EffectsLayer.Visibility=settings.MediaVisualizer=="Neon Ribbon"?Visibility.Collapsed:Visibility.Visible;
        EffectsLayer.Configure(settings,vm.Artwork,expanded,vm.HasMediaSession && !showingToast && (expanded ? vm.SelectedTab=="Music" : !vm.UseClock),vm.Playing);
        Meter.Visibility=settings.MediaVisualizer=="Classic"?Visibility.Visible:Visibility.Collapsed;
        ProgressWave.SetMotion(vm.Playing && settings.Animations && !settings.ReduceMotion && SystemParameters.ClientAreaAnimation && settings.MediaVisualizer=="Classic" && expanded && !showingToast && vm.SelectedTab == "Music");
        int i = 0;
        foreach (var bar in new[] { Bar0, Bar1, Bar2, Bar3 })
        {
            bar.BeginAnimation(HeightProperty, null); bar.Height = vm.Playing ? 7 + i % 2 * 4 : 3;
            if (vm.Playing && settings.Animations && !settings.ReduceMotion && SystemParameters.ClientAreaAnimation && settings.MediaVisualizer=="Classic" && !expanded && !showingToast && !vm.UseClock)
                bar.BeginAnimation(HeightProperty, new DoubleAnimation(3 + i, 15 - i, TimeSpan.FromMilliseconds(360 + i * 115)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            i++;
        }
    }
    protected override void OnClosing(CancelEventArgs e) { if(!vm.Personal.Flush()) { e.Cancel=true; MessageBox.Show(this, vm.Personal.Status + "\nПовторите сохранение перед выходом.", "NotchFlow"); } base.OnClosing(e); }
    protected override void OnClosed(EventArgs e)
    {
        closing = true;LayoutUpdated-=BackdropLayout;IsVisibleChanged-=BackdropVisibility;nativeBackdrop?.Dispose();nativeBackdrop=null;audioAnalysis.Changed-=RibbonLayer.AcceptAudio;audioAnalysis.Changed-=EffectsLayer.AcceptAudio;audioAnalysis.Dispose(); interactions.Changed-=InteractionChanged; interactions.Clear(); if(mediaSourceMenu!=null)mediaSourceMenu.IsOpen=false; IsMediaSourceSelectorOpen=false; hover.Stop(); collapse.Stop(); toastTimer.Stop(); ProgressWave.SetMotion(false);
        if (volume != null) volume.StateChanged -= VolumeStateChanged;
        vm.PropertyChanged -= MediaChanged; SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        vm.Dispose(); base.OnClosed(e);
    }
}





