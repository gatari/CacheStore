using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using SQLite;

namespace NetworkCache
{
    public class CacheStore : ICacheStore
    {
        private readonly AsyncLazy<SQLiteAsyncConnection> _connection;
        private readonly string _cacheDataDir;

        public CacheStore(string cacheRoot)
        {
            var dbPath = Path.Combine(cacheRoot, "CacheStore.db");

            _cacheDataDir = Path.Combine(cacheRoot, "Cache");
            if (!Directory.Exists(_cacheDataDir))
            {
                Directory.CreateDirectory(_cacheDataDir);
            }

            _connection = new AsyncLazy<SQLiteAsyncConnection>(async () =>
            {
                var con = new SQLiteAsyncConnection(dbPath);
                await con.CreateTableAsync<CacheItem>();
                return con;
            });
        }

        public async UniTask Save(string key, byte[] data, TimeSpan expiresIn)
        {
            var hash = Hash(key);
            var entry = new CacheItem()
            {
                Key = hash,
                ExpireTime = DateTime.Now.Add(expiresIn)
            };

            if (!Directory.Exists(_cacheDataDir))
            {
                Directory.CreateDirectory(_cacheDataDir);
            }

            var con = await _connection;
            await con.InsertOrReplaceAsync(entry);
            var newCachePath = Path.Combine(_cacheDataDir, hash);
            using var fileStream = new FileStream(newCachePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await fileStream.WriteAsync(data, 0, data.Length);
        }

        public async UniTask<(byte[], bool)> Load(string key)
        {
            var hash = Hash(key);
            var con = await _connection;
            var query = await con.Table<CacheItem>().FirstOrDefaultAsync(i => i.Key == hash);
            if (query == default)
            {
                return (null, false);
            }

            if (query.IsExpired())
            {
                return (null, false);
            }

            var cachePath = Path.Combine(_cacheDataDir, hash);
            if (!File.Exists(cachePath))
            {
                return (null, false);
            }

            using var fileStream = new FileStream(cachePath, FileMode.Open, FileAccess.Read);
            var data = new byte[fileStream.Length];
            await fileStream.ReadAsync(data, 0, data.Length);
            return (data, true);
        }

        public async UniTask DeleteExpired()
        {
            var con = await _connection;
            var expired = await con.Table<CacheItem>().Where(i => i.ExpireTime < DateTime.Now).ToListAsync();
            foreach (var cachePath in expired.Select(e => Path.Combine(_cacheDataDir, e.Key)))
            {
                using var fileStream = new FileStream(cachePath, FileMode.Open, FileAccess.Write, FileShare.None, 1,
                    FileOptions.DeleteOnClose);
                await fileStream.FlushAsync();
            }
        }

        private static string Hash(string input)
        {
            var md5Hasher = MD5.Create();
            var data = md5Hasher.ComputeHash(Encoding.Default.GetBytes(input));
            return BitConverter.ToString(data);
        }

        public async UniTask DeleteAll()
        {
            var con = await _connection;
            await con.DeleteAllAsync<CacheItem>();
        }
    }

    internal class CacheItem
    {
        [PrimaryKey] public string Key { get; set; }
        [Indexed] public DateTime ExpireTime { get; set; }

        public bool IsExpired()
        {
            return ExpireTime < DateTime.Now;
        }
    }
}