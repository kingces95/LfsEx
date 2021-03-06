﻿using Git.Lfx.Test;
using Lfx;
using NUnit.Framework;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Xml.Linq;

namespace Git.Lfx.Live.Test {

    [TestFixture]
    public class EndToEndTest : LfxTest {
        public const string ToolsDirName = "tools";
        public const string SourceDirName = "source";
        public const string TargetDirName = "target";
        public const string RemoteDirName = "remote";

        public const string XPackages = "packages";
        public const string XPackage = "package";
        public const string XId = "id";
        public const string XVersion = "version";

        public const string LfxConfig = LfxConfigFile.FileName;
        public const string GitAttributes = ".gitattributes";
        public const string GitIgnore = ".gitignore";

        public const string lfx = "lfx";
        public static readonly string NugetGitAttributes = 
            $"*/** filter={lfx} diff={lfx} merge={lfx} -text" + Nl +
            $"{LfxConfigFile.FileName} eol=lf";
        public static readonly string NugetGitIgnore = "*.nupkg";
        public static readonly string NugetType = "archive";
        public static readonly string NugetUrl = @"http://nuget.org/api/v2/package/${id}/${ver}";
        public static readonly string NugetRegex = @"^((?<id>.*?)[.])(?=\d)(?<ver>[^/]*)/(?<path>.*)$";
        public static readonly string NugetHint = @"${path}";

        public static readonly XElement PackagesConfig =
            new XElement(XPackages,
                new XElement(XPackage,
                    new XAttribute(XId, "NUnit"),
                    new XAttribute(XVersion, "2.6.4")
                )
                //new XElement(XPackage,
                //    new XAttribute(XId, "NUnit.Runners"),
                //    new XAttribute(XVersion, "2.6.4")
                //)
            );

        [Test]
        public static void Test() {
        }

        [Test]
        public static void ManualTest() {
            Console.WriteLine($"currentDir: {Environment.CurrentDirectory}");

            using (var tempDir = new TempDir()) {
                var tempDirString = (string)tempDir;
                Console.WriteLine($"tempDir: {tempDir}");

                using (var env = new TempCurDir(ToolsDirName)) {

                    // copy git-lfx.exe and libs to tools dir
                    typeof(ImmutableDictionary).Assembly.Location.CopyToDir();
                    typeof(GitCmd).Assembly.Location.CopyToDir();
                    typeof(LfxCmd).Assembly.Location.CopyToDir();
                    typeof(Program).Assembly.Location.CopyToDir();

                    // add tools dir to path
                    Environment.SetEnvironmentVariable("PATH",
                        Environment.GetEnvironmentVariable("PATH") + ";" + env
                    );
                }

                using (var env = new TempCurDir(RemoteDirName)) {
                    Git($"init --bare");
                }

                using (var env = new TempCurDir(SourceDirName)) {
                    Git($"init");
                    Git($"remote add origin ..\\{RemoteDirName}");

                    Git($"config --add filter.lfx.clean \"git-lfx clean %f\"");
                    Git($"config --add filter.lfx.smudge \"git-lfx smudge --\"");
                    Git($"config --get-regex .*lfx.*");

                    PackagesConfig.Save("packages.config");

                    using (var packages = new TempCurDir("packages")) {
                        File.WriteAllText(GitAttributes, NugetGitAttributes);
                        File.WriteAllText(GitIgnore, NugetGitIgnore);

                        var gitConfig = GitConfig.Load();
                        Assert.AreEqual(env.ToString(), gitConfig.EnlistmentDirectory);

                        Git($"config -f {LfxConfig} --add {LfxConfigFile.TypeId} {NugetType}");
                        Git($"config -f {LfxConfig} --add {LfxConfigFile.UrlId} {NugetUrl}");
                        Git($"config -f {LfxConfig} --add {LfxConfigFile.PatternId} {NugetRegex}");
                        Git($"config -f {LfxConfig} --add {LfxConfigFile.HintId} {NugetHint}");

                        Console.WriteLine($"{LfxConfig}: {Path.GetFullPath(LfxConfig)}:");
                        Console.WriteLine(File.ReadAllText(LfxConfig));
                    }

                    Nuget($"restore -packagesDirectory packages");
                    Cmd($"where.exe", "git-lfx.exe");
                    Git($"add .");
                    Git($"commit -m \"Initial Commit\"");
                    Git($"push -u origin master");
                }

                Git($"clone remote target");

                Console.WriteLine($"CurDir: {Environment.CurrentDirectory}");
            }
        }
    }
}
