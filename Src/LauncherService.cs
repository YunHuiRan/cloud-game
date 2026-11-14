using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace Ludens
{
    public class GameStartedEventArgs : EventArgs
    {
        public GameEntry Game { get; private set; }
        public GameStartedEventArgs(GameEntry game) { Game = game; }
    }

    public class GameEndedEventArgs : EventArgs
    {
        public GameEntry Game { get; private set; }
        public TimeSpan Duration { get; private set; }
        public GameEndedEventArgs(GameEntry game, TimeSpan duration)
        {
            Game = game;
            Duration = duration;
        }
    }

    public sealed class LauncherService
    {
        private sealed class RunningGame
        {
            public GameEntry Game;
            public Process Process;
            public DateTime StartUtc;
            public DateTime LastSeenUtc;
            public DateTime LastHeartbeatUtc;
            public IntPtr Job;
            public Timer Timer;
            public bool Ended;
            public bool Elevated;
            public int ProcessId;
        }

        private static readonly object SyncRoot = new object();
        private readonly Dictionary<string, RunningGame> _running =
            new Dictionary<string, RunningGame>(StringComparer.OrdinalIgnoreCase);

        public event EventHandler<GameStartedEventArgs> GameStarted;
        public event EventHandler<GameEndedEventArgs> GameEnded;

        public int RunningCount
        {
            get { lock (SyncRoot) { return _running.Count; } }
        }

        public bool IsRunning(string gameId)
        {
            if (string.IsNullOrEmpty(gameId)) return false;
            lock (SyncRoot) { return _running.ContainsKey(gameId); }
        }

        public GameEntry GetRunningGame(string gameId)
        {
            lock (SyncRoot)
            {
                RunningGame rg;
                return _running.TryGetValue(gameId, out rg) ? rg.Game : null;
            }
        }

        public List<GameEntry> GetRunningGames()
        {
            lock (SyncRoot)
            {
                List<GameEntry> list = new List<GameEntry>();
                foreach (RunningGame rg in _running.Values) list.Add(rg.Game);
                return list;
            }
        }

        public TimeSpan GetRunningDuration(string gameId)
        {
            lock (SyncRoot)
            {
                RunningGame rg;
                if (!_running.TryGetValue(gameId, out rg)) return TimeSpan.Zero;
                DateTime end = DateTime.UtcNow;
                if (rg.Process != null)
                {
                    try { if (rg.Process.HasExited) end = rg.LastSeenUtc; }
                    catch { }
                }
                return end - rg.StartUtc;
            }
        }

        public bool Launch(GameEntry game)
        {
            if (game == null) return false;
            if (string.IsNullOrEmpty(game.ExePath) || !File.Exists(game.ExePath))
                throw new FileNotFoundException("游戏可执行文件不存在，请编辑游戏路径。", game.ExePath);

            lock (SyncRoot)
            {
                if (_running.ContainsKey(game.Id)) return false;
            }

            string workingDirectory = game.WorkingDirectory;
            if (string.IsNullOrEmpty(workingDirectory) || !Directory.Exists(workingDirectory))
                workingDirectory = Path.GetDirectoryName(game.ExePath);

            ProcessStartInfo info = new ProcessStartInfo();
            string extension = Path.GetExtension(game.ExePath).ToLowerInvariant();
            if (extension == ".bat" || extension == ".cmd")
            {
                info.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
                info.Arguments = "/c \"" + game.ExePath + "\" " + (game.Arguments ?? "");
            }
            else
            {
                info.FileName = game.ExePath;
                info.Arguments = game.Arguments ?? "";
            }
            info.WorkingDirectory = workingDirectory;
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.ErrorDialog = false;

            DateTime start = DateTime.UtcNow;
            Process process;
            bool elevated = false;
            try
            {
                process = StartProcess(info, out elevated);
            }
            catch (Exception ex)
            {
                AppStore.Log(ex);
                throw new InvalidOperationException("无法启动游戏：" + ex.Message, ex);
            }
            if (process == null) throw new InvalidOperationException("游戏进程没有成功创建。");

            RunningGame rg = new RunningGame();
            rg.Game = game;
            rg.Process = process;
            rg.StartUtc = start;
            rg.LastSeenUtc = start;
            rg.LastHeartbeatUtc = start;
            rg.Elevated = elevated;
            rg.ProcessId = process.Id;
            // A process started through the UAC shell cannot be assigned to this
            // process's job object; watch it by process id instead.
            rg.Job = elevated ? IntPtr.Zero : CreateJob(process);

            game.IsRunning = true;
            game.ActiveSessionStartUtc = start;
            game.ActiveSessionLastSeenUtc = start;
            game.LaunchCount++;
            game.LastPlayedUtc = start;
            AppStore.Instance.Save(false);

            lock (SyncRoot) { _running[game.Id] = rg; }

            rg.Timer = new Timer(OnTick, rg, 2500, 2500);
            try
            {
                process.Exited += delegate { OnProcessExited(rg); };
                process.EnableRaisingEvents = true;
            }
            catch (Exception ex) { AppStore.Log(ex); }

            if (GameStarted != null)
            {
                Application app = Application.Current;
                if (app != null && app.Dispatcher != null)
                    app.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (GameStarted != null) GameStarted(this, new GameStartedEventArgs(game));
                    }));
                else if (GameStarted != null)
                    GameStarted(this, new GameStartedEventArgs(game));
            }
            return true;
        }

        public void Stop(GameEntry game)
        {
            if (game == null) return;
            RunningGame rg;
            lock (SyncRoot)
            {
                if (!_running.TryGetValue(game.Id, out rg)) return;
            }
            try
            {
                if (rg.Job != IntPtr.Zero) TerminateJobObject(rg.Job, 1);
                else if (rg.Process != null && !rg.Process.HasExited) rg.Process.Kill();
            }
            catch (Exception ex) { AppStore.Log(ex); }
            EndSession(rg, true);
        }

        public void Shutdown(bool killGames)
        {
            List<RunningGame> copy;
            lock (SyncRoot) { copy = new List<RunningGame>(_running.Values); }
            for (int i = 0; i < copy.Count; i++)
            {
                if (killGames)
                {
                    try
                    {
                        if (copy[i].Job != IntPtr.Zero) TerminateJobObject(copy[i].Job, 1);
                        else if (copy[i].Process != null && !copy[i].Process.HasExited) copy[i].Process.Kill();
                    }
                    catch { }
                }
                EndSession(copy[i], false);
            }
        }

        private static Process StartProcess(ProcessStartInfo info, out bool elevated)
        {
            elevated = false;
            try
            {
                return Process.Start(info);
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode != 740)
                    throw new InvalidOperationException("无法启动游戏：" + ex.Message, ex);

                // ERROR_ELEVATION_REQUIRED: the game asks for administrator rights.
                elevated = true;
                ProcessStartInfo runas = new ProcessStartInfo();
                runas.FileName = info.FileName;
                runas.Arguments = info.Arguments;
                runas.WorkingDirectory = info.WorkingDirectory;
                runas.UseShellExecute = true;
                runas.Verb = "runas";
                runas.ErrorDialog = false;
                try
                {
                    return Process.Start(runas);
                }
                catch (Exception inner)
                {
                    throw new InvalidOperationException("这款游戏需要管理员权限，但提权启动被取消或失败：" + inner.Message, inner);
                }
            }
        }

        private void OnProcessExited(RunningGame rg)
        {
            if (rg.Elevated) return;
            // Allow a game's child process (launcher → game) to keep the session alive.
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    for (int i = 0; i < 4; i++)
                    {
                        Thread.Sleep(700);
                        if (rg.Ended) return;
                        if (rg.Job != IntPtr.Zero && ActiveJobProcesses(rg.Job) > 0)
                        {
                            rg.LastSeenUtc = DateTime.UtcNow;
                            continue;
                        }
                        if (rg.Job == IntPtr.Zero) break;
                    }
                    EndSession(rg, false);
                }
                catch (Exception ex)
                {
                    AppStore.Log(ex);
                    EndSession(rg, false);
                }
            });
        }

        private void OnTick(object state)
        {
            RunningGame rg = state as RunningGame;
            if (rg == null) return;
            try
            {
                if (rg.Ended) return;
                bool exited = false;
                if (rg.Elevated)
                {
                    exited = !IsProcessAlive(rg.ProcessId);
                }
                else
                {
                    try { exited = rg.Process == null || rg.Process.HasExited; }
                    catch { exited = true; }
                }

                if (!exited) rg.LastSeenUtc = DateTime.UtcNow;

                if (exited)
                {
                    int active = rg.Job != IntPtr.Zero ? ActiveJobProcesses(rg.Job) : 0;
                    if (active <= 0)
                    {
                        EndSession(rg, false);
                        return;
                    }
                }

                DateTime now = DateTime.UtcNow;
                if ((now - rg.LastHeartbeatUtc).TotalSeconds >= 30)
                {
                    rg.LastHeartbeatUtc = now;
                    AppStore.Instance.Heartbeat(rg.Game, now);
                }
            }
            catch (Exception ex)
            {
                AppStore.Log(ex);
                EndSession(rg, false);
            }
        }

        private void EndSession(RunningGame rg, bool forced)
        {
            if (rg == null) return;
            lock (SyncRoot)
            {
                if (rg.Ended) return;
                rg.Ended = true;
                _running.Remove(rg.Game.Id);
            }

            try { if (rg.Timer != null) rg.Timer.Dispose(); } catch { }
            DateTime end = DateTime.UtcNow;
            if (rg.Process != null)
            {
                try { if (rg.Process.HasExited) rg.LastSeenUtc = end; } catch { }
            }
            if (end < rg.StartUtc) end = rg.StartUtc;
            long seconds = (long)Math.Round((end - rg.StartUtc).TotalSeconds);
            if (seconds < 1) seconds = 1;

            rg.Game.IsRunning = false;
            AppStore.Instance.AddSession(rg.Game, rg.StartUtc, end, seconds);
            AppStore.Instance.Save(false);
            AppStore.Instance.NotifyGame(rg.Game);

            try
            {
                if (rg.Job != IntPtr.Zero) CloseHandle(rg.Job);
            }
            catch { }
            rg.Job = IntPtr.Zero;

            TimeSpan duration = TimeSpan.FromSeconds(seconds);
            if (GameEnded != null)
            {
                Application app = Application.Current;
                if (app != null && app.Dispatcher != null)
                    app.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (GameEnded != null) GameEnded(this, new GameEndedEventArgs(rg.Game, duration));
                    }));
            }
        }

        private static IntPtr CreateJob(Process process)
        {
            IntPtr job = IntPtr.Zero;
            try
            {
                job = CreateJobObject(IntPtr.Zero, null);
                if (job == IntPtr.Zero) return IntPtr.Zero;
                if (!AssignProcessToJobObject(job, process.Handle))
                {
                    CloseHandle(job);
                    return IntPtr.Zero;
                }
                return job;
            }
            catch
            {
                if (job != IntPtr.Zero) CloseHandle(job);
                return IntPtr.Zero;
            }
        }

        private static int ActiveJobProcesses(IntPtr job)
        {
            try
            {
                JobAccounting info = new JobAccounting();
                int size = Marshal.SizeOf(typeof(JobAccounting));
                if (!QueryInformationJobObject(job, 1, ref info, size, IntPtr.Zero)) return 0;
                return (int)info.ActiveProcesses;
            }
            catch { return 0; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobAccounting
        {
            public long TotalUserTime;
            public long TotalKernelTime;
            public long ThisPeriodTotalUserTime;
            public long ThisPeriodTotalKernelTime;
            public uint TotalPageFaultCount;
            public uint TotalProcesses;
            public uint ActiveProcesses;
            public uint TotalTerminatedProcesses;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr security, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll")]
        private static extern bool QueryInformationJobObject(IntPtr job, int infoClass, ref JobAccounting info, int length, IntPtr returnLength);

        [DllImport("kernel32.dll")]
        private static extern bool TerminateJobObject(IntPtr job, uint exitCode);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int processId);

        [DllImport("kernel32.dll")]
        private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        private const uint Synchronize = 0x00100000;
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const uint WaitTimeout = 0x102;

        private static bool IsProcessAlive(int processId)
        {
            if (processId <= 0) return false;
            IntPtr handle = OpenProcess(Synchronize | ProcessQueryLimitedInformation, false, processId);
            if (handle == IntPtr.Zero)
            {
                try
                {
                    Process p = Process.GetProcessById(processId);
                    try { return !p.HasExited; }
                    catch { return true; }
                }
                catch { return false; }
            }
            try { return WaitForSingleObject(handle, 0) == WaitTimeout; }
            finally { CloseHandle(handle); }
        }
    }
}
