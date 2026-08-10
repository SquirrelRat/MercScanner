using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Helpers;

namespace MercScanner;

public class FlameLinkController
{
    private const string LinkBuffMarker = "flame_link";

    private enum CastPhase
    {
        Idle,
        Aiming,
        Pressing,
        Releasing,
    }

    private readonly MercScanner _plugin;
    private readonly Stopwatch _phaseClock = Stopwatch.StartNew();
    private readonly Stopwatch _sinceLastCast = Stopwatch.StartNew();

    private int CastLockoutMs => _plugin.Settings.RelinkCooldownMs.Value;

    private readonly Dictionary<uint, long> _lastCastByMerc = new();

    private readonly List<Entity> _aliveScratch = new();
    private readonly List<Entity> _inRangeScratch = new();
    private readonly HashSet<uint> _activeIdsScratch = new();
    private readonly List<uint> _staleIdsScratch = new();

    private CastPhase _phase = CastPhase.Idle;
    private System.Numerics.Vector2 _cursorBeforeCast;
    private uint? _linkedMercId;
    private uint? _castTargetId;
    private string _lastTarget = "";

    public string BlockedReason { get; private set; } = "Disabled";
    public int CastCount { get; private set; }

    private Keys? LinkKey => _plugin.Settings.FlameLinkKey.Value?.Key;

    public FlameLinkController(MercScanner plugin)
    {
        _plugin = plugin;
    }

    public void Update(IReadOnlyList<Entity> mercs)
    {
        if (mercs.Count == 0)
        {
            if (_phase != CastPhase.Idle) AbortCast();
            return;
        }

        PruneCastLockouts(mercs);

        if (_phase != CastPhase.Idle)
        {
            Advance();
            return;
        }

        if (!CanCast(out var reason))
        {
            BlockedReason = reason;
            return;
        }

        var target = FindLinkTarget(mercs);
        if (target == null)
        {
            BlockedReason = "All mercs linked";
            return;
        }

        BlockedReason = "Casting";
        StartCast(target);
    }

    private bool RecentlyCast(uint mercId) =>
        _lastCastByMerc.TryGetValue(mercId, out var lastCast) &&
        Environment.TickCount64 - lastCast < CastLockoutMs;

    public bool IsLinked(Entity merc)
    {
        if (merc is not { IsValid: true }) return false;

        if (!merc.TryGetComponent<Buffs>(out var buffs) || buffs?.BuffsList == null) return true;

        if (RecentlyCast(merc.Id)) return true;

        return HasLinkBuff(merc);
    }

    private bool HasLinkBuff(Entity merc)
    {
        if (!merc.TryGetComponent<Buffs>(out var buffs) || buffs?.BuffsList == null) return false;

        var playerId = _plugin.GameController.Player?.Id ?? 0;
        foreach (var buff in buffs.BuffsList)
        {
            var name = buff?.Name;
            if (string.IsNullOrEmpty(name)) continue;
            if (!name.Contains(LinkBuffMarker, StringComparison.OrdinalIgnoreCase)) continue;
            if (playerId != 0 && buff.SourceEntityId != 0 && buff.SourceEntityId != playerId) continue;
            return true;
        }

        return false;
    }

    private bool HoldsActiveLink(Entity merc) =>
        HasLinkBuff(merc) || RecentlyCast(merc.Id);

    public int EffectiveGapMs
    {
        get
        {
            var settings = _plugin.Settings;
            if (FindLinkSkill() is { } skill && skill.CastTime.TotalMilliseconds > 0)
                return (int)skill.CastTime.TotalMilliseconds + settings.CastMarginMs.Value;
            return settings.CastGapMs.Value;
        }
    }

    private bool CanCast(out string reason)
    {
        var settings = _plugin.Settings;

        if (!settings.AutoCastFlameLink.Value)
        {
            reason = "Auto-cast is off";
            return false;
        }

        if (settings.FlameLinkKey.Value?.Key is null or Keys.None)
        {
            reason = "No link key set";
            return false;
        }

        if (_sinceLastCast.ElapsedMilliseconds < EffectiveGapMs)
        {
            reason = "Pacing casts";
            return false;
        }

        if (EnvironmentUnsafe(out reason)) return false;

        if (settings.RequireSkillReady.Value && FindLinkSkill() is not { CanBeUsed: true })
        {
            reason = "Link skill not ready";
            return false;
        }

        reason = "Ready";
        return true;
    }

    private bool EnvironmentUnsafe(out string reason)
    {
        var settings = _plugin.Settings;
        var gc = _plugin.GameController;

        if (MenuWindow.IsOpened)
        {
            reason = "Settings menu open";
            return true;
        }

        if (!gc.IsForeGroundCache)
        {
            reason = "Game not focused";
            return true;
        }

        if (gc.Game?.IsEscapeState == true)
        {
            reason = "Escape menu open";
            return true;
        }

        if (gc.Player is not { IsValid: true } player || !player.TryGetComponent<Life>(out var life) || life == null || life.CurHP <= 0)
        {
            reason = "Player dead";
            return true;
        }

        var area = gc.Area?.CurrentArea;
        if (settings.DontCastInTown.Value && area is { IsTown: true } or { IsHideout: true })
        {
            reason = "In town or hideout";
            return true;
        }

        if (gc.IngameState.FocusedInputElement != null)
        {
            reason = "Text input focused";
            return true;
        }

        if (gc.IngameState.IngameUi.ChatTitlePanel?.IsVisible == true)
        {
            reason = "Chat open";
            return true;
        }

        if (settings.DontCastWithPanelsOpen.Value && AnyPanelOpen)
        {
            reason = "A panel is open";
            return true;
        }

        reason = "";
        return false;
    }

    private bool AnyPanelOpen
    {
        get
        {
            var ui = _plugin.GameController.IngameState.IngameUi;
            return ui.OpenLeftPanel?.IsVisible == true ||
                   ui.OpenRightPanel?.IsVisible == true ||
                   (ui.LargePanels?.Any(p => p.IsVisible) ?? false) ||
                   (ui.FullscreenPanels?.Any(p => p.IsVisible) ?? false);
        }
    }

    private ActorSkill FindLinkSkill()
    {
        var player = _plugin.GameController.Player;
        if (player == null || !player.TryGetComponent<Actor>(out var actor) || actor?.ActorSkills == null) return null;

        foreach (var skill in actor.ActorSkills)
        {
            if (skill?.InternalName == null) continue;
            if (skill.InternalName.Contains(LinkBuffMarker, StringComparison.OrdinalIgnoreCase)) return skill;
        }

        return null;
    }

    private void PruneCastLockouts(IReadOnlyList<Entity> mercs)
    {
        if (_lastCastByMerc.Count == 0 && _linkedMercId == null) return;

        _activeIdsScratch.Clear();
        foreach (var m in mercs)
        {
            if (m is { IsValid: true, IsAlive: true })
                _activeIdsScratch.Add(m.Id);
        }

        var now = Environment.TickCount64;
        _staleIdsScratch.Clear();
        foreach (var (id, lastCast) in _lastCastByMerc)
        {
            if (!_activeIdsScratch.Contains(id) || now - lastCast >= CastLockoutMs)
                _staleIdsScratch.Add(id);
        }
        foreach (var id in _staleIdsScratch) _lastCastByMerc.Remove(id);

        if (_linkedMercId != null && !_activeIdsScratch.Contains(_linkedMercId.Value))
            _linkedMercId = null;
    }

    private Entity FindLinkTarget(IReadOnlyList<Entity> mercs)
    {
        var maxDistance = _plugin.Settings.MaxCastDistance.Value;

        _aliveScratch.Clear();
        foreach (var m in mercs)
        {
            if (m is { IsValid: true, IsAlive: true })
                _aliveScratch.Add(m);
        }

        var currentlyLinked = _aliveScratch.FirstOrDefault(HoldsActiveLink);
        if (currentlyLinked != null)
            _linkedMercId = currentlyLinked.Id;

        if (_linkedMercId != null && !_aliveScratch.Any(m => m.Id == _linkedMercId.Value))
            _linkedMercId = null;

        _inRangeScratch.Clear();
        if (maxDistance <= 0)
        {
            _inRangeScratch.AddRange(_aliveScratch);
        }
        else
        {
            foreach (var m in _aliveScratch)
            {
                if (m.DistancePlayer <= maxDistance)
                    _inRangeScratch.Add(m);
            }
        }

        if (_linkedMercId != null)
        {
            var tracked = _aliveScratch.FirstOrDefault(m => m.Id == _linkedMercId.Value);
            var trackedInRange = _inRangeScratch.Any(m => m.Id == _linkedMercId.Value);

            if (tracked == null || (!IsLinked(tracked) && !trackedInRange))
            {
                _linkedMercId = null;
            }
            else
            {
                return trackedInRange && !IsLinked(tracked) ? tracked : null;
            }
        }

        Entity nearest = null;
        var nearestDistance = float.MaxValue;
        foreach (var m in _inRangeScratch)
        {
            if (!TryGetScreenPos(m, out _)) continue;
            if (m.DistancePlayer < nearestDistance)
            {
                nearest = m;
                nearestDistance = m.DistancePlayer;
            }
        }
        return nearest;
    }

    private void StartCast(Entity target)
    {
        if (!TryGetScreenPos(target, out var screenPos)) return;

        _cursorBeforeCast = Input.MousePositionNum;
        _castTargetId = target.Id;
        _lastTarget = target.RenderName ?? "Mercenary";
        Input.SetCursorPos(WindowTopLeft() + screenPos);
        _phase = CastPhase.Aiming;
        _phaseClock.Restart();
    }

    private void AbortCast()
    {
        if (LinkKey is { } key && Input.GetKeyState(key))
            Input.KeyUp(key);
        if (_plugin.Settings.RestoreCursor.Value)
            Input.SetCursorPos(_cursorBeforeCast);
        _phase = CastPhase.Idle;
    }

    private void Advance()
    {
        var settings = _plugin.Settings;

        if (EnvironmentUnsafe(out var abortReason))
        {
            AbortCast();
            BlockedReason = abortReason;
            return;
        }

        switch (_phase)
        {
            case CastPhase.Aiming:
                if (_phaseClock.ElapsedMilliseconds < settings.CursorSettleMs.Value) return;

                if (LinkKey is { } key)
                {
                    Input.KeyDown(key);
                    CastCount++;
                    if (_castTargetId != null)
                        _lastCastByMerc[_castTargetId.Value] = Environment.TickCount64;
                    _plugin.LogMessage($"Flame Link auto-cast #{CastCount} on {_lastTarget}");
                }

                _phase = CastPhase.Pressing;
                _phaseClock.Restart();
                return;

            case CastPhase.Pressing:
                if (_phaseClock.ElapsedMilliseconds < 30) return;

                if (LinkKey is { } releaseKey)
                    Input.KeyUp(releaseKey);

                _sinceLastCast.Restart();
                _phase = CastPhase.Releasing;
                _phaseClock.Restart();
                return;

            case CastPhase.Releasing:
                if (_phaseClock.ElapsedMilliseconds < settings.CursorRestoreMs.Value) return;

                if (LinkKey is { } verifyKey && Input.GetKeyState(verifyKey))
                    Input.KeyUp(verifyKey);

                if (settings.RestoreCursor.Value) Input.SetCursorPos(_cursorBeforeCast);

                _phase = CastPhase.Idle;
                return;
        }
    }

    private bool TryGetScreenPos(Entity merc, out System.Numerics.Vector2 screenPos)
    {
        screenPos = default;
        if (merc is not { IsValid: true }) return false;

        var world = _plugin.GameController.IngameState.Camera.WorldToScreen(merc.PosNum);
        var window = _plugin.GameController.Window.GetWindowRectangleTimeCache;
        if (world.X <= 0 || world.Y <= 0 || world.X >= window.Width || world.Y >= window.Height) return false;

        screenPos = new System.Numerics.Vector2(world.X, world.Y);
        return true;
    }

    private System.Numerics.Vector2 WindowTopLeft() =>
        _plugin.GameController.Window.GetWindowRectangleTimeCache.TopLeft.ToVector2Num();
}
