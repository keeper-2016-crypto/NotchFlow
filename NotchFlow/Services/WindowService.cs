using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using NotchFlow.Configuration;
using Forms=System.Windows.Forms;
namespace NotchFlow.Services;
public static class WindowService
{
 [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd,int index);
 [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd,int index,int value);
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd,IntPtr insert,int x,int y,int cx,int cy,uint flags);
 [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT pt,uint flags);
 [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint x,out uint y);
 [StructLayout(LayoutKind.Sequential)] struct POINT { public int X,Y; }
 public static void Initialize(Window window) {var hwnd=new WindowInteropHelper(window).Handle;SetWindowLong(hwnd,-20,GetWindowLong(hwnd,-20)|0x08000000|0x80);}
 public static void SetInteractive(Window window,bool interactive) { var hwnd=new WindowInteropHelper(window).Handle;if(hwnd==IntPtr.Zero)return;int flags=GetWindowLong(hwnd,-20);SetWindowLong(hwnd,-20,interactive?flags&~0x08000000:flags|0x08000000); }
 public static double MaximumScale(AppSettings settings,double width,double height) {
  var screen=Forms.Screen.AllScreens.FirstOrDefault(x=>x.DeviceName==settings.Monitor)??Forms.Screen.PrimaryScreen??Forms.Screen.AllScreens[0];
  var r=screen.WorkingArea;var monitor=MonitorFromPoint(new POINT{X=r.Left+1,Y=r.Top+1},2);GetDpiForMonitor(monitor,0,out uint dpi,out _);double factor=(dpi==0?96:dpi)/96d;
  return Math.Max(.25,Math.Min((r.Width/factor-12)/width,(r.Height/factor-12)/height));
 }
 public static void Position(Window window,AppSettings settings) {
  var screen=Forms.Screen.AllScreens.FirstOrDefault(x=>x.DeviceName==settings.Monitor)??Forms.Screen.PrimaryScreen??Forms.Screen.AllScreens[0];
  var r=settings.AttachToTop?screen.Bounds:screen.WorkingArea;var monitor=MonitorFromPoint(new POINT{X=r.Left+1,Y=r.Top+1},2);GetDpiForMonitor(monitor,0,out uint dpi,out _);double factor=(dpi==0?96:dpi)/96d;
  int w=(int)Math.Ceiling(window.Width*factor),h=(int)Math.Ceiling(window.Height*factor);
  double center=settings.Position=="Left"?r.Left+w/2d:settings.Position=="Right"?r.Right-w/2d:r.Left+r.Width/2d;
  int x=(int)(center-w/2d+settings.OffsetX*factor),y=(int)(r.Top+(settings.AttachToTop?0:settings.OffsetY*factor));
  x=Math.Clamp(x,r.Left,Math.Max(r.Left,r.Right-w));y=Math.Clamp(y,r.Top,Math.Max(r.Top,r.Bottom-h));
  SetWindowPos(new WindowInteropHelper(window).Handle,settings.AlwaysOnTop?new IntPtr(-1):new IntPtr(-2),x,y,w,h,0x10);
 }
}


