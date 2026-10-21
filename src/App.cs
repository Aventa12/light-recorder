using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Forms;
using ScreenRecorderLib;

namespace LightRecorder {
 [DataContract] internal class Settings {
  [DataMember] public bool SystemAudio=true;
  [DataMember] public bool Microphone=false;
  [DataMember] public bool Highlight=true;
  [DataMember] public bool Minimize=true;
  [DataMember] public bool Hardware=true;
  [DataMember] public int Fps=30;
  [DataMember] public int MaxHeight=1080;
  [DataMember] public int Countdown=3;
  [DataMember] public string Folder;
  static string Config {get {return Path.Combine(AppPaths.ConfigDirectory,"settings.json");}}
  public static Settings Load() {
   try {using(var f=File.OpenRead(Config))return (Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(f);}catch{return new Settings();}
  }
  public void Save() {
   Directory.CreateDirectory(AppPaths.ConfigDirectory);
   string temp=Config+".tmp";
   using(var f=File.Create(temp))new DataContractJsonSerializer(typeof(Settings)).WriteObject(f,this);
   if(File.Exists(Config))File.Replace(temp,Config,null);else File.Move(temp,Config);
  }
 }
 internal enum AppState {Ready,Countdown,Starting,Recording,Paused,Saving}
 internal sealed class MainForm : Form {
  readonly Color ink=Color.FromArgb(31,45,58),green=Color.FromArgb(18,136,105),muted=Color.FromArgb(111,125,137);
  ComboBox mode,monitor,windows,fps,resolution,countdown; CheckBox system,mic,highlight,hide,hardware;
  Button choose,refresh,start,pause,stop,folderButton; TextBox folder; Label target,status,clock,notice;
  Panel preferences; NotifyIcon tray; System.Windows.Forms.Timer timer;
  Settings settings; RecorderEngine engine; AppState state; Rectangle crop; CaptureRequest pending;
  DateTime deadline; string lastFile; bool exitAfterSave,hotkeysReady=true;
  CaptureToolbar toolbar;WindowOutline outline;ToolTip targetTip=new ToolTip();string captureTarget;

  public MainForm() {
   Text="轻录 · 屏幕录制"; Font=new Font("Microsoft YaHei UI",9); BackColor=Color.FromArgb(246,248,250);
   ForeColor=ink; ClientSize=new Size(680,660); FormBorderStyle=FormBorderStyle.FixedSingle;
   MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen; AutoScaleMode=AutoScaleMode.Dpi;
   Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath); settings=Settings.Load();
   var header=LabelAt("轻录",28,20,200,45,26,FontStyle.Bold);
   LabelAt("把屏幕里的精彩，轻轻留下。",30,72,600,25,10,FontStyle.Regular).ForeColor=muted;
   LabelAt("本地保存   /   无水印   /   轻量录屏",400,33,250,26,9,FontStyle.Regular).ForeColor=green;
   preferences=new Panel {Location=new Point(24,112),Size=new Size(632,382),BackColor=Color.White}; Controls.Add(preferences);
   LabelIn("录制范围",18,16,90,24);
   mode=ComboIn(new object[]{"全屏录制","窗口录制","区域录制"},18,45,145);mode.SelectedIndex=0;
   monitor=ComboIn(new object[0],175,45,315);
   foreach(var screen in Screen.AllScreens)monitor.Items.Add(screen.DeviceName+"  ·  "+screen.Bounds.Width+" × "+screen.Bounds.Height+(screen.Primary?"  主屏":""));
   monitor.SelectedIndex=Array.FindIndex(Screen.AllScreens,s=>s.Primary);
   windows=ComboIn(new object[0],175,45,315);windows.Visible=false;
   choose=ButtonIn("选择区域",502,44,110,31);choose.Visible=false;choose.Click+=delegate{PickRegion();};
   refresh=ButtonIn("定位窗口",502,44,110,31);refresh.Visible=false;refresh.Click+=delegate{LocateWindow();};
   target=LabelIn("录制所选显示器的完整画面",18,82,594,36);target.ForeColor=muted;target.AutoEllipsis=true;
   windows.DropDownWidth=620;windows.DropDown+=delegate{RefreshWindows();};
   windows.SelectedIndexChanged+=delegate{if(mode.SelectedIndex==1)DescribeWindow();};
   LabelIn("声音与鼠标",18,124,160,24);
   system=CheckIn("电脑声音",18,155,130,settings.SystemAudio);
   mic=CheckIn("麦克风",165,155,110,settings.Microphone);
   highlight=CheckIn("鼠标点击高亮",310,155,175,settings.Highlight);
   LabelIn("清晰度",18,204,85,24);resolution=ComboIn(new object[]{"1080p · 推荐","720p · 更省空间","原始分辨率"},18,233,190);
   resolution.SelectedIndex=settings.MaxHeight==720?1:settings.MaxHeight==int.MaxValue?2:0;
   LabelIn("帧率",225,204,85,24);fps=ComboIn(new object[]{"30 帧 · 日常录制","60 帧 · 动态画面"},225,233,185);fps.SelectedIndex=settings.Fps==60?1:0;
   LabelIn("开始倒计时",428,204,150,24);countdown=ComboIn(new object[]{"不倒计时","3 秒","5 秒"},428,233,184);countdown.SelectedIndex=settings.Countdown==0?0:settings.Countdown==5?2:1;
   hide=CheckIn("开始后最小化到托盘",18,284,255,settings.Minimize);
   hardware=CheckIn("优先使用硬件编码",310,284,270,settings.Hardware);
   LabelIn("保存位置",18,330,90,28);
   folder=new TextBox {Location=new Point(105,330),Size=new Size(397,27),ReadOnly=true,BorderStyle=BorderStyle.FixedSingle};preferences.Controls.Add(folder);
   folder.Text=string.IsNullOrWhiteSpace(settings.Folder)?AppPaths.DefaultVideoDirectory:settings.Folder;
   folderButton=ButtonIn("更改",514,328,98,31);folderButton.Click+=delegate{using(var d=new FolderBrowserDialog {Description="选择视频保存位置",SelectedPath=folder.Text})if(d.ShowDialog(this)==DialogResult.OK)folder.Text=d.SelectedPath;};
   clock=LabelAt("00:00:00",29,514,230,48,27,FontStyle.Bold);
   status=LabelAt("● 准备就绪",32,566,255,24,10,FontStyle.Regular);status.ForeColor=green;
   start=ButtonAt("●  开始录制",302,522,155,44);start.BackColor=green;start.ForeColor=Color.White;
   pause=ButtonAt("暂停",468,522,85,44);pause.Enabled=false;
   stop=ButtonAt("结束",564,522,85,44);stop.Enabled=false;
   start.Click+=delegate{BeginCapture();};pause.Click+=delegate{TogglePause();};stop.Click+=delegate{StopCapture();};
   notice=LabelAt("Ctrl + Alt + F9 开始 / 暂停 / 继续     F10 结束（同样按 Ctrl + Alt）",30,611,620,25,8,FontStyle.Regular);notice.ForeColor=muted;
   var open=new LinkLabel {Text="打开视频文件夹",Location=new Point(500,578),Size=new Size(150,25),LinkColor=green};Controls.Add(open);
   open.LinkClicked+=delegate{OpenFolder();};
   tray=new NotifyIcon {Icon=Icon,Text="轻录 · 准备就绪",Visible=true};
   var menu=new ContextMenuStrip();
   menu.Items.Add("显示主窗口",null,delegate{Restore();});
   menu.Items.Add("开始 / 暂停 / 继续",null,delegate{if(state==AppState.Ready)BeginCapture();else TogglePause();});
   menu.Items.Add("结束录制",null,delegate{StopCapture();});
   menu.Items.Add("打开视频文件夹",null,delegate{OpenFolder();});
   menu.Items.Add(new ToolStripSeparator());menu.Items.Add("退出轻录",null,delegate{Close();});
   tray.ContextMenuStrip=menu;tray.DoubleClick+=delegate{Restore();};
   mode.SelectedIndexChanged+=delegate{UpdateMode();};
   monitor.SelectedIndexChanged+=delegate{crop=Rectangle.Empty;UpdateMode();};
   timer=new System.Windows.Forms.Timer {Interval=200};timer.Tick+=delegate{Tick();};timer.Start();
   FormClosing+=OnClosing;Resize+=delegate{if(WindowState==FormWindowState.Minimized)Hide();};
   Shown+=delegate {
    try {Recorder.SetExcludeFromCapture(Handle,true);}catch{}
    if(!hotkeysReady)notice.Text="快捷键被其他软件占用，可使用窗口或托盘按钮操作。";
   };
  }
  Label LabelAt(string text,int x,int y,int w,int h,float size,FontStyle style) {
   var l=new Label {Text=text,Location=new Point(x,y),Size=new Size(w,h),Font=new Font("Microsoft YaHei UI",size,style)};Controls.Add(l);return l;
  }
  Label LabelIn(string text,int x,int y,int w,int h) {var l=new Label {Text=text,Location=new Point(x,y),Size=new Size(w,h)};preferences.Controls.Add(l);return l;}
  ComboBox ComboIn(object[] items,int x,int y,int w) {var c=new ComboBox {Location=new Point(x,y),Size=new Size(w,30),DropDownStyle=ComboBoxStyle.DropDownList};c.Items.AddRange(items);preferences.Controls.Add(c);return c;}
  CheckBox CheckIn(string text,int x,int y,int w,bool value) {var c=new CheckBox {Text=text,Location=new Point(x,y),Size=new Size(w,28),Checked=value};preferences.Controls.Add(c);return c;}
  Button ButtonAt(string text,int x,int y,int w,int h) {var b=MakeButton(text,x,y,w,h);Controls.Add(b);return b;}
  Button ButtonIn(string text,int x,int y,int w,int h) {var b=MakeButton(text,x,y,w,h);preferences.Controls.Add(b);return b;}
  Button MakeButton(string text,int x,int y,int w,int h) {var b=new Button {Text=text,Location=new Point(x,y),Size=new Size(w,h),FlatStyle=FlatStyle.Flat,BackColor=Color.White,Cursor=Cursors.Hand};b.FlatAppearance.BorderColor=Color.FromArgb(218,226,232);return b;}
  void UpdateMode() {
   monitor.Visible=mode.SelectedIndex!=1;windows.Visible=mode.SelectedIndex==1;
   choose.Visible=mode.SelectedIndex==2;refresh.Visible=mode.SelectedIndex==1;
   if(mode.SelectedIndex==1) {RefreshWindows();DescribeWindow();}
   else {if(outline!=null)outline.Hide();target.ForeColor=muted;target.Text=mode.SelectedIndex==2?(crop.IsEmpty?"请先拖动选择区域，再开始录制。":"已选区域："+crop.Width+" × "+crop.Height):"录制所选显示器的完整画面";targetTip.SetToolTip(target,target.Text);}
  }
  void RefreshWindows() {
   var old=windows.SelectedItem as WindowItem;windows.Items.Clear();
   foreach(var w in Native.Windows(Handle))windows.Items.Add(w);
   if(windows.Items.Count>0)windows.SelectedIndex=0;
   if(old!=null)foreach(WindowItem w in windows.Items)if(w.Handle==old.Handle)windows.SelectedItem=w;
  }
  void DescribeWindow() {
   var window=windows.SelectedItem as WindowItem;
   target.ForeColor=green;
   target.Text=window==null?"没有可录制的窗口，请展开目标窗口后重新打开列表。":"已选窗口："+window.Title+"\n绿色边框标示当前录制目标；点击“定位窗口”可切到目标。";
   targetTip.SetToolTip(target,target.Text);
   if(window==null){if(outline!=null)outline.Hide();return;}
   if(outline==null)outline=new WindowOutline();outline.Follow(window.Handle);
  }
  void LocateWindow() {
   var window=windows.SelectedItem as WindowItem;
   if(window==null||!Native.IsWindow(window.Handle)){RefreshWindows();DescribeWindow();return;}
   Native.SetForegroundWindow(window.Handle);DescribeWindow();
  }
  void PickRegion() {
   Hide();using(var picker=new RegionPicker(Screen.AllScreens[Math.Max(0,monitor.SelectedIndex)].Bounds)) {
    if(picker.ShowDialog()==DialogResult.OK)crop=picker.Selected;
   }
   Restore();UpdateMode();
  }
  CaptureRequest Request() {
   var screen=Screen.AllScreens[Math.Max(0,monitor.SelectedIndex)];
   var request=new CaptureRequest {DeviceName=screen.DeviceName,Bounds=screen.Bounds,SystemAudio=system.Checked,Microphone=mic.Checked,Highlight=highlight.Checked,Hardware=hardware.Checked,Fps=fps.SelectedIndex==1?60:30,MaxHeight=resolution.SelectedIndex==1?720:resolution.SelectedIndex==2?int.MaxValue:1080};
   if(mode.SelectedIndex==1) {
    var window=windows.SelectedItem as WindowItem;
    if(window==null||!Native.IsWindow(window.Handle)||Native.IsIconic(window.Handle))throw new InvalidOperationException("请选择一个已展开的窗口。");
    Native.RECT rect;Native.GetWindowRect(window.Handle,out rect);
    request.Window=window.Handle;request.Bounds=Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom);
   }
   if(mode.SelectedIndex==2) {if(crop.IsEmpty)throw new InvalidOperationException("请先点击“选择区域”。");request.Crop=crop;}
   request.FilePath=Path.Combine(folder.Text,"轻录_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".mp4");
   return request;
  }
  void SaveSettings() {
   settings.SystemAudio=system.Checked;settings.Microphone=mic.Checked;settings.Highlight=highlight.Checked;
   settings.Minimize=hide.Checked;settings.Hardware=hardware.Checked;settings.Fps=fps.SelectedIndex==1?60:30;
   settings.MaxHeight=resolution.SelectedIndex==1?720:resolution.SelectedIndex==2?int.MaxValue:1080;
   settings.Countdown=countdown.SelectedIndex==0?0:countdown.SelectedIndex==2?5:3;settings.Folder=folder.Text;
   try {settings.Save();}catch(Exception e){notice.Text="设置未保存："+e.Message;}
  }
  void BeginCapture() {
   if(state!=AppState.Ready)return;
   try {
    pending=Request();Directory.CreateDirectory(folder.Text);
    using(var probe=new FileStream(pending.FilePath,FileMode.CreateNew,FileAccess.Write)){} File.Delete(pending.FilePath);
    captureTarget=mode.SelectedIndex==1?"窗口 · "+((WindowItem)windows.SelectedItem).Title:mode.SelectedIndex==2?"区域 · "+crop.Width+" × "+crop.Height:"全屏 · "+pending.DeviceName;
    toolbar=new CaptureToolbar();toolbar.PauseRequested+=TogglePause;toolbar.StopRequested+=StopCapture;
    toolbar.Place(pending.Window!=IntPtr.Zero?Screen.FromHandle(pending.Window):Screen.AllScreens[Math.Max(0,monitor.SelectedIndex)]);
    var overlayHandle=toolbar.Handle;
    if(!toolbar.CaptureExcluded)throw new InvalidOperationException("Windows 未能启用控制条的录制排除功能。请使用 Windows 10 2004 或更新版本，以确保控制条不会进入视频。");
    if(outline!=null)outline.Hide();
    SaveSettings();clock.Text="00:00:00";preferences.Enabled=false;start.Enabled=false;stop.Enabled=true;
    state=AppState.Countdown;deadline=DateTime.UtcNow.AddSeconds(settings.Countdown);
    toolbar.ShowCountdown(settings.Countdown,captureTarget);toolbar.Show();
    if(hide.Checked){WindowState=FormWindowState.Minimized;Hide();}
    stop.Text="取消";Tick();
   }catch(Exception e){if(toolbar!=null){toolbar.Dispose();toolbar=null;}MessageBox.Show(this,e.Message,"无法开始录制",MessageBoxButtons.OK,MessageBoxIcon.Information);}
  }
  void StartNow() {
   state=AppState.Starting;status.Text="正在准备录制…";stop.Enabled=false;stop.Text="结束";
   if(toolbar!=null)toolbar.ShowStatus(state,clock.Text,captureTarget);
   try {
    engine=new RecorderEngine();
    engine.StatusChanged+=s=>Ui(delegate{OnStatus(s);});
    engine.Completed+=path=>Ui(delegate{Finish(path,null);});
    engine.Failed+=error=>Ui(delegate{Finish(null,error);});
    if(hide.Checked){WindowState=FormWindowState.Minimized;Hide();}
    engine.Start(pending);
   }catch(Exception e){Finish(null,e.Message);}
  }
  void Ui(Action action) {if(!IsDisposed&&IsHandleCreated)try{BeginInvoke(action);}catch(InvalidOperationException){}}
  void OnStatus(RecorderStatus s) {
   if(state==AppState.Ready)return;
   if(s==RecorderStatus.Recording) {state=AppState.Recording;status.Text="● 正在录制";pause.Text="暂停";pause.Enabled=true;stop.Enabled=true;}
   if(s==RecorderStatus.Paused) {state=AppState.Paused;status.Text="● 已暂停";pause.Text="继续";pause.Enabled=true;stop.Enabled=true;}
   if(s==RecorderStatus.Finishing) {state=AppState.Saving;status.Text="正在保存视频…";pause.Enabled=false;stop.Enabled=false;}
   if(toolbar!=null)toolbar.ShowStatus(state,clock.Text,captureTarget);
  }
  void TogglePause() {
   if(engine==null)return;
   try {if(state==AppState.Recording){pause.Enabled=false;if(toolbar!=null)toolbar.SetBusy();engine.Pause();}else if(state==AppState.Paused){pause.Enabled=false;if(toolbar!=null)toolbar.SetBusy();engine.Resume();}}catch(Exception e){Finish(null,e.Message);}
  }
  void StopCapture() {
   if(state==AppState.Countdown){Reset();Restore();return;}
   if(engine==null||state==AppState.Saving||state==AppState.Starting)return;
   try {state=AppState.Saving;pause.Enabled=false;stop.Enabled=false;if(toolbar!=null)toolbar.SetBusy();status.Text="正在保存视频…";engine.Stop();}catch(Exception e){Finish(null,e.Message);}
  }
  void Finish(string path,string error) {
   if(engine!=null){engine.Dispose();engine=null;}
   Reset();Restore();
   if(error==null) {
    lastFile=path;status.Text="✓ 已保存视频";
    notice.Text="已保存："+Path.GetFileName(path)+"  ·  "+(new FileInfo(path).Length/1048576.0).ToString("0.0")+" MB";
    tray.ShowBalloonTip(2500,"录制已保存",path,ToolTipIcon.Info);
    if(exitAfterSave){Close();return;}
   }else {
    exitAfterSave=false;status.Text="录制失败";
    MessageBox.Show(this,"录制未完成："+error+"\n\n可尝试关闭硬件编码；若启用麦克风，请检查 Windows 麦克风权限与默认设备。\n已写入的视频片段保留在保存文件夹中。","轻录",MessageBoxButtons.OK,MessageBoxIcon.Warning);
   }
  }
  void Reset() {if(toolbar!=null){toolbar.Dispose();toolbar=null;}state=AppState.Ready;preferences.Enabled=true;start.Enabled=true;pause.Enabled=false;pause.Text="暂停";stop.Enabled=false;stop.Text="结束";status.Text="● 准备就绪";}
  void Tick() {
   if(state==AppState.Countdown) {
    int seconds=(int)Math.Ceiling((deadline-DateTime.UtcNow).TotalSeconds);
    status.Text="即将开始："+Math.Max(0,seconds)+" 秒";if(toolbar!=null)toolbar.ShowCountdown(seconds,captureTarget);if(seconds<=0)StartNow();
   }
   if(engine!=null)clock.Text=engine.Clock.Elapsed.ToString(@"hh\:mm\:ss");
   if(toolbar!=null&&state!=AppState.Countdown)toolbar.ShowStatus(state,clock.Text,captureTarget);
   if(state==AppState.Ready&&mode.SelectedIndex==1&&Visible) {
    var selected=windows.SelectedItem as WindowItem;
    if(selected!=null) {
     if(!Native.IsWindow(selected.Handle)||Native.IsIconic(selected.Handle)) {if(outline!=null)outline.Hide();target.Text="所选窗口已关闭或最小化，请重新选择。";}
     else {if(outline!=null)outline.Follow(selected.Handle);}
    }
   }else if(outline!=null)outline.Hide();
   if(state==AppState.Recording||state==AppState.Paused)tray.Text="轻录 · "+(state==AppState.Paused?"已暂停 ":"录制中 ")+clock.Text;
   else tray.Text="轻录 · "+(state==AppState.Saving?"正在保存":"准备就绪");
  }
  void Restore(){Show();WindowState=FormWindowState.Normal;Activate();}
  void OpenFolder(){try{Directory.CreateDirectory(folder.Text);Process.Start("explorer.exe",folder.Text);}catch(Exception e){MessageBox.Show(this,e.Message);}}
  void OnClosing(object sender,FormClosingEventArgs e) {
   if(state==AppState.Starting||state==AppState.Saving){e.Cancel=true;Restore();return;}
   if(state==AppState.Recording||state==AppState.Paused) {
    e.Cancel=true;
    if(MessageBox.Show(this,"结束当前录制、保存视频后退出？","轻录",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes){exitAfterSave=true;StopCapture();}
    return;
   }
   SaveSettings();timer.Stop();timer.Dispose();tray.Dispose();targetTip.Dispose();if(toolbar!=null)toolbar.Dispose();if(outline!=null)outline.Dispose();if(engine!=null)engine.Dispose();
  }
  protected override void OnHandleCreated(EventArgs e) {
   base.OnHandleCreated(e);
   bool a=Native.RegisterHotKey(Handle,1,0x4003,(uint)Keys.F9),b=Native.RegisterHotKey(Handle,2,0x4003,(uint)Keys.F10);hotkeysReady=a&&b;
  }
  protected override void OnHandleDestroyed(EventArgs e) {Native.UnregisterHotKey(Handle,1);Native.UnregisterHotKey(Handle,2);base.OnHandleDestroyed(e);}
  protected override void WndProc(ref Message m) {
   if(m.Msg==0x312) {if(m.WParam.ToInt32()==1){if(state==AppState.Ready)BeginCapture();else TogglePause();}else if(m.WParam.ToInt32()==2)StopCapture();}
   base.WndProc(ref m);
  }
 }
 internal static class Program {
  [STAThread] static void Main(string[] args) {
   Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   if(args.Length>0&&args[0]=="--health-check") {
    try {string report=HealthCheck.Check();if(args.Length>1)File.WriteAllText(args[1],report);Environment.ExitCode=0;}
    catch(Exception e){Environment.ExitCode=1;if(args.Length>1)File.WriteAllText(args[1],"FAIL: "+e.ToString());}
    return;
   }
   if(args.Length>0&&args[0]=="--self-test"){SelfTest.Run();return;}
   if(args.Length>0&&args[0]=="--overlay-test"){OverlayTest.Run();return;}
   bool first;using(var mutex=new System.Threading.Mutex(true,"LightRecorder_482d64a1",out first)) {
    if(!first){MessageBox.Show("轻录已在运行，请在任务栏托盘中打开。","轻录");return;}
    try {Application.Run(new MainForm());}catch(Exception e){MessageBox.Show("启动失败："+e.Message+"\n请确认已安装 x64 Visual C++ 运行库及 Windows 媒体组件。","轻录");}
   }
  }
 }
}
