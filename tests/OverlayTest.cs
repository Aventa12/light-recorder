using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace LightRecorder {
 // An in-process interaction check: uses the real buttons, state machine and capture output.
 internal static class OverlayTest {
  static T Field<T>(object obj,string name) {return (T)obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);}
  static void Set(object obj,string name,object value) {obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);}
  static Button ButtonWith(CaptureToolbar bar,string text) {foreach(Control c in bar.Controls)if(c is Button&&c.Text==text)return (Button)c;throw new Exception("Missing button: "+text);}
  public static void Run() {
   string root=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","verification","overlay-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")));
   Directory.CreateDirectory(root);var results=new List<string>();
   var screen=Screen.PrimaryScreen;var area=screen.WorkingArea;
   var background=new Form {Text="轻录窗口选择验证 · 紫色测试窗口",FormBorderStyle=FormBorderStyle.None,StartPosition=FormStartPosition.Manual,Bounds=area,BackColor=Color.FromArgb(70,32,86)};
   background.Controls.Add(new Label {Text="轻录 · 悬浮控制条与窗口选择验证",ForeColor=Color.White,Font=new Font("Microsoft YaHei UI",24),Location=new Point(30,30),Size=new Size(1000,60)});
   var crop=new Rectangle(area.Right-640,area.Bottom-360,640,360);
   // This ordinary, nonexcluded marker must remain visible in the recording.
   var marker=new Form {Text="测试画面标记",FormBorderStyle=FormBorderStyle.None,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(crop.Left+20,crop.Top+20,56,56),BackColor=Color.FromArgb(239,67,77),TopMost=true};
   MainForm main=null;var timer=new System.Windows.Forms.Timer {Interval=100};
   DateTime startTime=DateTime.UtcNow,recordingTime=DateTime.UtcNow,pausedTime=DateTime.UtcNow,resumedTime=DateTime.UtcNow;
   int stage=0;CaptureToolbar bar=null;
   Action finish=delegate {
    timer.Stop();File.WriteAllLines(Path.Combine(root,"interaction-results.txt"),results);
    if(main!=null)main.Close();marker.Close();background.Close();
   };
   background.Shown+=delegate {
    try {
     marker.Show();main=new MainForm();main.Show();Application.DoEvents();
     var mode=Field<ComboBox>(main,"mode");var windows=Field<ComboBox>(main,"windows");mode.SelectedIndex=1;
     foreach(WindowItem item in windows.Items)if(item.Handle==background.Handle)windows.SelectedItem=item;
     var selected=windows.SelectedItem as WindowItem;var outline=Field<WindowOutline>(main,"outline");
     if(selected==null||selected.Handle!=background.Handle||!Field<Label>(main,"target").Text.Contains(background.Text)||outline==null||!outline.Visible||!outline.CaptureExcluded)throw new Exception("Selected window title/border/exclusion missing");
     results.Add("PASS: selected window title and excluded green outline identify the target");
     using(var image=new Bitmap(main.Width,main.Height)){main.DrawToBitmap(image,new Rectangle(0,0,main.Width,main.Height));image.Save(Path.Combine(root,"selection-ui.png"));}
     mode.SelectedIndex=2;Set(main,"crop",crop);
     Field<CheckBox>(main,"system").Checked=false;Field<CheckBox>(main,"mic").Checked=false;
     Field<CheckBox>(main,"hide").Checked=true;Field<ComboBox>(main,"countdown").SelectedIndex=1;
     Field<TextBox>(main,"folder").Text=root;
     Field<Button>(main,"start").PerformClick();bar=Field<CaptureToolbar>(main,"toolbar");
     if(bar==null||!bar.Visible||!bar.CaptureExcluded||bar.Right!=area.Right-20||bar.Bottom!=area.Bottom-20)throw new Exception("Countdown placement/exclusion failed");
     results.Add("PASS: countdown visible at desktop bottom right with WDA_EXCLUDEFROMCAPTURE");
     File.WriteAllText(Path.Combine(root,"expected-frame.txt"),"Frame: 640x360\nBackground RGB: 70,32,86\nOrdinary red marker: x=20..76,y=20..76\nExcluded toolbar: x="+(bar.Left-crop.Left)+",y="+(bar.Top-crop.Top)+",w="+bar.Width+",h="+bar.Height+"\n");
     using(var image=new Bitmap(bar.Width,bar.Height)){bar.DrawToBitmap(image,new Rectangle(0,0,bar.Width,bar.Height));image.Save(Path.Combine(root,"countdown-ui.png"));}
     startTime=DateTime.UtcNow;timer.Start();
    }catch(Exception e){results.Add("FAIL: "+e.Message);finish();}
   };
   timer.Tick+=delegate {
    try {
     var state=Field<AppState>(main,"state");
     if((DateTime.UtcNow-startTime).TotalSeconds>30)throw new Exception("Interaction timed out: "+state);
     if(stage==0&&state==AppState.Recording) {
      if((DateTime.UtcNow-startTime).TotalSeconds<2.5)throw new Exception("Countdown ended too soon");
      results.Add("PASS: 3-second countdown transitions to recording controls");
      recordingTime=DateTime.UtcNow;stage=1;
      using(var image=new Bitmap(bar.Width,bar.Height)){bar.DrawToBitmap(image,new Rectangle(0,0,bar.Width,bar.Height));image.Save(Path.Combine(root,"toolbar-ui.png"));}
     }
     if(stage==1&&(DateTime.UtcNow-recordingTime).TotalSeconds>1.3){ButtonWith(bar,"暂停").PerformClick();stage=2;}
     if(stage==2&&state==AppState.Paused){results.Add("PASS: toolbar Pause button pauses recorder");pausedTime=DateTime.UtcNow;stage=3;}
     if(stage==3&&(DateTime.UtcNow-pausedTime).TotalSeconds>1){ButtonWith(bar,"继续").PerformClick();stage=4;}
     if(stage==4&&state==AppState.Recording){results.Add("PASS: toolbar Resume button resumes recorder");resumedTime=DateTime.UtcNow;stage=5;}
     if(stage==5&&(DateTime.UtcNow-resumedTime).TotalSeconds>1.5){ButtonWith(bar,"停止").PerformClick();stage=6;}
     if(stage==6&&state==AppState.Ready){
      if(Field<CaptureToolbar>(main,"toolbar")!=null||Directory.GetFiles(root,"*.mp4").Length!=1)throw new Exception("Stop did not save/close toolbar");
      results.Add("PASS: toolbar Stop saves a video and disposes floating controls");
      // A cancelled countdown must also restore the main window without creating a video.
      Field<Button>(main,"start").PerformClick();var cancelBar=Field<CaptureToolbar>(main,"toolbar");ButtonWith(cancelBar,"取消").PerformClick();
      if(Field<AppState>(main,"state")!=AppState.Ready||!main.Visible||Field<CaptureToolbar>(main,"toolbar")!=null)throw new Exception("Countdown cancellation failed");
      results.Add("PASS: countdown Cancel restores main window without starting capture");finish();
     }
    }catch(Exception e){results.Add("FAIL: "+e.Message);finish();}
   };
   Application.Run(background);timer.Dispose();if(main!=null)main.Dispose();marker.Dispose();
  }
 }
}
