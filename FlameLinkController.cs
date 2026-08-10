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

    // After we cast at a merc, treat it as freshly linked for this long even if
    // its buff read momentarily flaps. This is the real anti-spam: the link is
    // applied by our own cast, and the buff list on a merc can briefly read
    // empty, which must not trigger a recast. Distinct from CastGapMs (the
    // global pacing between casts): this is the per-merc window during which a
    // just-cast merc is not considered for another cast.
    private int CastLockoutMs => _plugin.Settings.RelinkCooldownMs.Value;

    private readonly Dictionary<uint, long> _lastCastByMerc = new();

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

    public bool IsLinked(Entity merc)
    {
        if (merc is not { IsValid: true }) return false;

        // A buff list we cannot read is treated as linked: on zone change the
        // buffs of a merc can briefly read empty for a few frames, and casting
        // into that flap is exactly what caused the recast spam.
        if (!merc.TryGetComponent<Buffs>(out var buffs) || buffs?.BuffsList == null) return true;

        // After we cast at a merc, the link is applied by our own cast. The
        // buff read can flap empty for a few frames afterwards; that must not
        // be read as "unlinked" or we would recast into our own success.
        if (_lastCastByMerc.TryGetValue(merc.Id, out var lastCast) &&
            Environment.TickCount64 - lastCast < CastLockoutMs)
            return true;

        return HasLinkBuff(merc);
    }

    // True only when the link buff is actually present on the merc. Unlike
    // IsLinked, this never fabricates "linked" from an unreadable buff list,
    // so it can be used to identify the true holder during target selection.
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

    // A merc counts as the active holder if it shows the link buff, or if we
    // just cast at it and the buff has not had time to register yet.
    private bool HoldsActiveLink(Entity merc) =>
        HasLinkBuff(merc) ||
        (_lastCastByMerc.TryGetValue(merc.Id, out var lastCast) &&
         Environment.TickCount64 - lastCast < CastLockoutMs);

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

        var active = new HashSet<uint>(mercs.Select(m => m.Id));
        var now = Environment.TickCount64;
        var stale = _lastCastByMerc.Keys
            .Where(id => !active.Contains(id) || now - _lastCastByMerc[id] >= CastLockoutMs)
            .ToList();
        foreach (var id in stale) _lastCastByMerc.Remove(id);

        // If the merc we are tracking as linked is no longer present at all,
        // the link is free — let a fresh target be chosen.
        if (_linkedMercId != null && !active.Contains(_linkedMercId.Value))
            _linkedMercId = null;
    }

    private Entity FindLinkTarget(IReadOnlyList<Entity> mercs)
    {
        var maxDistance = _plugin.Settings.MaxCastDistance.Value;

        // The link holder must be found across ALL alive mercs, not just the
        // ones in cast range: a linked merc that walked out of range is still
        // linked, and re-targeting another merc would steal the single Flame
        // Link (ping-pong).
        var alive = mercs
            .Where(m => m is { IsValid: true, IsAlive: true })
            .ToList();

        var currentlyLinked = alive.FirstOrDefault(HoldsActiveLink);
        if (currentlyLinked != null)
            _linkedMercId = currentlyLinked.Id;

        // The tracked merc despawned (zone change, gone) — the link is free.
        if (_linkedMercId != null && !alive.Any(m => m.Id == _linkedMercId.Value))
            _linkedMercId = null;

        var inRange = maxDistance <= 0
            ? alive
            : alive.Where(m => m.DistancePlayer <= maxDistance).ToList();

        if (_linkedMercId != null)
        {
            var tracked = alive.FirstOrDefault(m => m.Id == _linkedMercId.Value);
            var trackedInRange = inRange.Any(m => m.Id == _linkedMercId.Value);

            if (tracked == null || (!IsLinked(tracked) && !trackedInRange))
            {
                // The tracked merc is gone entirely, or dropped the link while
                // out of cast range — the link is free, pick a fresh target.
                _linkedMercId = null;
            }
            else
            {
                // Still holds the link (or is in range): only ever re-link that
                // same merc if it dropped it. Never touch the others, or we
                // would ping-pong the single Flame Link target between all
                // hired mercs.
                return trackedInRange && !IsLinked(tracked) ? tracked : null;
            }
        }

        // Nothing is linked yet: establish the link on the nearest merc.
        return inRange
            .OrderBy(m => m.DistancePlayer)
            .FirstOrDefault(m => TryGetScreenPos(m, out _));
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
        // Never leave the link key held down, and always restore the cursor.
        if (LinkKey is { } key && Input.GetKeyState(key))
            Input.KeyUp(key);
        if (_plugin.Settings.RestoreCursor.Value)
            Input.SetCursorPos(_cursorBeforeCast);
        _phase = CastPhase.Idle;
    }

    private void Advance()
    {
        var settings = _plugin.Settings;

        // Re-check the environment on every frame of the cast: if a menu,
        // escape overlay, panel, chat window or death happens while the cursor
        // is parked on a merc and the link key is about to go down, abort
        // instead of pressing the key into an unsafe state.
        if (EnvironmentUnsafe(out var abortReason))
        {
            // Never leave the link key held down, no matter which phase aborts.
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
                // Let the release settle, then make sure it really is up.
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
