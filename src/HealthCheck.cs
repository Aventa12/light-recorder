using System;
using System.IO;
using System.Runtime.InteropServices;
using ScreenRecorderLib;

namespace LightRecorder {
 internal static class HealthCheck {
  [DllImport("mfplat.dll")] static extern int MFStartup(int version,int flags);
  [DllImport("mfplat.dll")] static extern int MFShutdown();
  internal static string Check() {
   int hr=MFStartup(0x20070,0);if(hr<0)Marshal.ThrowExceptionForHR(hr);
   MFShutdown();
   // Loading the actual recording core resolves its C++ dependencies, not just .NET.
   var displays=Recorder.GetDisplays();
   using(var icon=System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath)) {
    if(icon==null)throw new InvalidOperationException("无法读取程序图标。");
   }
   Directory.CreateDirectory(AppPaths.ConfigDirectory);
   string probe=Path.Combine(AppPaths.ConfigDirectory,"health-"+Guid.NewGuid().ToString("N")+".tmp");
   File.WriteAllText(probe,"ok");File.Delete(probe);
   Directory.CreateDirectory(AppPaths.DefaultVideoDirectory);
   return "PASS: recording core and C++ dependencies loaded; Media Foundation initialized; icon loaded; user settings writable; video directory ready. Displays="+displays.Count;
  }
 }
 internal static class AppPaths {
  internal static bool IsInstalled {get{return File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"installed.flag"));}}
  internal static string ConfigDirectory {get{return IsInstalled?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LightRecorder"):AppDomain.CurrentDomain.BaseDirectory;}}
  internal static string DefaultVideoDirectory {get{return IsInstalled?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),"轻录"):Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"录制视频");}}
 }
}
