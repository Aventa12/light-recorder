using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ScreenRecorderLib;

namespace LightRecorder {
 internal static class SelfTest {
  public static void Run() {
   string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","verification");Directory.CreateDirectory(root);
   string runRoot=Path.Combine(root,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(runRoot);
   var results=new List<string>();
   using(var preview=new MainForm()) {
    preview.Show();Application.DoEvents();
    using(var image=new Bitmap(preview.Width,preview.Height)){preview.DrawToBitmap(image,new Rectangle(0,0,preview.Width,preview.Height));image.Save(Path.Combine(root,"interface.png"));}
    preview.Close();
   }
   var form=new Form {Text="轻录 · 录制验证画面",Size=new Size(720,480),StartPosition=FormStartPosition.CenterScreen,BackColor=Color.FromArgb(21,42,58)};
   var label=new Label {ForeColor=Color.White,Font=new Font("Microsoft YaHei UI",24),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter};form.Controls.Add(label);
   var timer=new System.Windows.Forms.Timer {Interval=100};RecorderEngine engine=null;
   int index=-1,stage=0,frame=0;DateTime began=DateTime.UtcNow,timeout=DateTime.UtcNow;
   string[] cases={"display-silent","window","region-pause","system-audio","microphone","both-audio","hardware-1080p60"};
   Action next=null;
   next=delegate {
    if(engine!=null){engine.Dispose();engine=null;}
    index++;stage=0;
    if(index>=cases.Length){File.WriteAllLines(Path.Combine(root,"results.txt"),results);form.Close();return;}
    var screen=Screen.PrimaryScreen;
    var req=new CaptureRequest {DeviceName=screen.DeviceName,Bounds=screen.Bounds,FilePath=Path.GetFullPath(Path.Combine(runRoot,cases[index]+".mp4")),DiagnosticLog=Path.GetFullPath(Path.Combine(runRoot,cases[index]+".log")),MaxHeight=720,Hardware=false,Highlight=true};
    if(index==1) {req.Window=form.Handle;req.Bounds=form.Bounds;}
    if(index==2)req.Crop=new Rectangle(screen.Bounds.X+80,screen.Bounds.Y+80,640,360);
    req.SystemAudio=index==3||index==5||index==6;req.Microphone=index==4||index==5;
    if(index==6){req.Hardware=true;req.Fps=60;req.MaxHeight=1080;}
    engine=new RecorderEngine();timeout=DateTime.UtcNow.AddSeconds(25);
    engine.StatusChanged+=s=>form.BeginInvoke((Action)delegate {
     if(s==RecorderStatus.Recording&&stage==0){began=DateTime.UtcNow;stage=1;}
    });
    engine.Completed+=p=>form.BeginInvoke((Action)delegate {results.Add(cases[index]+": PASS "+new FileInfo(p).Length+" bytes, active="+engine.Clock.Elapsed.TotalSeconds.ToString("0.00"));next();});
    engine.Failed+=err=>form.BeginInvoke((Action)delegate {results.Add(cases[index]+": FAIL "+err);next();});
    try{engine.Start(req);}catch(Exception e){results.Add(cases[index]+": FAIL "+e.Message);form.BeginInvoke(next);}
   };
   timer.Tick+=delegate {
    frame++;label.Text="轻录  ·  实际录制验证\n"+(index>=0&&index<cases.Length?cases[index]:"")+"\n"+frame.ToString("0000");
    label.BackColor=frame%20<10?Color.FromArgb(21,42,58):Color.FromArgb(24,85,76);
    if(engine==null)return;
    if(DateTime.UtcNow>timeout){results.Add(cases[index]+": FAIL timeout");timer.Stop();File.WriteAllLines(Path.Combine(root,"results.txt"),results);form.Close();return;}
    double elapsed=(DateTime.UtcNow-began).TotalSeconds;
    if(index==2&&stage==1&&elapsed>=1){engine.Pause();stage=2;}
    if(index==2&&stage==2&&elapsed>=2){engine.Resume();stage=3;}
    if((stage==1||stage==3)&&elapsed>=4){stage=4;engine.Stop();}
   };
   form.Shown+=delegate{timer.Start();next();};Application.Run(form);timer.Dispose();if(engine!=null)engine.Dispose();
  }
 }
}
