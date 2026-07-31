using MC_Helper.Helpers;
using MC_Helper.Models;

namespace MC_Helper.Services;

public enum FishingState
{
    Idle,
    Fishing,
    ReelingIn,
    ReeledIn
}

public class FishingStateMachine
{
    private readonly FishingSettings _settings;
    private DateTime _stateEnteredAt;
    private bool _cooldownActive;

    public FishingState CurrentState { get; private set; } = FishingState.Idle;

    public event Action<string>? RightClickRequested;
    public event Action<FishingState, FishingState>? StateChanged;
    public event Action<int>? RodBrokenDetected;
    public event Action<string>? DebugInfo;

    public FishingStateMachine(FishingSettings settings)
    {
        _settings = settings;
        _stateEnteredAt = DateTime.Now;
    }

    public void Process(IReadOnlyList<string> textLines)
    {
        var now = DateTime.Now;

        switch (CurrentState)
        {
            case FishingState.Idle:
                ProcessIdle(textLines);
                break;

            case FishingState.Fishing:
                ProcessFishing(textLines, now);
                break;

            case FishingState.ReelingIn:
                ProcessReelingIn(textLines);
                break;

            case FishingState.ReeledIn:
                ProcessReeledIn(now);
                break;
        }
    }

    public void Reset()
    {
        TransitionTo(FishingState.Idle, "手动重置");
    }

    private int _idleDebugCounter;

    private void ProcessIdle(IReadOnlyList<string> textLines)
    {
        if (!_settings.AutoFishEnabled) return;

        if (FuzzyMatchAny(textLines, _settings.CastPhrases))
        {
            _cooldownActive = true;
            TransitionTo(FishingState.Fishing, "检测到抛竿");
        }
        else if (_settings.AutoRecastFromIdleEnabled
            && (DateTime.Now - _stateEnteredAt).TotalMilliseconds >= _settings.AutoRecastFromIdleDelayMs)
        {
            DebugInfo?.Invoke($"[状态机] Idle 超时 {(DateTime.Now - _stateEnteredAt).TotalSeconds:F0}s，自动抛竿");
            RightClickRequested?.Invoke("Idle 超时自动抛竿");
            _cooldownActive = true;
            TransitionTo(FishingState.Fishing, "Idle 超时自动抛竿");
        }
        else if (textLines.Count > 0 && ++_idleDebugCounter % 10 == 0)
        {
            var sample = string.Join(" | ", textLines.Take(3));
            DebugInfo?.Invoke($"未匹配: [{sample}]");
        }
    }

    private void ProcessFishing(IReadOnlyList<string> textLines, DateTime now)
    {
        if (!_settings.AutoFishEnabled) return;

        if (_cooldownActive)
        {
            var elapsed = (now - _stateEnteredAt).TotalMilliseconds;
            if (elapsed < _settings.CastCooldownMs)
                return;
            _cooldownActive = false;
        }

        // 超时兜底：超过 FishingTimeoutMs 未检测到咬钩，强制重抛（可能勾到溺尸等）
        var fishingElapsed = (now - _stateEnteredAt).TotalMilliseconds;
        if (fishingElapsed >= _settings.FishingTimeoutMs)
        {
            DebugInfo?.Invoke($"[状态机] Fishing 超时 {fishingElapsed / 1000:F0}s，强制重抛");
            RightClickRequested?.Invoke("钓鱼超时重抛");
            _cooldownActive = true;
            TransitionTo(FishingState.Fishing, "超时强制重抛");
            return;
        }

        if (FuzzyMatchAny(textLines, _settings.BitePhrases))
        {
            RightClickRequested?.Invoke(string.Join(", ", textLines));
            TransitionTo(FishingState.ReelingIn, "检测到咬钩");
            return;
        }

        if (FuzzyMatchAny(textLines, _settings.ReelPhrases))
            TransitionTo(FishingState.Idle, "用户主动收杆");
    }

    private void ProcessReelingIn(IReadOnlyList<string> textLines)
    {
        if (!_settings.AutoFishEnabled) return;

        // 超时兜底：若提杆后超过 ReelingInTimeoutMs (默认 6000ms) 仍未识别到"浮漂收回"，
        // 直接发右键抛竿 + 回到 Fishing（鱼还在手里，重新试）
        var elapsed = (DateTime.Now - _stateEnteredAt).TotalMilliseconds;
        if (elapsed >= _settings.ReelingInTimeoutMs)
        {
            DebugInfo?.Invoke($"[状态机] ReelingIn 超时 {elapsed:F0}ms，强制重抛");
            RightClickRequested?.Invoke("ReelingIn 超时重抛");
            _cooldownActive = true;
            TransitionTo(FishingState.Fishing, "超时强制重抛");
            return;
        }

        if (FuzzyMatchAny(textLines, _settings.ReelPhrases))
        {
            var brokenPhrases = _settings.BrokenPhrases;
            var autoSwitch = _settings.AutoSwitchRodEnabled;
            var hasBroken = brokenPhrases != null && brokenPhrases.Count > 0;
            var matchedBroken = hasBroken && FuzzyMatchAny(textLines, brokenPhrases!);

            if (_settings.DebugLogInput && hasBroken)
            {
                var joined = string.Join(" | ", textLines);
                Logger.Info($"切杆检测: AutoSwitch={autoSwitch}, BrokenCnt={brokenPhrases!.Count}, "
                    + $"Matched={matchedBroken}, OCR=[{joined}]");
            }

            if (autoSwitch && matchedBroken)
            {
                RodBrokenDetected?.Invoke(0);
                TransitionTo(FishingState.Idle, "鱼竿损坏，等待切杆");
            }
            else
            {
                TransitionTo(FishingState.ReeledIn, "检测到收回");
            }
        }
    }

    private void ProcessReeledIn(DateTime now)
    {
        if (!_settings.AutoFishEnabled) return;

        var elapsed = (now - _stateEnteredAt).TotalMilliseconds;
        if (elapsed >= _settings.RecastDelayMs)
        {
            RightClickRequested?.Invoke("自动重抛");
            _cooldownActive = true;
            TransitionTo(FishingState.Fishing, "自动重抛");
        }
    }

    private void TransitionTo(FishingState newState, string reason)
    {
        var oldState = CurrentState;
        CurrentState = newState;
        _stateEnteredAt = DateTime.Now;
        DebugInfo?.Invoke($"[状态机] {oldState} → {newState} ({reason})");
        StateChanged?.Invoke(oldState, newState);
    }

    /// <summary>
    /// 模糊匹配 — OCR 可能误识别个别字符（如"甩出"→"用出"）。
    /// 短语中 ≥70% 的字符按顺序出现在同一行 OCR 文本中即视为匹配。
    /// </summary>
    private bool FuzzyMatchAny(IReadOnlyList<string> textLines, List<string> phrases)
    {
        var threshold = _settings.FuzzyMatchThreshold;

        foreach (var line in textLines)
        {
            var normalized = Normalize(line);
            if (normalized.Length == 0) continue;

            foreach (var phrase in phrases)
            {
                var np = Normalize(phrase);
                if (np.Length == 0) continue;

                // 精确匹配优先
                if (normalized.Contains(np)) return true;

                // 模糊匹配：检查 phrase 中多少字符按顺序出现在 line 中
                int matched = 0;
                int searchFrom = 0;
                foreach (var ch in np)
                {
                    var idx = normalized.IndexOf(ch, searchFrom);
                    if (idx >= 0)
                    {
                        matched++;
                        searchFrom = idx + 1;
                    }
                }

                if ((double)matched / np.Length >= threshold)
                    return true;
            }
        }
        return false;
    }

    private static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var result = text.ToLowerInvariant()
            .Replace('：', ':')
            .Replace('，', ',')
            .Replace('（', '(')
            .Replace('）', ')');

        result = result.Replace(" ", "").Replace("\u00A0", "");

        var chars = new char[result.Length];
        int pos = 0;
        foreach (var c in result)
        {
            if (char.IsLetterOrDigit(c))
                chars[pos++] = c;
        }

        return new string(chars, 0, pos);
    }
}
