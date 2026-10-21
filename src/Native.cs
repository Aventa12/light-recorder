using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace LightRecorder {
 internal static class Native {
  [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr h, int id, uint modifiers, uint key);
  [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h, int id);
  [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr h);
  [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll",SetLastError=true)] internal static extern bool SetWindowDisplayAffinity(IntPtr h,uint affinity);
  [DllImport("user32.dll")] internal static extern bool GetWindowDisplayAffinity(IntPtr h,out uint affinity);
  [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
  internal delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int size);
  [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT value, int size);
  [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left,Top,Right,Bottom; }
  internal static bool VisibleWindowBounds(IntPtr h,out RECT rect) {
   if(DwmGetWindowAttribute(h,9,out rect,Marshal.SizeOf(typeof(RECT)))==0)return true;
   return GetWindowRect(h,out rect);
  }
  internal static List<WindowItem> Windows(IntPtr own) {
   var result=new List<WindowItem>();
   EnumWindows(delegate(IntPtr h,IntPtr l) {
    var text=new StringBuilder(512); GetWindowText(h,text,text.Capacity);
    int cloaked=0; DwmGetWindowAttribute(h,14,out cloaked,4);
    bool internalWindow=false;
    foreach(Form f in Application.OpenForms)if((f is MainForm||f is ExcludedOverlay||f is RegionPicker)&&f.IsHandleCreated&&f.Handle==h){internalWindow=true;break;}
    if(h!=own && !internalWindow && IsWindowVisible(h) && !IsIconic(h) && cloaked==0 && text.Length>0)
     result.Add(new WindowItem {Handle=h,Title=text.ToString()});
    return true;
   },IntPtr.Zero);
   return result;
  }
 }
 internal class WindowItem {
  public IntPtr Handle; public string Title;
  public override string ToString() {return Title;}
 }
 internal sealed class RegionPicker : Form {
  Point start; Rectangle selection; bool dragging;
  public Rectangle Selected {get {return new Rectangle(selection.X+Left,selection.Y+Top,selection.Width,selection.Height);}}
  public RegionPicker(Rectangle bounds) {
   FormBorderStyle=FormBorderStyle.None; StartPosition=FormStartPosition.Manual; Bounds=bounds;
   TopMost=true; DoubleBuffered=true; BackColor=Color.FromArgb(16,24,35); Opacity=.52;
   Cursor=Cursors.Cross; KeyPreview=true; ShowInTaskbar=false;
   KeyDown+=delegate(object s,KeyEventArgs e) {if(e.KeyCode==Keys.Escape) {DialogResult=DialogResult.Cancel;Close();}};
  }
  protected override void OnMouseDown(MouseEventArgs e) {if(e.Button!=MouseButtons.Left)return; start=e.Location;dragging=true;Capture=true;}
  protected override void OnMouseMove(MouseEventArgs e) {
   if(!dragging)return;
   int x=Math.Max(0,Math.Min(ClientSize.Width,e.X)),y=Math.Max(0,Math.Min(ClientSize.Height,e.Y));
   selection=Rectangle.FromLTRB(Math.Min(start.X,x),Math.Min(start.Y,y),Math.Max(start.X,x),Math.Max(start.Y,y));Invalidate();
  }
  protected override void OnMouseUp(MouseEventArgs e) {
   if(!dragging)return;dragging=false;Capture=false;
   if(selection.Width<32||selection.Height<32) {selection=Rectangle.Empty;Invalidate();return;}
   DialogResult=DialogResult.OK;Close();
  }
  protected override void OnPaint(PaintEventArgs e) {
   base.OnPaint(e);
   using(var p=new Pen(Color.FromArgb(64,255,191),3)) e.Graphics.DrawRectangle(p,selection);
   string text=selection.IsEmpty?"拖动选择录制区域 · Esc 取消":"录制区域  "+selection.Width+" × "+selection.Height;
   using(var f=new Font("Microsoft YaHei UI",16,FontStyle.Bold)) e.Graphics.DrawString(text,f,Brushes.White,28,28);
  }
 }
}
