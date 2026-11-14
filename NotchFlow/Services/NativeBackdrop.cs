using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using NotchFlow.Controls;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using WinRT;

namespace NotchFlow.Services;

// Experimental native backing surface; the WPF foreground remains the input window.
public sealed class NativeBackdrop : IDisposable
{
    readonly Window owner;
    readonly IslandSurface surface;
    HwndSource? source;
    // One dispatcher queue lives with the UI thread and is shared by successive windows.
    static Windows.System.DispatcherQueueController? queue;
    Compositor? compositor;
    DesktopWindowTarget? target;
    SpriteVisual? visual;
    CompositionBackdropBrush? brush;
    bool updating,enabled;
    Rect lastBounds, lastHostBounds; double lastRadius=-1; bool lastNotch;
    public int Result {get;private set;}
    public bool Available => source!=null&&Result>=0;
    public NativeBackdrop(Window owner,IslandSurface surface){this.owner=owner;this.surface=surface;}
    public void Enable(bool value)
    {
        enabled=value;
        if(!value){if(source!=null)ShowWindow(source.Handle,0);return;}
        if(source==null)
        {
            var parameters=new HwndSourceParameters("NotchFlow native backdrop")
            {
                WindowStyle=unchecked((int)0x80000000),
                ExtendedWindowStyle=0x08000000|0x80|0x20|0x00200000,
                Width=1,Height=1,UsesPerPixelOpacity=false,HwndSourceHook=Hook
            };
            source=new HwndSource(parameters);
            if(Windows.System.DispatcherQueue.GetForCurrentThread()==null)
            {
                var options=new QueueOptions{Size=Marshal.SizeOf<QueueOptions>(),ThreadType=2,ApartmentType=2};
                Marshal.ThrowExceptionForHR(CreateDispatcherQueueController(options,out var pointer));
                queue=MarshalInterface<Windows.System.DispatcherQueueController>.FromAbi(pointer);Marshal.Release(pointer);
            }
            compositor=new Compositor();
            var interop=compositor.As<ICompositorDesktopInterop>();
            interop.CreateDesktopWindowTarget(source.Handle,false,out var targetPointer);
            target=MarshalInterface<DesktopWindowTarget>.FromAbi(targetPointer);Marshal.Release(targetPointer);
            int enabledBrush=1;
            Result=DwmSetWindowAttribute(source.Handle,17,ref enabledBrush,4);
            brush=compositor.CreateHostBackdropBrush();
            visual=compositor.CreateSpriteVisual();visual.Brush=brush;
            target.Root=visual;
        }
        Sync(true);
    }
    IntPtr Hook(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled)
    {
        if(message==0x84){handled=true;return new IntPtr(-1);}
        if(message==0x21){handled=true;return new IntPtr(3);}
        return IntPtr.Zero;
    }
    public void Sync(bool force=false)
    {
        if(updating||source==null)return;
        if(!enabled||!Available||!owner.IsVisible||owner.WindowState==WindowState.Minimized){ShowWindow(source.Handle,0);return;}
        if(surface.ActualWidth<2||surface.ActualHeight<2)return;
        updating=true;
        try
        {
            var origin=surface.PointToScreen(new Point(0,0));
            var end=surface.PointToScreen(new Point(surface.ActualWidth,surface.ActualHeight));
            var bounds=new Rect(origin,end);
            // Keep the native host and brush allocation stable throughout capsule animation.
            // Resizing this HWND every frame leaves newly exposed pixels without backdrop
            // until the compositor catches up. Only its region should follow the capsule.
            var hostOrigin=owner.PointToScreen(new Point(0,0));
            var hostEnd=owner.PointToScreen(new Point(owner.ActualWidth,owner.ActualHeight));
            var hostBounds=new Rect(hostOrigin,hostEnd);
            if(!force && bounds==lastBounds && hostBounds==lastHostBounds && lastRadius==surface.CornerRadius.TopLeft && lastNotch==surface.IsNotch)return;
            bool hostChanged=hostBounds!=lastHostBounds;
            lastBounds=bounds;lastHostBounds=hostBounds;lastRadius=surface.CornerRadius.TopLeft;lastNotch=surface.IsNotch;
            double sx=(end.X-origin.X)/surface.ActualWidth,sy=(end.Y-origin.Y)/surface.ActualHeight;
            double offsetX=origin.X-hostOrigin.X,offsetY=origin.Y-hostOrigin.Y;
            var flattened=surface.Outline(surface.ActualWidth,surface.ActualHeight).GetFlattenedPathGeometry(.2,ToleranceType.Absolute);
            var points=new List<NativePoint>();
            foreach(var figure in flattened.Figures)
            {
                points.Add(new NativePoint(figure.StartPoint,sx,sy,offsetX,offsetY));
                foreach(var segment in figure.Segments)
                {
                    if(segment is PolyLineSegment poly)foreach(var point in poly.Points)points.Add(new NativePoint(point,sx,sy,offsetX,offsetY));
                    if(segment is LineSegment line)points.Add(new NativePoint(line.Point,sx,sy,offsetX,offsetY));
                }
            }
            if(visual!=null&&hostChanged)visual.Size=new System.Numerics.Vector2((float)hostBounds.Width,(float)hostBounds.Height);
            var region=CreatePolygonRgn(points.ToArray(),points.Count,1);
            if(region==IntPtr.Zero)throw new InvalidOperationException("Cannot create backdrop clip.");
            if(SetWindowRgn(source.Handle,region,true)==0){DeleteObject(region);ShowWindow(source.Handle,0);Result=unchecked((int)0x80004005);return;}
            if(!SetWindowPos(source.Handle,new WindowInteropHelper(owner).Handle,(int)Math.Round(hostOrigin.X),(int)Math.Round(hostOrigin.Y),(int)Math.Ceiling(hostBounds.Width),(int)Math.Ceiling(hostBounds.Height),0x10|0x40|(hostChanged?0u:0x3u)))
            {
                ShowWindow(source.Handle,0);
                Result=unchecked((int)0x80004005);
            }
        }
        finally{updating=false;}
    }
    public void Dispose(){target?.Dispose();visual?.Dispose();brush?.Dispose();compositor?.Dispose();source?.Dispose();source=null;}
    [ComImport,Guid("29E691FA-4567-4DCA-B319-D0F207EB6807"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ICompositorDesktopInterop
    {
        void CreateDesktopWindowTarget(IntPtr hwnd,[MarshalAs(UnmanagedType.Bool)] bool topmost,out IntPtr target);
        void EnsureOnThread(uint thread);
    }
    [StructLayout(LayoutKind.Sequential)] struct QueueOptions{public int Size,ThreadType,ApartmentType;}
    [DllImport("CoreMessaging.dll")] static extern int CreateDispatcherQueueController(QueueOptions options,out IntPtr controller);
    [StructLayout(LayoutKind.Sequential)] struct NativePoint
    {
        public int X,Y;
        public NativePoint(Point value,double sx,double sy,double offsetX,double offsetY){X=(int)Math.Round(value.X*sx+offsetX);Y=(int)Math.Round(value.Y*sy+offsetY);}
    }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd,int command);
    [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr hwnd,IntPtr region,bool redraw);
    [DllImport("gdi32.dll")] static extern IntPtr CreatePolygonRgn(NativePoint[] points,int count,int mode);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr value);
}
