﻿using System;
using System.Text;
using System.Security.Cryptography;
using IOPath = System.IO.Path;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.IO.Compression;

namespace Git.Lfs {

    public class TempDir : IDisposable, IEnumerable<string> {
        public static implicit operator string(TempDir tempDir) => tempDir.ToString();

        private readonly string m_tempDir;

        public TempDir()
            : this(IOPath.Combine(IOPath.GetTempPath(), IOPath.GetRandomFileName())) {
        }
        public TempDir(string tempDir) {
            Directory.CreateDirectory(tempDir);
            m_tempDir = tempDir;
            if (!m_tempDir.EndsWith($"{IOPath.DirectorySeparatorChar}"))
                m_tempDir += IOPath.DirectorySeparatorChar;
        }

        public string Path => m_tempDir;

        public void Dispose() => Directory.Delete(m_tempDir, recursive: true);

        public IEnumerator<string> GetEnumerator() =>
            Directory.GetFiles(m_tempDir, "*", SearchOption.AllDirectories)
                .Cast<string>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => Path;
    }
    public class TempFile : IDisposable {
        public static implicit operator string(TempFile tempFile) => tempFile.ToString();

        private readonly string m_path;

        public static TempFile Create(string content) {
            var tempFile = new TempFile();
            File.WriteAllText(tempFile, content);
            return tempFile;
        }

        public TempFile() 
            : this(IOPath.GetTempFileName()) {
        }
        public TempFile(string path) {
            m_path = path;
        }

        public string Path => m_path;
        public void Dispose() => File.Delete(m_path);

        public override string ToString() => Path;
    }

    public static class Extensions {
        public static void Download(this Uri uri, string file) {
            new WebClient().DownloadFile(uri, file);
        }
        public static TempFile DownloadToTempFile(this Uri url) {
            var tempFile = Path.GetTempFileName();
            File.Delete(tempFile);
            url.Download(tempFile);
            return new TempFile(tempFile);
        }
        public static TempDir DownloadAndUnZip(this Uri url) {
            using (var tempFile = DownloadToTempFile(url)) {
                var tempDir = new TempDir();
                ZipFile.ExtractToDirectory(tempFile, tempDir);
                return new TempDir(tempDir);
            }
        }
        public static Uri ToUrl(this string value, UriKind kind = UriKind.Absolute) {
            Uri url;
            if (!Uri.TryCreate(value, kind, out url))
                throw new Exception($"Expected '{value}' to be '{kind}' url.");
            return url;
        }
        public static Stream ToMemoryStream(this Stream stream) {
            var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms;
        }

        public static LfsHash ComputeHash(this string value) => LfsHash.Compute(value);
        public static LfsHash ComputeHash(this Stream stream) => LfsHash.Compute(stream);
        public static LfsHash ComputeHash(this byte[] bytes) => LfsHash.Compute(bytes);
        public static LfsHash ComputeHash(this byte[] bytes, int count) => LfsHash.Compute(bytes, count);
    }
}