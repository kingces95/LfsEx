﻿using System;
using System.Collections.Generic;
using System.IO;

namespace Git.Lfs {

    public sealed class LfsLoader {
        public static readonly string DefaultLfsUserDir = 
            Environment.GetEnvironmentVariable("APPDATA") + 
                Path.DirectorySeparatorChar + "lfs";

        public static LfsLoader Create(string storeDir = null) {
            var loader = new LfsLoader();
            if (storeDir != null) {
                Directory.CreateDirectory(storeDir);
                var cache = new LfsBlobCache(loader, storeDir);
                loader.m_cache = cache;
            }
            return loader;
        }

        private LfsBlobCache m_cache;
        private readonly Dictionary<LfsHash, LfsObject> m_objects; 
        private readonly Dictionary<FileInfo, LfsConfigFile> m_configFiles;
        private readonly Dictionary<FileInfo, LfsFile> m_files;

        private LfsLoader() {
            m_objects = new Dictionary<LfsHash, LfsObject>();
            m_configFiles = new Dictionary<FileInfo, LfsConfigFile>();
            m_files = new Dictionary<FileInfo, LfsFile>();
        }

        public LfsBlobCache Cache => m_cache;
        public LfsFile GetFile(string path) {
            var file = new FileInfo(path);

            LfsFile result;
            if (!m_files.TryGetValue(file, out result))
                m_files[file] = result = LfsFile.Create(this, path);

            return result;
        }
        public LfsConfigFile GetConfigFile(string path) {
            var file = new FileInfo(path);

            LfsConfigFile result;
            if (!m_configFiles.TryGetValue(file, out result))
                m_configFiles[file] = result = new LfsConfigFile(this, path);

            return result;
        }
        public LfsObject GetObject(LfsPointer pointer) {
            if (m_cache == null)
                throw new InvalidOperationException("Expected local cache director at activation.");

            var hash = pointer.Hash;

            LfsObject result;
            if (!m_objects.TryGetValue(hash, out result))
                m_objects[hash] = result = new LfsObject(this, m_cache.Load(pointer));

            return result;
        }
    }
}