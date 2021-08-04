using System;
using Cysharp.Threading.Tasks;

namespace NetworkCache
{
    public interface ICacheStore
    {
        UniTask Save(string key, byte[] data, TimeSpan expiresIn);
        UniTask<(byte[], bool)> Load(string key);
        UniTask DeleteExpired();
    }
}