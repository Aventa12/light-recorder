using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

// Development-only check: open each output with Windows' actual media decoder.
class VerifyPlayback {
 [STAThread] static void Main(string[] args) {
  var files=Directory.GetFiles(args[0],"*.mp4");int index=0;MediaPlayer player=null;
  var timer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(12)};
  var frameTimer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(1)};
  int failures=0;Action next=null;
  next=delegate {
   timer.Stop();frameTimer.Stop();if(player!=null)player.Close();
   if(index>=files.Length){Environment.ExitCode=failures;Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);return;}
   string path=files[index++];player=new MediaPlayer {Volume=0};
   player.MediaFailed+=delegate(object s,ExceptionEventArgs e){Console.WriteLine(Path.GetFileName(path)+": FAIL "+e.ErrorException.Message);failures++;next();};
   player.MediaOpened+=delegate {
    if(player.NaturalVideoWidth==0||!player.NaturalDuration.HasTimeSpan){Console.WriteLine(Path.GetFileName(path)+": FAIL no video/duration");failures++;next();return;}
    Console.WriteLine(Path.GetFileName(path)+": decoded "+player.NaturalVideoWidth+"x"+player.NaturalVideoHeight+", "+player.NaturalDuration.TimeSpan.TotalSeconds.ToString("0.00")+"s");
    player.Play();frameTimer.Start();
   };
   player.Open(new Uri(path));timer.Start();
  };
  frameTimer.Tick+=delegate {
   frameTimer.Stop();var visual=new DrawingVisual();
   using(var context=visual.RenderOpen())context.DrawVideo(player,new Rect(0,0,640,360));
   var bitmap=new RenderTargetBitmap(640,360,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
   var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
   using(var file=File.Create(Path.ChangeExtension(files[index-1],".decoded.png")))encoder.Save(file);
   next();
  };
  timer.Tick+=delegate {Console.WriteLine(Path.GetFileName(files[index-1])+": FAIL decoder timeout");failures++;next();};
  next();Dispatcher.Run();
 }
}
