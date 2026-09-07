using Godot;

namespace SpaceAdventure.Game;

// Sampling is driven by presentation time, never by the AnimationPlayer's
// process callback. Exact paused steps and ordinary rendered frames use this
// same path, including the short transition between clips.
public sealed class SkeletalPosePlayer
{
    private readonly AnimationPlayer _player;
    private readonly Skeleton3D _skeleton;
    private readonly BoneAttachment3D[] _attachments;
    private Transform3D[]? _blendFrom;
    private StringName? _clip;
    private long _cycle;
    private double _blendStart;
    private readonly Transform3D[] _samplePose;
    private bool _hasSamplePose;

    public SkeletalPosePlayer(AnimationPlayer player, Skeleton3D skeleton)
    {
        _player = player;
        _skeleton = skeleton;
        _attachments = skeleton.GetChildren().OfType<BoneAttachment3D>().ToArray();
        _samplePose = new Transform3D[skeleton.GetBoneCount()];
        _player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
    }

    public double ClipTime { get; private set; }

    public string ClipName => _clip?.ToString() ?? string.Empty;

    public void Sample(StringName clip, double clipSeconds, double clockSeconds, long cycle = 0, double blendSeconds = 0.12)
    {
        // Animation compression may omit constant spine/arm tracks. Restore the
        // previous authored sample before seeking so procedural aim and IK never
        // become the next sample's base pose or accumulate across firing cycles.
        if (_hasSamplePose)
        {
            for (var index = 0; index < _samplePose.Length; index++) { _skeleton.SetBonePose(index, _samplePose[index]); }
        }
        if (_clip != clip || _cycle != cycle)
        {
            _blendFrom = _clip is null ? null : ReadPose();
            _blendStart = clockSeconds;
            _clip = clip;
            _cycle = cycle;
            _player.Play(clip, customBlend: 0);
        }

        var animation = _player.GetAnimation(clip);
        ClipTime = animation.LoopMode == Animation.LoopModeEnum.Linear
            ? Math.Max(0, clipSeconds) % animation.Length
            : Math.Clamp(clipSeconds, 0, animation.Length);
        _player.Seek(ClipTime, update: true);
        for (var index = 0; index < _samplePose.Length; index++) { _samplePose[index] = _skeleton.GetBonePose(index); }
        _hasSamplePose = true;
        var blend = blendSeconds <= 0 ? 1 : (float)Math.Clamp((clockSeconds - _blendStart) / blendSeconds, 0, 1);
        if (_blendFrom is not null && blend < 1)
        {
            for (var index = 0; index < _blendFrom.Length; index++)
            {
                _skeleton.SetBonePose(index, _blendFrom[index].InterpolateWith(_skeleton.GetBonePose(index), blend));
            }
        }
        else
        {
            _blendFrom = null;
        }

        foreach (var attachment in _attachments) { attachment.OnSkeletonUpdate(); }
    }

    private Transform3D[] ReadPose() => Enumerable.Range(0, _skeleton.GetBoneCount())
        .Select(_skeleton.GetBonePose).ToArray();
}
