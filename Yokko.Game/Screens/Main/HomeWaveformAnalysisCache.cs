using System;
using System.Collections.Generic;
using Yokko.Audio;

namespace Yokko.Game.Screens.Main;

/// <summary>
/// 主页波形分析结果的 LRU 缓存：命中即刷新热度，超出容量驱逐最久未用的
/// 曲目，避免歌单很大时波形数据无限增长。分析结果由后台线程写入、update
/// 线程读取，因此内部加锁。
/// </summary>
internal sealed class HomeWaveformAnalysisCache
{
    private readonly object syncRoot = new();
    private readonly int capacity;
    private readonly Dictionary<string, LinkedListNode<CachedWaveform>> entries
        = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<CachedWaveform> recency = new();

    internal HomeWaveformAnalysisCache(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        this.capacity = capacity;
    }

    internal int Count
    {
        get
        {
            lock (syncRoot)
                return entries.Count;
        }
    }

    internal bool TryGet(string path, out AudioWaveformAnalysis analysis)
    {
        lock (syncRoot)
        {
            if (!entries.TryGetValue(
                    path,
                    out LinkedListNode<CachedWaveform> node))
            {
                analysis = null;
                return false;
            }

            recency.Remove(node);
            recency.AddFirst(node);
            analysis = node.Value.Analysis;
            return true;
        }
    }

    internal void Store(string path, AudioWaveformAnalysis analysis)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(analysis);

        lock (syncRoot)
        {
            if (entries.TryGetValue(
                    path,
                    out LinkedListNode<CachedWaveform> node))
            {
                node.Value = new CachedWaveform(node.Value.Path, analysis);
                recency.Remove(node);
                recency.AddFirst(node);
                return;
            }

            if (entries.Count >= capacity)
            {
                LinkedListNode<CachedWaveform> oldest = recency.Last!;
                recency.RemoveLast();
                entries.Remove(oldest.Value.Path);
            }

            entries[path] = recency.AddFirst(
                new CachedWaveform(path, analysis));
        }
    }

    /// <summary>
    /// 只查存在性，不影响 LRU 顺序；供测试断言驱逐结果。
    /// </summary>
    internal bool Contains(string path)
    {
        lock (syncRoot)
            return entries.ContainsKey(path);
    }

    private sealed record CachedWaveform(
        string Path,
        AudioWaveformAnalysis Analysis);
}
