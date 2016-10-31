﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Git;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Git.Lfx;
using Util;
using System.IO;
using System.Text;
using System.Collections.Concurrent;

namespace Lfx {

    public enum LfxCmdSwitches {
        Quite, Q,
        Exe,
        Zip,
        Clean,
        Clear,
        Force, F,
    }

    public sealed class LfxProgressTracker {
        private readonly ConcurrentDictionary<LfxProgressType, long> m_totalBytes;
        private readonly ConcurrentDictionary<LfxProgressType, long> m_progressBytes;

        public LfxProgressTracker() {
            m_totalBytes = new ConcurrentDictionary<LfxProgressType, long>();
            m_progressBytes = new ConcurrentDictionary<LfxProgressType, long>();
        }

        private void Log() {
            lock (this) {
                Console.Write(ToString().PadRight(Console.WindowWidth - 1));
                Console.CursorLeft = 0;
            }
        }

        public void UpdateProgress(LfxProgress progress) {
            var type = progress.Type;
            var bytes = progress.Bytes;

            if (!m_progressBytes.ContainsKey(type))
                m_progressBytes[type] = 0;

            m_progressBytes[type] += bytes;
            Log();
        }
        public void SetTotal(LfxProgressType type, long value) {
            m_progressBytes.GetOrAdd(type, 0);
            m_totalBytes[type] = value;
        }
        public void Finished() {
            foreach (var pair in m_totalBytes)
                m_progressBytes[pair.Key] = pair.Value;
            Log();
        }

        public override string ToString() {

            var sb = new StringBuilder();

            foreach (var o in
                from progress in m_progressBytes
                orderby progress.Key
                join total in m_totalBytes on progress.Key equals total.Key into totalForProgress
                from total in totalForProgress.DefaultIfEmpty()
                select new {
                    Type = progress.Key,
                    Total = total.Value,
                    Progress = progress.Value
                }
            ) {
                if (sb.Length != 0)
                    sb.Append(", ");

                sb.Append($"{o.Type}: {o.Progress.ToFileSize()}");
                if (o.Total != 0)
                    sb.Append($" ({o.Progress / (double)o.Total:P})");
            }

            return sb.ToString();
        }
    }

    public sealed class LfxCmd {
        public const string Exe = "git-lfx.exe";

        private readonly object Lock = new object();
        private readonly string m_commandLine;
        private readonly LfxEnv m_env;

        public static void Execute(string commandLine) => new LfxCmd(commandLine).Execute();

        private LfxCmd(string commandLine) {
            m_commandLine = commandLine;
            m_env = new LfxEnv();
        }

        private void Execute() {
            var bf = 
                BindingFlags.Instance| 
                BindingFlags.Public |
                BindingFlags.IgnoreCase;

            try {

                var args = GitCmdArgs.Parse(m_commandLine);
                var name = args.Name;

                // default
                if (args.Name == null) {
                    Help();
                    return;
                }

                // check name
                var method = typeof(LfxCmd).GetMethod(name, bf);
                if (method == null)
                    throw new GitCmdException($"Command '{name}' unrecognized.");

                // dispach
                var result = method.Invoke(this, null) as Task;
                if (result != null)
                    result.Wait();

            } catch (Exception e) {
                Log(e);
            }
        }
        private GitCmdArgs Parse(
            int minArgs = 0,
            int maxArgs = int.MaxValue,
            IEnumerable<GitCmdSwitchInfo> switchInfo = null) {

            if (switchInfo == null)
                switchInfo = GitCmdSwitchInfo.Create();

            return GitCmdArgs.Parse(m_commandLine, minArgs, maxArgs, switchInfo);
        }
        private void Log(Exception e) {
            while (e is TargetInvocationException)
                e = e.InnerException;

            var ae = e as AggregateException;
            if (ae != null) {
                foreach (var aei in ae.InnerExceptions)
                    Log(aei);
                return;
            }

            Log($"{e.GetType()}: {e.Message}");
        }
        private void Log(object obj) => Log(obj?.ToString());
        private void Log(string message = null) {
            Console.WriteLine(message);
        }
        private LfxProgressTracker LogProgress() {
            var progress = new LfxProgressTracker();
            m_env.OnProgress += progress.UpdateProgress;
            return progress;
        }

        public void Help() {
            Log("git-lfx/0.2.0 (GitHub; corclr)");
            Log("git lfx <command> [<args>]");
            Log();
            Log("Env                                Dump environment.");
            Log();
            Log("Checkout                           Sync content in lfx directory using pointers in .lfx directory.");
            Log("    -q, --quite                        Suppress progress reporting.");
            Log();
            Log("Pull <path> <url> [<exeCmd>]       Pull content to path in 'lfx' and add corrisponding pointer to '.lfx'.");
            Log("    -q, --quite                        Suppress progress reporting.");
            Log("    --zip                              Url points to zip archive.");
            Log("    --exe                              Url points to self expanding archive. Use '{0}' in <exeCmd> for target directory.");
            Log();
            Log("Fetch <url> [<exeCmd>]             Fetch content and echo poitner.");
            Log("    -q, --quite                        Suppress progress reporting.");
            Log("    --zip                              Url points to zip archive.");
            Log("    --exe                              Url points to self expanding archive. Use '{0}' in <cmd> for target directory.");
            Log();
            Log("Cache                              Dump cache stats.");
            Log("    --clean                            Delete orphaned temp directories.");
            Log("    --clear                            Delete all caches on this machine.");
            Log("    -f, --force                        Must be specified with clear.");
        }
        public void Env() {
            Log($"Environment Variables:");
            Log($"  {LfxEnv.EnvironmentVariable.DiskCacheName}={LfxEnv.EnvironmentVariable.DiskCache}");
            Log($"  {LfxEnv.EnvironmentVariable.BusCacheName}={LfxEnv.EnvironmentVariable.BusCache}");
            Log($"  {LfxEnv.EnvironmentVariable.LanCacheName}={LfxEnv.EnvironmentVariable.LanCache}");
            Log();

            Log($"Directories:");
            Log($"  {nameof(m_env.WorkingDir)}: {m_env.WorkingDir}");
            Log($"  {nameof(m_env.EnlistmentDir)}: {m_env.EnlistmentDir}");
            Log($"  {nameof(m_env.ContentDir)}: {m_env.ContentDir}");
            Log($"  {nameof(m_env.InfoDir)}: {m_env.InfoDir}");
            Log($"  {nameof(m_env.DiskCacheDir)}: {m_env.DiskCacheDir}");
            Log($"  {nameof(m_env.BusCacheDir)}: {m_env.BusCacheDir}");
            Log($"  {nameof(m_env.LanCacheDir)}: {m_env.LanCacheDir}");
            Log();
        }
        public void Cache() {
            var args = Parse(
                minArgs: 0,
                maxArgs: 0,
                switchInfo: GitCmdSwitchInfo.Create(
                    LfxCmdSwitches.Clean,
                    LfxCmdSwitches.Clear,
                    LfxCmdSwitches.F, LfxCmdSwitches.Force
               )
            );

            var clean = args.IsSet(LfxCmdSwitches.Clean);
            var clear = args.IsSet(LfxCmdSwitches.Clear);
            var force = args.IsSet(LfxCmdSwitches.F, LfxCmdSwitches.Force);

            if (clear && !force)
                throw new Exception("To --clear you must also specify --force.");

            // clear
            if (clear) {
                m_env.ClearCache();
                return;
            }

            // clean
            if (clean) {
                m_env.CleanCache();
                return;
            }

            // dump
        }

        public Task Pull() {
            // pull --exe tools\git https://github.com/git-for-windows/git/releases/download/v2.10.1.windows.1/PortableGit-2.10.1-32-bit.7z.exe "-y -gm2 -InstallPath=\"{0}\""
            // pull --zip packages\Xamarin.Forms https://www.nuget.org/api/v2/package/Xamarin.Forms/2.3.2.127
            // pull nuget.exe https://dist.nuget.org/win-x86-commandline/v3.5.0/NuGet.exe
            return FetchOrPull(isPull: true);
        }
        public Task Fetch() {
            // fetch --exe https://github.com/git-for-windows/git/releases/download/v2.10.1.windows.1/PortableGit-2.10.1-32-bit.7z.exe "-y -gm2 -InstallPath=\"{0}\""
            return FetchOrPull(isPull: false);
        }
        private async Task FetchOrPull(bool isPull) {

            var minArgs = isPull ? 2 : 1;
            var urlArgIndex = isPull ? 1 : 0;

            // parse
            var args = Parse(
                minArgs: minArgs,
                maxArgs: minArgs + 1,
                switchInfo: GitCmdSwitchInfo.Create(
                    LfxCmdSwitches.Quite, LfxCmdSwitches.Q,
                    LfxCmdSwitches.Exe,
                    LfxCmdSwitches.Zip
               )
            );

            // get flags
            var isQuiet = args.IsSet(LfxCmdSwitches.Quite | LfxCmdSwitches.Q);
            var isExe = args.IsSet(LfxCmdSwitches.Exe);
            var isZip = args.IsSet(LfxCmdSwitches.Zip);

            // get args
            var url = new Uri(args[urlArgIndex]);
            var exeArgs = isExe ? args[urlArgIndex + 1] : null;

            // make pointer
            var pointer =
                isExe ? LfxPointer.CreateExe(url, exeArgs) :
                isZip ? LfxPointer.CreateZip(url) :
                LfxPointer.CreateFile(url);

            // log progress
            if (!isQuiet)
                LogProgress();

            // load!
            var entry = await m_env.GetOrLoadEntryAsync(pointer);

            // fetch
            if (!isPull) {
                Log($"{entry.Info}");
                return;
            }

            // ensure pulling only done in lfx subdirectory
            var repoContentPath = Path.GetFullPath(args[0]);
            if (!repoContentPath.IsSubDirOf(m_env.ContentDir))
                throw new ArgumentException(
                    $"Expected path '{repoContentPath}' to be in/a subdirectory of '{m_env.ContentDir}'.");

            // add alias to cached content (wha-bam!)
            entry.Path.AliasPath(repoContentPath);

            // write info to .lfx subdirectory
            var recursiveDir = m_env.ContentDir.GetRecursiveDir(repoContentPath);
            var repoPointerPath = Path.Combine(m_env.InfoDir, recursiveDir, repoContentPath.GetFileName());
            Directory.CreateDirectory(repoPointerPath.GetDir());
            File.WriteAllText(repoPointerPath, entry.Info.ToString());
        }

        public async Task Checkout() {
            // checkout

            var args = Parse(
                minArgs: 0,
                maxArgs: 0,
                switchInfo: GitCmdSwitchInfo.Create(
                    LfxCmdSwitches.Quite,
                    LfxCmdSwitches.Q
               )
            );

            // todo: compute diff to update. for now, nuke lfx
            m_env.ContentDir.DeletePath(force: true);

            // log progress
            var isQuiet = args.IsSet(LfxCmdSwitches.Q, LfxCmdSwitches.Quite);
            LfxProgressTracker progress = null;
            Task progressTask = Task.FromResult(true);

            if (!isQuiet) {
                progress = LogProgress();

                long totalFileSize = 0;
                long totalContentSize = 0;

                progressTask = Task.Run(() => {
                    foreach (var infoPath in m_env.InfoDir.GetAllFiles()) {
                        var repoInfo = m_env.GetRepoInfo(infoPath);
                        if (!repoInfo.HasMetadata)
                            continue;

                        totalContentSize += repoInfo.ContentSize ?? 0;
                        totalFileSize += repoInfo.Size;
                    };

                    progress.SetTotal(LfxProgressType.Download, totalFileSize);
                    progress.SetTotal(LfxProgressType.Expand, totalContentSize);
                });
            }

            // alias every repo info file in parallel
            var restoreTask = m_env.InfoDir.GetAllFiles().ParallelForEachAsync(async infoPath => {
                var repoInfo = m_env.GetRepoInfo(infoPath);
                var cacheEntry = await m_env.GetOrLoadEntryAsync(repoInfo);

                // add alias to cached content (wha-bam!)
                cacheEntry.Path.AliasPath(repoInfo.ContentPath);

                // update repo info file with metadata
                if (repoInfo.Info != cacheEntry.Info)
                    infoPath.WriteAllText(cacheEntry.Info.ToString());
            });

            await restoreTask.JoinWith(progressTask);

            if (!isQuiet)
                progress.Finished();
        }
    }
}