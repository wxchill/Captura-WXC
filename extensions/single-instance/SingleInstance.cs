using System;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Reflection;

[assembly: AssemblyVersion("1.0.0.0")]
namespace CapturaInstance {
    // A named mutex is acquired before the splash screen or application is
    // constructed. A tray-hidden window still owns the same process lease.
    public sealed class InstanceLease : IDisposable {
        readonly Mutex mutex;
        readonly EventWaitHandle signal;
        readonly Action activated;
        Thread listener;
        volatile bool stopped;
        bool owns;
        InstanceLease(string id,Action callback) {
            string sid=WindowsIdentity.GetCurrent().User.Value;
            string name="Local\\Captura.Desktop."+sid+"."+id;
            signal=new EventWaitHandle(false,EventResetMode.AutoReset,name+".Activate");
            mutex=new Mutex(false,name+".Owner");
            activated=callback;
        }
        public static bool TryAcquire(string id,Action callback,out InstanceLease lease) {
            var candidate=new InstanceLease(id,callback);
            try {candidate.owns=candidate.mutex.WaitOne(0,false);}
            catch(AbandonedMutexException) {candidate.owns=true;}
            if(!candidate.owns) {
                candidate.signal.Set();candidate.Dispose();lease=null;return false;
            }
            candidate.listener=new Thread(candidate.Listen){IsBackground=true,Name="Captura instance activation"};
            candidate.listener.Start();lease=candidate;return true;
        }
        void Listen() {
            try {
                while(!stopped) {
                    signal.WaitOne();
                    if(!stopped && activated!=null) {try{activated();}catch{}}
                }
            }catch(ObjectDisposedException) {}
        }
        public void Dispose() {
            if(stopped)return;
            stopped=true;signal.Set();
            if(listener!=null && listener!=Thread.CurrentThread)listener.Join(500);
            if(owns){mutex.ReleaseMutex();owns=false;}
            mutex.Dispose();signal.Dispose();
        }
    }
    public static class SingleInstance {
        static InstanceLease lease;
        static int activationPending;
        public static bool TryEnter() {
            return InstanceLease.TryAcquire("Main.v1",RequestActivation,out lease);
        }
        static void RequestActivation() {
            if(Interlocked.Exchange(ref activationPending,1)!=0)return;
            Application app=null;
            // A second click can arrive during the primary instance startup.
            for(int i=0;i<200;i++) {
                app=Application.Current;
                if(app!=null)break;
                Thread.Sleep(50);
            }
            if(app==null || app.Dispatcher.HasShutdownStarted){Interlocked.Exchange(ref activationPending,0);return;}
            app.Dispatcher.BeginInvoke(new Action(delegate {
                int ticks=0;
                var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
                timer.Tick+=delegate {
                    Window window=app.MainWindow;
                    if(window!=null) {
                        timer.Stop();
                        if(window.WindowState==WindowState.Minimized)window.WindowState=WindowState.Normal;
                        window.Show();window.Activate();
                        Interlocked.Exchange(ref activationPending,0);
                    } else if(++ticks>100 || app.Dispatcher.HasShutdownStarted) {
                        timer.Stop();Interlocked.Exchange(ref activationPending,0);
                    }
                };
                timer.Start();
            }));
        }
        public static void Release() {
            if(lease!=null){lease.Dispose();lease=null;}
        }
    }
}
