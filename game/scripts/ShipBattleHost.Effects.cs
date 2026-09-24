using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

/// <summary>
/// Presentation-only combat effects: projectiles that leave one frame and re-enter the other (never a line across
/// the divider), shield ripples and collapses, explosions, sparks, scorch marks, hazard particles, hull-hit shake and
/// a bounded destruction sequence. Everything ages on the battle presentation clock, so pause freezes it; after a
/// terminal outcome a short presentation-only clock lets the destruction finish (like the station's final fall).
/// </summary>
public partial class ShipBattleHost
{
    private const float ProjectileHeight = 4.2f;
    private const double SourceShare = .42;
    private const double TerminalSeconds = 4;
    private readonly Dictionary<long, Projectile> _projectiles = [];
    private readonly List<Glow> _glows = [];
    private readonly List<FloatingText> _texts = [];
    private readonly Dictionary<ShipBattleView, List<(Vector2 Local, double Tick)>> _shieldHits = [];
    private readonly Dictionary<ShipBattleView, (int Layers, double Collapse, double Regrow)> _shieldState = [];
    private readonly Dictionary<ShipBattleView, List<Decal>> _scorches = [];
    private readonly Dictionary<string, (GpuParticles3D Flames, OmniLight3D Light)> _fireFx = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GpuParticles3D> _ventFx = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GpuParticles3D> _playerSparks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GpuParticles3D> _enemySparks = new(StringComparer.Ordinal);
    private static Texture2D? _softDot, _scorchTexture;
    private double _shakeTick = double.NegativeInfinity;
    private float _shakeAmount;
    private double _terminalSeconds;
    private int _destructionStep;
    private bool _destroyed;

    /// <summary>Effect clock: the battle presentation tick, extended by the bounded post-outcome clock.</summary>
    public double EffectTick => PresentationTick + _terminalSeconds * ShipCombatSession.TicksPerSecond;

    public int ActiveEffectCount => _projectiles.Count + _glows.Count + _texts.Count;

    public double TerminalPresentationSeconds => _terminalSeconds;

    private enum Outcome { None, Absorbed, Hit, Missed }

    private sealed class Projectile
    {
        public required ShipShotObservation Shot { get; init; }
        public required MeshInstance3D SourceHead { get; init; }
        public required MeshInstance3D TargetHead { get; init; }
        public bool Launched { get; set; }
        public bool MissAnnounced { get; set; }
        public Outcome Outcome { get; set; }
        public double ResolvedTick { get; set; }
        public int Damage { get; set; }
    }

    private sealed record Glow(ShipBattleView View, MeshInstance3D Node, double Start, double Duration, Vector2 From, Vector2 Velocity,
        float Size, float Growth, float Length, float Energy, bool Streak);

    private sealed record FloatingText(ShipBattleView View, Vector2 World, string Text, Color Color, double Start, int Size);

    private void AdvanceTerminalClock(double delta)
    {
        if (_session.Phase == ShipBattlePhase.Active) { _terminalSeconds = 0; return; }
        _terminalSeconds = Math.Min(TerminalSeconds, _terminalSeconds + delta);
    }

    private void ResetEffects()
    {
        foreach (var projectile in _projectiles.Values) { projectile.SourceHead.QueueFree(); projectile.TargetHead.QueueFree(); }
        _projectiles.Clear();
        foreach (var glow in _glows) { glow.Node.QueueFree(); }
        _glows.Clear();
        _texts.Clear();
        foreach (var list in _scorches.Values) { foreach (var decal in list) { decal.QueueFree(); } list.Clear(); }
        _shieldHits.Clear();
        _shieldState.Clear();
        _shakeTick = double.NegativeInfinity;
        _terminalSeconds = 0;
        _destructionStep = 0;
        _destroyed = false;
        _playerView.Ship.Visible = _enemyView.Ship.Visible = true;
        _playerView.Ship.Scale = _enemyView.Ship.Scale = Vector3.One;
        GameAudio.StopLoops();
    }

    private void BuildHazardEffects()
    {
        foreach (var room in _definition.Rooms)
        {
            var centre = RoomCentre(room.Id);
            var flames = Particles(_playerView.Overlay, $"Flames_{room.Id}", 30, .8f, new Vector3((float)room.Width * .28f, .05f, (float)room.Depth * .28f),
                new Color("ff8a2a", .9f), new Color("b3260a", .6f), additive: true, speed: .45f, scale: .42f);
            flames.Position = _playerView.Anchor($"hazard_fire_{room.Id}", centre) + new Vector3(0, .35f, 0);
            var light = new OmniLight3D { Name = $"FireLight_{room.Id}", LightColor = new Color("ff5a14"), OmniRange = 1.9f, LightEnergy = 0, Position = flames.Position + new Vector3(0, .45f, 0) };
            _playerView.Overlay.AddChild(light);
            _fireFx[room.Id] = (flames, light);
            var vent = Particles(_playerView.Overlay, $"Vent_{room.Id}", 36, .8f, new Vector3(.9f, .05f, .9f), new Color("e8f4ff", .55f), new Color("9fc4dd", 0), additive: false, speed: .1f, scale: .16f);
            ((ParticleProcessMaterial)vent.ProcessMaterial).RadialAccelMin = -5.5f;
            ((ParticleProcessMaterial)vent.ProcessMaterial).RadialAccelMax = -3.5f;
            vent.Position = _playerView.Anchor($"hazard_breach_{room.Id}", centre) + new Vector3(0, .3f, 0);
            _ventFx[room.Id] = vent;
        }
        foreach (var system in ShipBattleDefinition.PlayerSystemIds)
        {
            var sparks = Particles(_playerView.Overlay, $"Sparks_{system}", 14, .45f, new Vector3(.25f, .05f, .25f), new Color("fff1b0"), new Color("ff9b3a"), additive: true, speed: 1.6f, scale: .08f);
            sparks.Position = PlayerSystemPosition(system) + new Vector3(0, .7f, 0);
            _playerSparks[system] = sparks;
        }
    }

    private void BuildEnemyEffects()
    {
        foreach (var system in ShipBattleDefinition.EnemySystemIds)
        {
            var sparks = Particles(_enemyView.Overlay, $"Sparks_{system}", 18, .45f, new Vector3(.35f, .05f, .35f), new Color("fff1b0"), new Color("ff9b3a"), additive: true, speed: 1.8f, scale: .09f);
            sparks.Position = EnemySystemPosition(system) + new Vector3(0, .9f, 0);
            _enemySparks[system] = sparks;
        }
    }

    private static GpuParticles3D Particles(Node3D parent, string name, int amount, float lifetime, Vector3 extents, Color hot, Color cool, bool additive, float speed, float scale)
    {
        _softDot ??= SoftDot();
        var gradient = new Gradient();
        gradient.SetColor(0, hot);
        gradient.SetColor(1, cool with { A = 0 });
        gradient.AddPoint(.45f, cool with { A = cool.A * .8f });
        var process = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = extents,
            Direction = new Vector3(0, 1, 0), Spread = 180, InitialVelocityMin = speed * .4f, InitialVelocityMax = speed,
            Gravity = Vector3.Zero, DampingMin = .5f, DampingMax = 1.5f, ScaleMin = scale * .6f, ScaleMax = scale * 1.4f,
            ColorRamp = new GradientTexture1D { Gradient = gradient },
        };
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix, VertexColorUseAsAlbedo = true,
            AlbedoTexture = _softDot, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, NoDepthTest = false,
        };
        var particles = new GpuParticles3D
        {
            Name = name, Amount = amount, Lifetime = lifetime, Emitting = false, ProcessMaterial = process,
            DrawPass1 = new QuadMesh { Size = Vector2.One, Material = material }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-3, -2, -3), new Vector3(6, 4, 6)),
        };
        parent.AddChild(particles);
        return particles;
    }

    private static GradientTexture2D SoftDot()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, Colors.White);
        gradient.SetColor(1, new Color(1, 1, 1, 0));
        return new GradientTexture2D { Gradient = gradient, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(.5f, 0), Width = 64, Height = 64 };
    }

    private static GradientTexture2D ScorchTexture()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(.02f, .015f, .01f, .92f));
        gradient.AddPoint(.55f, new Color(.08f, .05f, .03f, .55f));
        gradient.SetColor(2, new Color(0, 0, 0, 0));
        return new GradientTexture2D { Gradient = gradient, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(.5f, 0), Width = 128, Height = 128 };
    }

    private void ConsumeEvents(ShipBattleObservation observation)
    {
        foreach (var gameEvent in _session.EventsSince(_eventCursor).ToArray())
        {
            _eventCursor = gameEvent.Sequence;
            if (gameEvent.Attempt != _session.Attempt) { continue; }
            var playerSide = gameEvent.Subject == "player";
            switch (gameEvent.Kind)
            {
                case "shot_missed" or "shield_absorbed" or "hull_hit":
                    var parts = gameEvent.Detail.Split(':');
                    if (parts.Length >= 3 && long.TryParse(parts[2], System.Globalization.CultureInfo.InvariantCulture, out var shotId) && _projectiles.TryGetValue(shotId, out var projectile))
                    {
                        projectile.Outcome = gameEvent.Kind switch { "shot_missed" => Outcome.Missed, "shield_absorbed" => Outcome.Absorbed, _ => Outcome.Hit };
                        projectile.ResolvedTick = gameEvent.Tick;
                        projectile.Damage = parts.Length >= 4 && int.TryParse(parts[3], System.Globalization.CultureInfo.InvariantCulture, out var damage) ? damage : 0;
                    }
                    break;
                case "fire_started" or "fire_spread":
                    GameAudio.Play("ship.fire_ignite", PanFor(_playerView));
                    GameAudio.Play("ship.alarm");
                    break;
                case "breach_opened":
                    GameAudio.Play("ship.breach", PanFor(_playerView));
                    GameAudio.Play("ship.alarm");
                    break;
                case "crew_hit":
                    GameAudio.Play("ship.crew_hurt", PanFor(_playerView));
                    break;
                case "crew_downed":
                    GameAudio.Play("ship.crew_down", PanFor(_playerView));
                    break;
                case "system_repaired":
                    GameAudio.Play("ship.repair_done", PanFor(_playerView));
                    break;
                case "enemy_repaired":
                    GameAudio.Play("ship.repair_done", PanFor(_enemyView), -6);
                    break;
                case "fire_extinguished" or "breach_sealed":
                    GameAudio.Play("ship.hazard_cleared", PanFor(_playerView));
                    break;
                case "vent_opened" or "vent_closed":
                    GameAudio.Play("ship.vent", PanFor(_playerView));
                    break;
                case "door_set" or "door_traversal":
                    GameAudio.Play("ship.door", PanFor(_playerView), gameEvent.Kind == "door_traversal" ? -8 : 0);
                    break;
                case "shield_restored":
                    GameAudio.Play("ship.shield_up", PanFor(playerSide ? _playerView : _enemyView), playerSide ? 0 : -6);
                    break;
                case "weapon_unpowered" when !playerSide:
                    GameAudio.Play("ship.power_down", PanFor(_enemyView));
                    SpawnText(_enemyView, new Vector2(EnemySystemPosition("weapons").X, EnemySystemPosition("weapons").Z), "WEAPON OFFLINE", TacticalUi.Amber, 12);
                    break;
                case "paused":
                    GameAudio.Play("ui.pause");
                    break;
                case "resumed":
                    GameAudio.Play("ui.resume");
                    break;
                case "victory" or "defeat":
                    GameAudio.Play(gameEvent.Kind == "victory" ? "ship.victory" : "ship.defeat");
                    break;
            }
        }
    }

    private float PanFor(ShipBattleView view) => view == _playerView ? -.55f : .55f;

    private void UpdateEffects(ShipBattleObservation observation, double tick)
    {
        var clock = EffectTick;
        var frozen = observation.Paused || observation.Phase != ShipBattlePhase.Active;
        foreach (var shot in observation.Shots.Where(shot => !_projectiles.ContainsKey(shot.Id))) { _projectiles[shot.Id] = CreateProjectile(shot); }
        foreach (var projectile in _projectiles.Values.ToArray()) { UpdateProjectile(projectile, tick, clock, observation); }
        UpdateShield(_playerView, observation.Player, clock);
        UpdateShield(_enemyView, observation.Enemy, clock);
        for (var index = _glows.Count - 1; index >= 0; index--)
        {
            var glow = _glows[index];
            var age = (clock - glow.Start) / glow.Duration;
            if (age >= 1) { glow.Node.QueueFree(); _glows.RemoveAt(index); continue; }
            if (age < 0) { glow.Node.Visible = false; continue; }
            glow.Node.Visible = true;
            var seconds = (float)((clock - glow.Start) / ShipCombatSession.TicksPerSecond);
            // Sparks decelerate; flashes swell and fade.
            var travel = glow.Velocity * seconds * (1 - (float)age * .5f);
            var position = glow.From + travel;
            glow.Node.Position = new Vector3(position.X, ProjectileHeight - .2f, position.Y);
            var size = glow.Size * (1 + glow.Growth * (float)age);
            glow.Node.Scale = new Vector3(size, 1, glow.Streak ? glow.Length * (1 - (float)age * .6f) : size);
            if (glow.Streak && glow.Velocity.LengthSquared() > 0) { glow.Node.Rotation = new Vector3(0, Mathf.Atan2(glow.Velocity.X, glow.Velocity.Y), 0); }
            ((ShaderMaterial)glow.Node.MaterialOverride).SetShaderParameter("fade", 1 - (float)age);
        }
        _texts.RemoveAll(text => clock - text.Start > 45);
        UpdateHazards(observation, clock, frozen);
        // Hull-hit shake on the player view only, decaying over 12 ticks.
        var shakeAge = (clock - _shakeTick) / 12;
        _playerView.SetShake(shakeAge is >= 0 and < 1
            ? new Vector2(Mathf.Sin((float)clock * 2.7f), Mathf.Cos((float)clock * 3.3f)) * _shakeAmount * (float)(1 - shakeAge)
            : Vector2.Zero);
        UpdateDestruction(observation);
    }

    private static MeshInstance3D Head(ShipBattleView view, ShipShotObservation shot)
    {
        var player = shot.Source == "player";
        var missile = shot.Kind == ShipWeaponKind.Missile;
        var color = missile ? new Color("ffc46b") : player ? new Color("8ff3ff") : new Color("ff5a3a");
        var head = ShipBattleView.Glow(color, missile ? 3.2f : 3.6f, 1);
        head.Name = $"Shot_{shot.Id}";
        head.Visible = false;
        head.Scale = missile ? new Vector3(.34f, 1, .95f) : new Vector3(.22f, 1, 1.05f);
        view.Overlay.AddChild(head);
        if (missile)
        {
            var body = new MeshInstance3D { Mesh = new CapsuleMesh { Radius = .07f, Height = .36f }, Rotation = new Vector3(Mathf.Pi / 2, 0, 0), Position = new Vector3(0, .1f, .15f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("3a4148"), Metallic = .6f, Roughness = .4f } };
            body.Scale = new Vector3(1 / .34f, 1, 1 / .95f);
            head.AddChild(body);
        }
        return head;
    }

    private Projectile CreateProjectile(ShipShotObservation shot)
    {
        var source = shot.Source == "player" ? _playerView : _enemyView;
        var target = shot.Source == "player" ? _enemyView : _playerView;
        return new Projectile { Shot = shot, SourceHead = Head(source, shot), TargetHead = Head(target, shot) };
    }

    private (Vector2 Muzzle, Vector2 Exit, Vector2 Entry, Vector2 Contact, Vector2 Target) Path(ShipShotObservation shot)
    {
        var player = shot.Source == "player";
        var source = player ? _playerView : _enemyView;
        var target = player ? _enemyView : _playerView;
        var muzzle3 = source.Anchor("muzzle", new Vector3(0, ShipBattleView.FloorY, -5.5f));
        var muzzle = new Vector2(muzzle3.X, muzzle3.Z);
        var muzzleScreen = source.ToScreen(muzzle3);
        // The divider-side frame edge: right edge of the player view, left edge of the enemy view.
        var exitScreen = new Vector2(player ? source.Container.Size.X + 60 : -60, muzzleScreen.Y - 150);
        var target3 = player ? EnemySystemPosition(shot.TargetSystem) : RoomCentre(shot.TargetSystem);
        var aim = new Vector2(target3.X, target3.Z) + Scatter(shot.Id, .35f);
        var targetScreen = target.ToScreen(new Vector3(aim.X, ShipBattleView.FloorY, aim.Y));
        var entryScreen = new Vector2(player ? -60 : target.Container.Size.X + 60, targetScreen.Y - 110 + Scatter(shot.Id * 7, 60).Y);
        var exit = source.FloorAt(exitScreen);
        var entry = target.FloorAt(entryScreen);
        var contact = target.BubbleEntry(entry, aim);
        return (muzzle, exit, entry, contact, aim);
    }

    private static Vector2 Scatter(long seed, float radius)
    {
        var a = Mathf.Sin(seed * 12.9898f) * 43758.5453f;
        var b = Mathf.Sin(seed * 78.233f) * 12345.6789f;
        return new Vector2(a - Mathf.Floor(a) - .5f, b - Mathf.Floor(b) - .5f) * 2 * radius;
    }

    private void UpdateProjectile(Projectile projectile, double tick, double clock, ShipBattleObservation observation)
    {
        var shot = projectile.Shot;
        var source = shot.Source == "player" ? _playerView : _enemyView;
        var target = shot.Source == "player" ? _enemyView : _playerView;
        var (muzzle, exit, entry, contact, aim) = Path(shot);
        if (!projectile.Launched && tick >= shot.LaunchTick && projectile.Outcome == Outcome.None)
        {
            projectile.Launched = true;
            SpawnGlow(source, muzzle, Vector2.Zero, shot.Source == "player" ? new Color("bff7ff") : new Color("ffb08a"), .42f, 1f, 5, 2.4f);
            GameAudio.Play(shot.Kind == ShipWeaponKind.Missile ? "ship.missile_launch" : "ship.laser_fire", PanFor(source), shot.Source == "player" ? 0 : -3);
        }
        if (projectile.Outcome == Outcome.None)
        {
            if (observation.Phase != ShipBattlePhase.Active || tick > shot.ImpactTick + 2) { Remove(projectile); return; }
            var progress = (tick - shot.LaunchTick) / Math.Max(1, shot.ImpactTick - shot.LaunchTick);
            projectile.SourceHead.Visible = progress is >= 0 and < SourceShare;
            projectile.TargetHead.Visible = progress >= SourceShare;
            if (progress < 0) { return; }
            if (progress < SourceShare) { Place(projectile.SourceHead, muzzle, exit, progress / SourceShare); }
            else { Place(projectile.TargetHead, entry, contact, (progress - SourceShare) / (1 - SourceShare)); }
            return;
        }
        projectile.SourceHead.Visible = false;
        // After resolution the flight finishes on the effect clock, which keeps running briefly after a terminal outcome.
        var since = clock - projectile.ResolvedTick;
        switch (projectile.Outcome)
        {
            case Outcome.Absorbed:
                AddShieldHit(target, contact);
                SpawnGlow(target, contact, Vector2.Zero, new Color("bfe9ff"), .7f, 1.4f, 8, 3);
                GameAudio.Play("ship.shield_hit", PanFor(target), shot.Source == "player" ? -3 : 0);
                Remove(projectile);
                return;
            case Outcome.Missed:
                if (!projectile.MissAnnounced)
                {
                    projectile.MissAnnounced = true;
                    SpawnText(target, contact, "MISS", Colors.White, 14);
                    GameAudio.Play("ship.miss", PanFor(target));
                }
                var direction = (contact - entry).Normalized();
                projectile.TargetHead.Visible = since < 16;
                Place(projectile.TargetHead, contact, contact + direction * 6, since / 16);
                SetFade(projectile.TargetHead, 1 - (float)(since / 16));
                if (since >= 16) { Remove(projectile); }
                return;
            case Outcome.Hit:
                const double Final = 5;
                if (since < Final) { projectile.TargetHead.Visible = true; Place(projectile.TargetHead, contact, aim, since / Final); return; }
                Explode(target, aim, projectile.Damage, shot.Kind == ShipWeaponKind.Missile, shot.Id);
                Remove(projectile);
                return;
        }
    }

    private static void Place(MeshInstance3D head, Vector2 from, Vector2 to, double t)
    {
        var position = from.Lerp(to, (float)Math.Clamp(t, 0, 1));
        head.Position = new Vector3(position.X, ProjectileHeight, position.Y);
        var direction = to - from;
        if (direction.LengthSquared() > 1e-6f) { head.Rotation = new Vector3(0, Mathf.Atan2(direction.X, direction.Y), 0); }
    }

    private static void SetFade(MeshInstance3D node, float fade) => ((ShaderMaterial)node.MaterialOverride).SetShaderParameter("fade", Math.Clamp(fade, 0, 1));

    private void Remove(Projectile projectile)
    {
        projectile.SourceHead.QueueFree();
        projectile.TargetHead.QueueFree();
        _projectiles.Remove(projectile.Shot.Id);
    }

    private void Explode(ShipBattleView view, Vector2 at, int damage, bool heavy, long seed)
    {
        var clock = EffectTick;
        SpawnGlow(view, at, Vector2.Zero, new Color("ff6a1f"), heavy ? 1.6f : 1.15f, 1.1f, heavy ? 14 : 11, 2f);
        SpawnGlow(view, at, Vector2.Zero, new Color("ffd49a"), heavy ? .7f : .45f, .6f, 5, 2.4f);
        for (var index = 0; index < (heavy ? 10 : 7); index++)
        {
            var direction = Scatter(seed * 31 + index, 1).Normalized();
            if (direction == Vector2.Zero) { direction = Vector2.Up; }
            _glows.Add(NewGlow(view, at, direction * (2.2f + index % 3), new Color("ffd08a"), .07f, 0, 14, 2.6f, streak: true, length: .5f, start: clock));
        }
        AddScorch(view, at, heavy ? 1.3f : 1);
        if (view == _playerView) { _shakeTick = clock; _shakeAmount = Math.Min(.28f, .09f * Math.Max(1, damage)); }
        if (damage > 0) { SpawnText(view, at + new Vector2(0, -.4f), $"-{damage}", TacticalUi.Damaged, 16); }
        GameAudio.Play(heavy ? "ship.explosion_heavy" : "ship.hull_hit", PanFor(view), view == _enemyView ? -3 : 0);
    }

    private void SpawnGlow(ShipBattleView view, Vector2 at, Vector2 velocity, Color color, float size, float growth, double duration, float energy) =>
        _glows.Add(NewGlow(view, at, velocity, color, size, growth, duration, energy, streak: false, length: size, start: EffectTick));

    private static Glow NewGlow(ShipBattleView view, Vector2 at, Vector2 velocity, Color color, float size, float growth, double duration, float energy, bool streak, float length, double start)
    {
        var node = ShipBattleView.Glow(color, energy, streak ? 1 : 0);
        node.Position = new Vector3(at.X, ProjectileHeight - .2f, at.Y);
        view.Overlay.AddChild(node);
        return new Glow(view, node, start, duration, at, velocity, size, growth, length, energy, streak);
    }

    private void SpawnText(ShipBattleView view, Vector2 world, string text, Color color, int size) =>
        _texts.Add(new FloatingText(view, world, text, color, EffectTick, size));

    private IEnumerable<Action<ShipOverlay>> FloatingTexts(ShipBattleView view, double tick)
    {
        var clock = EffectTick;
        foreach (var text in _texts.Where(item => item.View == view))
        {
            // Pausing drops the fractional tick; keep text created in that fraction visible.
            var age = Math.Max(0, (float)((clock - text.Start) / 45));
            if (age > 1) { continue; }
            var screen = view.ToScreen(new Vector3(text.World.X, ShipBattleView.FloorY, text.World.Y)) + new Vector2(0, -28 * age);
            var color = new Color(text.Color, 1 - age * age);
            yield return overlay => overlay.Text(screen, text.Text, text.Size, color, bold: true);
        }
    }

    private void AddScorch(ShipBattleView view, Vector2 at, float scale)
    {
        _scorchTexture ??= ScorchTexture();
        if (!_scorches.TryGetValue(view, out var list)) { _scorches[view] = list = []; }
        var decal = new Decal { TextureAlbedo = _scorchTexture, Size = new Vector3(1.1f * scale, 3, 1.1f * scale), Position = new Vector3(at.X, ShipBattleView.FloorY + .5f, at.Y),
            AlbedoMix = .85f, UpperFade = .1f, LowerFade = .1f };
        view.Overlay.AddChild(decal);
        list.Add(decal);
        if (list.Count > 8) { list[0].QueueFree(); list.RemoveAt(0); }
    }

    private void AddShieldHit(ShipBattleView view, Vector2 world)
    {
        if (!_shieldHits.TryGetValue(view, out var list)) { _shieldHits[view] = list = []; }
        list.Add((view.BubbleLocal(world), EffectTick));
        if (list.Count > 4) { list.RemoveAt(0); }
    }

    private void UpdateShield(ShipBattleView view, ShipSideObservation side, double clock)
    {
        (int Layers, double Collapse, double Regrow) state = _shieldState.TryGetValue(view, out var known) ? known : (side.ShieldLayers, double.NegativeInfinity, double.NegativeInfinity);
        if (side.ShieldLayers == 0 && state.Layers > 0) { state.Collapse = clock; GameAudio.Play("ship.shield_down", PanFor(view)); }
        if (side.ShieldLayers > state.Layers) { state.Regrow = clock; }
        state.Layers = side.ShieldLayers;
        _shieldState[view] = state;
        var hits = _shieldHits.TryGetValue(view, out var list)
            ? list.Select(hit => new Vector4(hit.Local.X, hit.Local.Y, (float)((clock - hit.Tick) / ShipCombatSession.TicksPerSecond), 1))
                .Where(hit => hit.Z is >= 0 and < .9f).ToList()
            : [];
        var collapse = (float)Math.Max(0, 1 - (clock - state.Collapse) / 14);
        var regrow = (float)Math.Max(0, 1 - (clock - state.Regrow) / 15);
        view.SetShield(_destroyed && view.Ship.Visible == false ? 0 : side.ShieldLayers, collapse, regrow, hits);
    }

    private void UpdateHazards(ShipBattleObservation observation, double clock, bool frozen)
    {
        var speed = frozen ? 0f : 1f;
        foreach (var room in observation.Rooms)
        {
            var (flames, light) = _fireFx[room.Id];
            flames.Emitting = room.FireSeverity > 0;
            flames.AmountRatio = Math.Clamp(.4f + room.FireSeverity * .2f, 0, 1);
            flames.SpeedScale = speed;
            var flicker = .75f + .25f * Mathf.Sin((float)clock * .9f + room.Id.Length) * Mathf.Sin((float)clock * .37f);
            light.LightEnergy = room.FireSeverity > 0 ? (.3f + room.FireSeverity * .22f) * flicker : 0;
            var vent = _ventFx[room.Id];
            vent.Emitting = room.BreachSeverity > 0;
            vent.SpeedScale = speed;
        }
        foreach (var system in observation.Player.Systems)
        {
            _playerSparks[system.Id].Emitting = system.Damage > 0;
            _playerSparks[system.Id].SpeedScale = speed;
        }
        foreach (var system in observation.Enemy.Systems)
        {
            _enemySparks[system.Id].Emitting = system.Damage > 0 && !_destroyed;
            _enemySparks[system.Id].SpeedScale = speed;
        }
        var anyFire = observation.Rooms.Any(room => room.FireSeverity > 0);
        var anyLeak = observation.Rooms.Any(room => room.BreachSeverity > 0) || observation.Doors.Any(door => door.Exterior && door.CommandedOpen);
        var lowOxygen = observation.Rooms.Any(room => room.OxygenPermille < 250);
        GameAudio.SetLoop("ship.fire_loop", anyFire, frozen);
        GameAudio.SetLoop("ship.leak_loop", anyLeak, frozen);
        GameAudio.SetLoop("ship.oxygen_alarm", lowOxygen && observation.Phase == ShipBattlePhase.Active, frozen);
        GameAudio.SetLoop("ship.ambience", true, false);
    }

    /// <summary>Chained explosions across the losing ship, then it breaks up. Driven by the bounded post-outcome clock.</summary>
    private void UpdateDestruction(ShipBattleObservation observation)
    {
        if (observation.Phase == ShipBattlePhase.Active) { return; }
        var view = observation.Phase == ShipBattlePhase.Victory ? _enemyView : _playerView;
        var bounds = view.Bounds;
        for (; _destructionStep < Math.Min(12, (int)(_terminalSeconds / .16)); _destructionStep++)
        {
            var point = bounds.GetCenter() + Scatter(900 + _destructionStep, 1) * bounds.Size * .38f;
            Explode(view, point, 0, _destructionStep % 4 == 3, 500 + _destructionStep);
        }
        if (!_destroyed && _terminalSeconds >= 2.1)
        {
            _destroyed = true;
            SpawnGlow(view, bounds.GetCenter(), Vector2.Zero, new Color("fff0d0"), 4.5f, 1.8f, 24, 4.5f);
            for (var index = 0; index < 18; index++)
            {
                var direction = Scatter(700 + index, 1).Normalized();
                _glows.Add(NewGlow(view, bounds.GetCenter(), direction * (3 + index % 4), new Color("ffc27a"), .1f, 0, 30, 2.8f, streak: true, length: .8f, start: EffectTick));
            }
            view.Ship.Visible = false;
            GameAudio.Play("ship.explosion_final", PanFor(view));
        }
    }
}
