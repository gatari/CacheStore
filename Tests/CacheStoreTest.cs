using System;
using System.Collections;
using System.Text;
using Cysharp.Threading.Tasks;
using NetworkCache;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class CacheStoreTest
{
    [UnityTest]
    public IEnumerator TestCacheStore()
    {
        var store = new CacheStore(Application.temporaryCachePath);
        yield return UniTask.Run(async () =>
            {
                await store.DeleteExpired();
                await store.Save("hoge", Encoding.Default.GetBytes("Hello World"), TimeSpan.FromSeconds(10));
                await store.Save("huga", Encoding.Default.GetBytes("This should expires soon"), TimeSpan.Zero);

                var loadHogeResult = await store.Load("hoge");
                Assert.AreEqual(loadHogeResult.Item2, true);
                Assert.AreEqual(loadHogeResult.Item1, Encoding.Default.GetBytes("Hello World"));

                var loadHugaResult = await store.Load("huga");
                Assert.AreEqual(loadHugaResult.Item2, false);

                var loadPiyoResult = await store.Load("piyo");
                Assert.AreEqual(loadHugaResult.Item2, false);
            })
            .ToCoroutine();
    }
}