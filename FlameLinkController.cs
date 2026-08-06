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
        Firing,
    }

    private readonly MercScanner _plugin;
    private readonly Stopwatch _phaseClock = Stopwatch.StartNew();
    private readonly Stopwatch _cooldownClock = Stopwatch.StartNew();

    private CastPhase _phase = CastPhase.Idle;
    private System.Numerics.Vector2 _cursorBeforeCast;

    public string BlockedReason { get; private set; } = "Disabled";
    public string LastTarget { get; private set; } = "";
    public int CastCount { get; private set; }
    public bool Busy => _phase != CastPhase.Idle;

    public FlameLinkController(MercScanner plugin)
    {
        _plugin = plugin;
    }

    public void Update(IReadOnlyList<Entity> mercs)
    {
        if (_phase != CastPhase.Idle)
        {
            Advance();
            return;
        }

        if (!ReadyToCast(out var blockReason))
        {
            BlockedReason = blockReason;
            return;
        }

        var target = FindUnlinkedMerc(mercs);
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

    public int CastGapMs
    {
        get
        {
            var settings = _plugin.Settings;
            if (FindLinkSkill() is { } skill && skill.CastTime.TotalMilliseconds > 0)
                return (int)skill.CastTime.TotalMilliseconds + settings.CastMarginMs.Value;
            return settings.CastGapMs.Value;
        }
    }

    private bool ReadyToCast(out string reason)
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

        if (_cooldownClock.ElapsedMilliseconds < CastGapMs)
        {
            reason = "Pacing casts";
            return false;
        }

        var gc = _plugin.GameController;
        if (MenuWindow.IsOpened)
        {
            reason = "Settings menu open";
            return false;
        }

        if (!gc.IsForeGroundCache)
        {
            reason = "Game not focused";
            return false;
        }

        if (gc.Game?.IsEscapeState == true)
        {
            reason = "Escape menu open";
            return false;
        }

        if (gc.Player is not { IsValid: true } player || !player.TryGetComponent<Life>(out var life) || life == null || life.CurHP <= 0)
        {
            reason = "Player dead";
            return false;
        }

        var area = gc.Area?.CurrentArea;
        if (settings.DontCastInTown.Value && area is { IsTown: true } or { IsHideout: true })
        {
            reason = "In town or hideout";
            return false;
        }

        if (gc.IngameState.FocusedInputElement != null)
        {
            reason = "Text input focused";
            return false;
        }

        if (gc.IngameState.IngameUi.ChatTitlePanel?.IsVisible == true)
        {
            reason = "Chat open";
            return false;
        }

        if (settings.DontCastWithPanelsOpen.Value && AnyPanelOpen)
        {
            reason = "A panel is open";
            return false;
        }

        if (settings.RequireSkillReady.Value && FindLinkSkill() is not { CanBeUsed: true })
        {
            reason = "Link skill not ready";
            return false;
        }

        reason = "Ready";
        return true;
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

    private Entity FindUnlinkedMerc(IReadOnlyList<Entity> mercs)
    {
        var maxDistance = _plugin.Settings.MaxCastDistance.Value;
        return mercs
            .Where(m => m is { IsValid: true, IsAlive: true })
            .Where(m => !IsLinked(m))
            .Where(m => maxDistance <= 0 || m.DistancePlayer <= maxDistance)
            .Where(m => TryGetScreenPos(m, out _))
            .OrderBy(m => m.DistancePlayer)
            .FirstOrDefault();
    }

    private void StartCast(Entity target)
    {
        if (!TryGetScreenPos(target, out var screenPos)) return;

        _cursorBeforeCast = Input.MousePositionNum;
        LastTarget = target.RenderName ?? "Mercenary";
        Input.SetCursorPos(WindowTopLeft() + screenPos);
        _phase = CastPhase.Aiming;
        _phaseClock.Restart();
    }

    private void Advance()
    {
        var settings = _plugin.Settings;

        if (MenuWindow.IsOpened)
        {
            if (_phase == CastPhase.Aiming && settings.RestoreCursor.Value)
                Input.SetCursorPos(_cursorBeforeCast);

            _phase = CastPhase.Idle;
            BlockedReason = "Settings menu open";
            return;
        }

        switch (_phase)
        {
            case CastPhase.Aiming:
                if (_phaseClock.ElapsedMilliseconds < settings.CursorSettleMs.Value) return;

                if (settings.FlameLinkKey.Value?.Key is { } key)
                {
                    Input.KeyPressRelease(key);
                    CastCount++;
                    _cooldownClock.Restart();
                }

                _phase = CastPhase.Firing;
                _phaseClock.Restart();
                return;

            case CastPhase.Firing:
                if (_phaseClock.ElapsedMilliseconds < settings.CursorRestoreMs.Value) return;

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
