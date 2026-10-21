using System;
using System.Drawing;
using System.Windows.Forms;

namespace LightRecorder {
 // WDA_EXCLUDEFROMCAPTURE removes the complete window, including its child controls.
 internal class ExcludedOverlay : Form {
  public bool CaptureExcluded {get;private set;}
  public ExcludedOverlay() {
   FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;
   StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.Dpi;
  }
  protected override bool ShowWithoutActivation {get{return true;}}
  protected override CreateParams CreateParams {
   get {var p=base.CreateParams;p.ExStyle|=0x08000080;return p;}
  }
  protected override void OnHandleCreated(EventArgs e) {
   base.OnHandleCreated(e);uint affinity;
   CaptureExcluded=Native.SetWindowDisplayAffinity(Handle,0x11)&&Native.GetWindowDisplayAffinity(Handle,out affinity)&&affinity==0x11;
  }
  protected override void WndProc(ref Message m) {
   if(m.Msg==0x21){m.Result=new IntPtr(3);return;} // Keep the recording target active.
   base.WndProc(ref m);
  }
 }
 internal sealed class CaptureToolbar : ExcludedOverlay {
  readonly Label title,elapsed,detail; readonly Button pause,stop;
  readonly Font countdownFont,timeFont;
  public event Action PauseRequested,StopRequested;
  public CaptureToolbar() {
   ClientSize=new Size(360,112);BackColor=Color.FromArgb(24,38,49);ForeColor=Color.White;
   Font=new Font("Microsoft YaHei UI",9);Padding=new Padding(12);
   countdownFont=new Font(Font.FontFamily,23,FontStyle.Bold);timeFont=new Font(Font.FontFamily,19,FontStyle.Bold);
   title=new Label {Location=new Point(15,10),Size=new Size(165,24),Text="准备录制",ForeColor=Color.FromArgb(90,224,184)};
   elapsed=new Label {Location=new Point(14,34),Size=new Size(172,39),Font=countdownFont,Text="3"};
   detail=new Label {Location=new Point(15,85),Size=new Size(330,20),AutoEllipsis=true,ForeColor=Color.FromArgb(186,203,212)};
   pause=MakeButton("暂停",190,37,72);stop=MakeButton("停止",271,37,74);
   pause.Click+=delegate{var h=PauseRequested;if(h!=null)h();};
   stop.Click+=delegate{var h=StopRequested;if(h!=null)h();};
   Controls.AddRange(new Control[]{title,elapsed,detail,pause,stop});
   AccessibleName="轻录录制控制条";
  }
  Button MakeButton(string text,int x,int y,int w) {
   var b=new Button {Text=text,Location=new Point(x,y),Size=new Size(w,35),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(44,63,76),ForeColor=Color.White,Cursor=Cursors.Hand};
   b.FlatAppearance.BorderSize=0;return b;
  }
  public void Place(Screen screen) {
   var area=screen.WorkingArea;Location=new Point(area.Right-Width-20,area.Bottom-Height-20);
  }
  public void ShowCountdown(int seconds,string target) {
   title.Text="即将开始录制";elapsed.Font=countdownFont;elapsed.Text=Math.Max(0,seconds).ToString();detail.Text=target;
   pause.Visible=false;stop.Text="取消";stop.Enabled=true;
  }
  public void ShowStatus(AppState state,string time,string target) {
   title.Text=state==AppState.Paused?"● 已暂停":state==AppState.Saving?"正在保存视频…":state==AppState.Starting?"正在准备录制…":"● 正在录制";
   elapsed.Font=timeFont;elapsed.Text=time;detail.Text=target;
   pause.Visible=true;pause.Text=state==AppState.Paused?"继续":"暂停";stop.Text="停止";
   pause.Enabled=stop.Enabled=state==AppState.Recording||state==AppState.Paused;
  }
  public void SetBusy(){pause.Enabled=false;stop.Enabled=false;}
  protected override void Dispose(bool disposing) {base.Dispose(disposing);if(disposing){countdownFont.Dispose();timeFont.Dispose();}}
 }
 internal sealed class WindowOutline : ExcludedOverlay {
  public WindowOutline(){BackColor=Color.FromArgb(20,189,139);}
  protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x20;return p;}}
  public void Follow(IntPtr window) {
   Native.RECT rect;
   if(!Native.IsWindow(window)||Native.IsIconic(window)||!Native.VisibleWindowBounds(window,out rect)){Hide();return;}
   var bounds=Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom);
   if(bounds.Width<16||bounds.Height<16){Hide();return;}
   if(Bounds!=bounds) {
    Bounds=bounds;var ring=new Region(new Rectangle(0,0,Width,Height));ring.Exclude(new Rectangle(5,5,Width-10,Height-10));
    var old=Region;Region=ring;if(old!=null)old.Dispose();
   }
   if(!Visible)Show();
  }
 }
}
