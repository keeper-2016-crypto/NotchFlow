using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NotchFlow.Configuration;
using NotchFlow.Models;

namespace NotchFlow.Services;
public sealed record ShelfFileInfo(string Path,string Name,string Type,long? Size,bool Available,ImageSource? Icon)
{
 public string Detail => !Available ? "Недоступно — файл перемещён или нет доступа" : Type + (Size is long n ? " · " + FormatSize(n) : "");
 public static string FormatSize(long n)=> n>=1073741824 ? $"{n/1073741824d:0.#} ГБ" : n>=1048576 ? $"{n/1048576d:0.#} МБ" : n>=1024 ? $"{n/1024d:0.#} КБ" : $"{n} Б";
}
public sealed class FileShelfService
{
 readonly string path;
 readonly SemaphoreSlim storage=new(1,1);
 // Unresponsive network paths occupy at most two workers, never the UI or an unbounded queue.
 static readonly SemaphoreSlim metadataSlots=new(2,2);
 public FileShelfService(string? directory=null)=>path=System.IO.Path.Combine(directory??Log.DirectoryPath,"shelf.json");
 public static string? Normalize(string? value)
 {
  try { if(string.IsNullOrWhiteSpace(value)||!System.IO.Path.IsPathFullyQualified(value)||value.StartsWith(@"\\.\",StringComparison.Ordinal))return null;return System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(value)); }
  catch(Exception e) when(e is ArgumentException or NotSupportedException or PathTooLongException){return null;}
 }
 public async Task<string[]> LoadAsync()
 {
  await storage.WaitAsync().ConfigureAwait(false);
  try {return await Task.Run(()=>{
   if(!File.Exists(path))return Array.Empty<string>();
   var doc=AtomicJson.Read(path,DataJsonContext.Default.ShelfDocument);
   return (doc.Paths??new()).Select(Normalize).Where(p=>p!=null).Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
  }).ConfigureAwait(false);}
  finally{storage.Release();}
 }
 public async Task SaveAsync(IEnumerable<string> paths)
 {
  var snapshot=new ShelfDocument{Paths=paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList()};
  await storage.WaitAsync().ConfigureAwait(false);
  try{await Task.Run(()=>AtomicJson.Write(path,snapshot,DataJsonContext.Default.ShelfDocument)).ConfigureAwait(false);}finally{storage.Release();}
 }
 public static ShelfFileInfo Unavailable(string path)=>new(path,System.IO.Path.GetFileName(path) is {Length:>0} n?n:path,"Файл",null,false,null);
 public async Task<ShelfFileInfo> InspectAsync(string path,CancellationToken token=default)
 {
  if(!await metadataSlots.WaitAsync(0,token).ConfigureAwait(false))return Unavailable(path);
  var worker=Task.Run(()=>{try{return Inspect(path);}catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException or COMException){return Unavailable(path);}finally{metadataSlots.Release();}});
  try{return await worker.WaitAsync(TimeSpan.FromMilliseconds(900),token).ConfigureAwait(false);}catch(TimeoutException){return Unavailable(path);}
 }
 static ShelfFileInfo Inspect(string path)
 {
  var attrs=File.GetAttributes(path);bool folder=(attrs&FileAttributes.Directory)!=0;
  var info=new SHFILEINFO();ImageSource? icon=null;
  // USEFILEATTRIBUTES avoids shell extensions and opening remote files just to obtain an icon.
  if(SHGetFileInfo(folder?"folder":System.IO.Path.GetExtension(path),(uint)(folder?16:128),ref info,(uint)Marshal.SizeOf<SHFILEINFO>(),0x100|0x4000|0x10)!=IntPtr.Zero && info.Icon!=IntPtr.Zero)
  {try{var bitmap=ReadLargeIcon(info.Index,info.Icon);bitmap.Freeze();icon=bitmap;}finally{DestroyIcon(info.Icon);}}
  return new(path,System.IO.Path.GetFileName(path) is {Length:>0} n?n:path,folder?"Папка":System.IO.Path.GetExtension(path).TrimStart('.').ToUpperInvariant() is {Length:>0} ext?ext:"Файл",folder?null:new FileInfo(path).Length,true,icon);
 }
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct SHFILEINFO { public IntPtr Icon;public int Index;public uint Attributes;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string DisplayName;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)]public string TypeName; }

 static BitmapSource ReadLargeIcon(int index,IntPtr fallback)
 {
  var iid=new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
  IntPtr large=IntPtr.Zero;
  if(SHGetImageList(2,ref iid,out var list)>=0){
   try {var getIcon=Marshal.GetDelegateForFunctionPointer<GetListIcon>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(list),10*IntPtr.Size));getIcon(list,index,1,out large);}
   finally{Marshal.Release(list);}
  }
  try{return Imaging.CreateBitmapSourceFromHIcon(large!=IntPtr.Zero?large:fallback,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());}
  finally{if(large!=IntPtr.Zero)DestroyIcon(large);}
 }
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int GetListIcon(IntPtr self,int index,uint flags,out IntPtr icon);
 [DllImport("shell32.dll",ExactSpelling=true)]static extern int SHGetImageList(int size,ref Guid iid,out IntPtr list);
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SHGetFileInfo(string path,uint attributes,ref SHFILEINFO info,uint size,uint flags);
 [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr icon);
}
