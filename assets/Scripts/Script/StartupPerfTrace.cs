using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

public static class StartupPerfTrace
{
    const string LogPrefix = "[StartupPerf]";

    sealed class StepStat
    {
        public readonly Dictionary<string, long> TotalItems = new Dictionary<string, long>(StringComparer.Ordinal);
        public readonly Dictionary<string, int> LastItems = new Dictionary<string, int>(StringComparer.Ordinal);
        public long TotalMilliseconds;
        public long MaxMilliseconds;
        public int CallCount;
        public int MainThreadCalls;
        public int BackgroundThreadCalls;
    }

    public struct Scope : IDisposable
    {
        readonly string _name;
        readonly long _startTimestamp;
        readonly int _threadId;
        readonly bool _enabled;
        Dictionary<string, int> _itemCounts;

        internal Scope(string name, bool enabled)
        {
            _name = name ?? string.Empty;
            _enabled = enabled;
            _threadId = Thread.CurrentThread.ManagedThreadId;
            _startTimestamp = enabled ? Stopwatch.GetTimestamp() : 0L;
            _itemCounts = null;

            if (_enabled)
            {
                Profiler.BeginSample(_name);
            }
        }

        public void SetItemCount(string key, int value)
        {
            if (!_enabled || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            _itemCounts ??= new Dictionary<string, int>(StringComparer.Ordinal);
            _itemCounts[key.Trim()] = value;
        }

        public void AddToItemCount(string key, int delta)
        {
            if (!_enabled || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            _itemCounts ??= new Dictionary<string, int>(StringComparer.Ordinal);
            string normalizedKey = key.Trim();
            _itemCounts.TryGetValue(normalizedKey, out int currentValue);
            _itemCounts[normalizedKey] = currentValue + delta;
        }

        public void Dispose()
        {
            if (!_enabled)
            {
                return;
            }

            Profiler.EndSample();

            long elapsedMilliseconds = ConvertTicksToMilliseconds(Stopwatch.GetTimestamp() - _startTimestamp);
            StartupPerfTrace.RecordScope(_name, elapsedMilliseconds, _threadId, _itemCounts);
        }
    }

    static readonly object Sync = new object();
    static readonly Dictionary<string, StepStat> StatsByName = new Dictionary<string, StepStat>(StringComparer.Ordinal);
    static readonly List<string> BootSaveEvents = new List<string>();

    static bool _configured;
    static bool _enabled;
    static bool _bootStarted;
    static bool _bootCompleted;
    static long _bootStartTimestamp;
    static long _bootCompleteTimestamp;
    static string _label = "baseline";
    static int _mainThreadId;

    static int _getCardSpriteCalls;
    static int _loadCardImageCalls;
    static int _loadCardImageStarted;
    static int _loadCardImageSkippedEmpty;
    static int _loadCardImageSkippedStarted;
    static int _loadCardImageCompleted;
    static int _loadCardImageCompletedWithSprite;
    static int _catalogEnsureCalls;
    static int _catalogRebuildCalls;
    static int _ownedPrintCacheBuilds;
    static int _ownedPrintCacheHits;
    static int _ownedPrintCacheMisses;
    static int _saveAllCalls;
    static int _saveCachedCalls;
    static int _saveDuringBootCalls;

    static bool _skipInitEditDeckOnStartup;
    static bool _skipLoadCardImagesOnStartup;
    static bool _useOwnedPrintLookupCache;

    public static bool Enabled
    {
        get
        {
            ConfigureIfNeeded();
            return _enabled;
        }
    }

    public static bool BootStarted
    {
        get
        {
            ConfigureIfNeeded();
            return _bootStarted;
        }
    }

    public static bool BootCompleted
    {
        get
        {
            ConfigureIfNeeded();
            return _bootCompleted;
        }
    }

    public static string Label
    {
        get
        {
            ConfigureIfNeeded();
            return _label;
        }
    }

    public static bool SkipInitEditDeckOnStartup
    {
        get
        {
            ConfigureIfNeeded();
            return _skipInitEditDeckOnStartup;
        }
    }

    public static bool SkipLoadCardImagesOnStartup
    {
        get
        {
            ConfigureIfNeeded();
            return _skipLoadCardImagesOnStartup;
        }
    }

    public static bool UseOwnedPrintLookupCache
    {
        get
        {
            ConfigureIfNeeded();
            return _useOwnedPrintLookupCache;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ConfigureRuntime()
    {
        ConfigureIfNeeded();
        StartBootIfNeeded("BeforeSceneLoad");
    }

    public static void ConfigureIfNeeded()
    {
        if (_configured)
        {
            return;
        }

        _configured = true;
        _mainThreadId = Thread.CurrentThread.ManagedThreadId;

        string[] args = Environment.GetCommandLineArgs() ?? Array.Empty<string>();
        _enabled = HasFlag(args, "-startupPerfEnable");
        _skipInitEditDeckOnStartup = HasFlag(args, "-startupPerfSkipInitEditDeck");
        _skipLoadCardImagesOnStartup = HasFlag(args, "-startupPerfSkipLoadCardImages");
        _useOwnedPrintLookupCache = HasFlag(args, "-startupPerfUseOwnedPrintCache");
        _label = GetValue(args, "-startupPerfLabel") ?? "baseline";
    }

    public static Scope Measure(string name)
    {
        bool enabled = Enabled;
        if (enabled && !_bootStarted && !_bootCompleted)
        {
            StartBootIfNeeded($"first-scope:{name}");
        }

        return new Scope(name, enabled);
    }

    public static void StartBootIfNeeded(string phase)
    {
        if (!Enabled)
        {
            return;
        }

        lock (Sync)
        {
            if (_bootStarted)
            {
                return;
            }

            _bootStarted = true;
            _bootCompleted = false;
            _bootStartTimestamp = Stopwatch.GetTimestamp();
            _bootCompleteTimestamp = 0L;
            Debug.Log($"{LogPrefix} BOOT_START label={_label} phase={phase} mainThreadId={_mainThreadId} thread={GetThreadLabel(Thread.CurrentThread.ManagedThreadId)}");
        }
    }

    public static void MarkBootComplete(string phase)
    {
        if (!Enabled)
        {
            return;
        }

        lock (Sync)
        {
            if (!_bootStarted || _bootCompleted)
            {
                return;
            }

            _bootCompleted = true;
            _bootCompleteTimestamp = Stopwatch.GetTimestamp();
            Debug.Log($"{LogPrefix} BOOT_COMPLETE label={_label} phase={phase} totalMs={BootElapsedMilliseconds}");
            DumpSummary();
        }
    }

    public static long BootElapsedMilliseconds
    {
        get
        {
            ConfigureIfNeeded();
            if (!_bootStarted)
            {
                return 0L;
            }

            long endTimestamp = _bootCompleted
                ? _bootCompleteTimestamp
                : Stopwatch.GetTimestamp();
            return ConvertTicksToMilliseconds(endTimestamp - _bootStartTimestamp);
        }
    }

    public static void RecordGetCardSpriteCall()
    {
        if (!ShouldRecordBootCounter())
        {
            return;
        }

        lock (Sync)
        {
            _getCardSpriteCalls++;
        }
    }

    public static void RecordLoadCardImageCall(bool skippedEmpty, bool skippedStarted)
    {
        if (!ShouldRecordBootCounter())
        {
            return;
        }

        lock (Sync)
        {
            _loadCardImageCalls++;
            if (skippedEmpty)
            {
                _loadCardImageSkippedEmpty++;
            }
            else if (skippedStarted)
            {
                _loadCardImageSkippedStarted++;
            }
            else
            {
                _loadCardImageStarted++;
            }
        }
    }

    public static void RecordLoadCardImageCompleted(bool hasSprite)
    {
        if (!ShouldRecordBootCounter())
        {
            return;
        }

        lock (Sync)
        {
            _loadCardImageCompleted++;
            if (hasSprite)
            {
                _loadCardImageCompletedWithSprite++;
            }
        }
    }

    public static void RecordCatalogEnsure(bool rebuilt)
    {
        if (!ShouldRecordBootCounter())
        {
            return;
        }

        lock (Sync)
        {
            _catalogEnsureCalls++;
            if (rebuilt)
            {
                _catalogRebuildCalls++;
            }
        }
    }

    public static void RecordOwnedPrintLookupCacheBuild(int ownedPrintCount)
    {
        if (!ShouldRecordBootCounter())
        {
            return;
        }

        lock (Sync)
        {
            _ownedPrintCacheBuilds++;
        }

        Debug.Log($"{LogPrefix} OWNED_PRINT_CACHE_BUILD label={_label} ownedPrints={ownedPrintCount} thread={GetThreadLabel(Thread.CurrentThread.ManagedThreadId)}");
    }

    public static void RecordOwnedPrintLookupCacheHit()
    {
        if (!ShouldRecordBootCounter())
        {
            return;
        }

        lock (Sync)
        {
            _ownedPrintCacheHits++;
        }
    }

    public static void RecordOwnedPrintLookupCacheMiss()
    {
        if (!ShouldRecordBootCounter())
        {
            return;
        }

        lock (Sync)
        {
            _ownedPrintCacheMisses++;
        }
    }

    public static void RecordSaveAll(string reason, int ownedPrintCount, int deckCount)
    {
        if (!Enabled)
        {
            return;
        }

        bool duringBoot = IsDuringBootWindow;

        lock (Sync)
        {
            _saveAllCalls++;
            if (duringBoot)
            {
                _saveDuringBootCalls++;
                BootSaveEvents.Add($"SaveAll:{SanitizeReason(reason)}:ownedPrints={ownedPrintCount}:decks={deckCount}");
            }
        }

        Debug.Log($"{LogPrefix} SAVE_ALL label={_label} duringBoot={duringBoot} reason={SanitizeReason(reason)} ownedPrints={ownedPrintCount} decks={deckCount} thread={GetThreadLabel(Thread.CurrentThread.ManagedThreadId)}");
    }

    public static void RecordSaveCached(string reason, int ownedPrintCount, int deckCount)
    {
        if (!Enabled)
        {
            return;
        }

        bool duringBoot = IsDuringBootWindow;

        lock (Sync)
        {
            _saveCachedCalls++;
            if (duringBoot)
            {
                _saveDuringBootCalls++;
                BootSaveEvents.Add($"SaveCached:{SanitizeReason(reason)}:ownedPrints={ownedPrintCount}:decks={deckCount}");
            }
        }

        Debug.Log($"{LogPrefix} SAVE_CACHED label={_label} duringBoot={duringBoot} reason={SanitizeReason(reason)} ownedPrints={ownedPrintCount} decks={deckCount} thread={GetThreadLabel(Thread.CurrentThread.ManagedThreadId)}");
    }

    static bool ShouldRecordBootCounter()
    {
        return Enabled && IsDuringBootWindow;
    }

    static bool IsDuringBootWindow => _bootStarted && !_bootCompleted;

    static void RecordScope(string name, long elapsedMilliseconds, int threadId, IReadOnlyDictionary<string, int> itemCounts)
    {
        if (!Enabled)
        {
            return;
        }

        lock (Sync)
        {
            if (!StatsByName.TryGetValue(name, out StepStat stat))
            {
                stat = new StepStat();
                StatsByName[name] = stat;
            }

            stat.CallCount++;
            stat.TotalMilliseconds += elapsedMilliseconds;
            stat.MaxMilliseconds = Math.Max(stat.MaxMilliseconds, elapsedMilliseconds);

            if (threadId == _mainThreadId)
            {
                stat.MainThreadCalls++;
            }
            else
            {
                stat.BackgroundThreadCalls++;
            }

            if (itemCounts != null)
            {
                foreach (KeyValuePair<string, int> pair in itemCounts)
                {
                    stat.LastItems[pair.Key] = pair.Value;
                    if (!stat.TotalItems.ContainsKey(pair.Key))
                    {
                        stat.TotalItems[pair.Key] = 0L;
                    }

                    stat.TotalItems[pair.Key] += pair.Value;
                }
            }
        }
    }

    static void DumpSummary()
    {
        List<KeyValuePair<string, StepStat>> rankedSteps;
        string saveSummary;

        lock (Sync)
        {
            rankedSteps = StatsByName
                .OrderByDescending(pair => pair.Value.TotalMilliseconds)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .ToList();

            saveSummary = BootSaveEvents.Count == 0
                ? "none"
                : string.Join("|", BootSaveEvents);
        }

        Debug.Log(
            $"{LogPrefix} COUNTERS label={_label} totalBootMs={BootElapsedMilliseconds} " +
            $"getCardSpriteCalls={_getCardSpriteCalls} loadCardImageCalls={_loadCardImageCalls} " +
            $"loadCardImageStarted={_loadCardImageStarted} loadCardImageSkippedEmpty={_loadCardImageSkippedEmpty} " +
            $"loadCardImageSkippedStarted={_loadCardImageSkippedStarted} loadCardImageCompleted={_loadCardImageCompleted} " +
            $"loadCardImageCompletedWithSprite={_loadCardImageCompletedWithSprite} catalogEnsureCalls={_catalogEnsureCalls} " +
            $"catalogRebuildCalls={_catalogRebuildCalls} ownedPrintCacheBuilds={_ownedPrintCacheBuilds} " +
            $"ownedPrintCacheHits={_ownedPrintCacheHits} ownedPrintCacheMisses={_ownedPrintCacheMisses} " +
            $"saveAllCalls={_saveAllCalls} saveCachedCalls={_saveCachedCalls} saveDuringBootCalls={_saveDuringBootCalls} " +
            $"bootSaveEvents={saveSummary}");

        for (int index = 0; index < rankedSteps.Count; index++)
        {
            KeyValuePair<string, StepStat> pair = rankedSteps[index];
            StepStat stat = pair.Value;
            double averageMilliseconds = stat.CallCount > 0
                ? (double)stat.TotalMilliseconds / stat.CallCount
                : 0d;

            Debug.Log(
                $"{LogPrefix} STEP rank={index + 1} label={_label} name={pair.Key} totalMs={stat.TotalMilliseconds} " +
                $"avgMs={averageMilliseconds:F2} maxMs={stat.MaxMilliseconds} count={stat.CallCount} " +
                $"mainThreadCalls={stat.MainThreadCalls} backgroundCalls={stat.BackgroundThreadCalls} " +
                $"totalItems={FormatLongDictionary(stat.TotalItems)} lastItems={FormatIntDictionary(stat.LastItems)}");
        }
    }

    static string FormatLongDictionary(IReadOnlyDictionary<string, long> values)
    {
        if (values == null || values.Count == 0)
        {
            return "none";
        }

        return string.Join(",", values
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}:{pair.Value}"));
    }

    static string FormatIntDictionary(IReadOnlyDictionary<string, int> values)
    {
        if (values == null || values.Count == 0)
        {
            return "none";
        }

        return string.Join(",", values
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}:{pair.Value}"));
    }

    static long ConvertTicksToMilliseconds(long ticks)
    {
        return (long)Math.Round(ticks * 1000d / Stopwatch.Frequency);
    }

    static bool HasFlag(IReadOnlyList<string> args, string flag)
    {
        if (args == null || string.IsNullOrWhiteSpace(flag))
        {
            return false;
        }

        for (int index = 0; index < args.Count; index++)
        {
            if (string.Equals(args[index], flag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    static string GetValue(IReadOnlyList<string> args, string flag)
    {
        if (args == null || string.IsNullOrWhiteSpace(flag))
        {
            return null;
        }

        for (int index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], flag, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    static string GetThreadLabel(int threadId)
    {
        return threadId == _mainThreadId
            ? $"main({threadId})"
            : $"background({threadId})";
    }

    static string SanitizeReason(string reason)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? "unspecified"
            : reason.Trim().Replace("\n", " ").Replace("\r", " ");
    }
}
