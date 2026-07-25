using Godot;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SpaceAdventure.Game;

public partial class AssetGalleryReview : Node3D
{
    private const string DefaultVanguardAnimation =
        "anim.humanoid.idle_holstered";
    private const int RenderSettlingFrames = 8;
    private const double AnimationPositionToleranceSeconds = 0.001;
    private const int CaptureFailureExitCode = 1;

    private string? _capturePath;
    private string _animationContract = DefaultVanguardAnimation;
    private double _requestedAnimationPositionSeconds = 4.0;
    private AnimationPlayer? _animationPlayer;
    private int _settledFrames;
    private bool _captureTerminating;

    public override void _Ready()
    {
        var arguments = OS.GetCmdlineUserArgs();
        _capturePath = arguments
            .LastOrDefault(argument => argument.StartsWith(
                "--gallery-output=",
                StringComparison.Ordinal))?["--gallery-output=".Length..];

        try
        {
            var cameraName = ParseArguments(arguments);
            var camera = SelectCamera(cameraName);

            if (_capturePath is not null)
            {
                PrepareCapturePath();
            }

            ConfigureVanguardAnimation();
            GD.Print(
                $"ASSET_GALLERY_CAMERA={cameraName}|"
                + $"distance={FormatInvariant(camera.GetMeta("distance_metres").AsDouble(), "F3")}m|"
                + $"fov={FormatInvariant(camera.Fov, "F3")}deg");
            SetProcess(_capturePath is not null);
        }
        catch (CaptureFailureException exception)
        {
            HandleInitializationFailure(exception.ErrorCode, exception.Message);
        }
        catch (Exception exception)
        {
            HandleInitializationFailure(
                "capture_initialization_failed",
                exception.Message);
        }
    }

    private string ParseArguments(string[] arguments)
    {
        var cameraName = "ReviewCamera14_5m";

        foreach (var argument in arguments)
        {
            if (argument.StartsWith("--gallery-camera=", StringComparison.Ordinal))
            {
                cameraName = argument["--gallery-camera=".Length..] switch
                {
                    "7.5" => "ReviewCamera7_5m",
                    "14.5" => "ReviewCamera14_5m",
                    "20" => "ReviewCamera20m",
                    var value => throw new CaptureFailureException(
                        "invalid_camera_distance",
                        $"Unknown gallery camera distance '{value}'.")
                };
            }
            else if (argument.StartsWith(
                "--gallery-animation-contract=",
                StringComparison.Ordinal))
            {
                _animationContract =
                    argument["--gallery-animation-contract=".Length..];
            }
            else if (argument.StartsWith(
                "--gallery-animation-position=",
                StringComparison.Ordinal))
            {
                var value = argument["--gallery-animation-position=".Length..];
                if (!double.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out _requestedAnimationPositionSeconds)
                    || !double.IsFinite(_requestedAnimationPositionSeconds))
                {
                    throw new CaptureFailureException(
                        "invalid_animation_position",
                        $"Invalid gallery animation position '{value}'.");
                }
            }
        }

        return cameraName;
    }

    private Camera3D SelectCamera(string cameraName)
    {
        Camera3D? selectedCamera = null;

        foreach (var child in GetNode("ReviewCameras").GetChildren())
        {
            if (child is not Camera3D camera)
            {
                continue;
            }

            camera.Current = camera.Name == cameraName;
            if (camera.Current)
            {
                selectedCamera = camera;
            }
        }

        return selectedCamera
            ?? throw new CaptureFailureException(
                "camera_unavailable",
                $"Gallery camera '{cameraName}' is unavailable.");
    }

    private void PrepareCapturePath()
    {
        if (string.Equals(
                DisplayServer.GetName(),
                "headless",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new CaptureFailureException(
                "rendering_unavailable",
                "Asset-gallery screenshots require a graphical display server; "
                + "headless mode has no capture render texture.");
        }

        if (string.IsNullOrWhiteSpace(_capturePath))
        {
            throw new CaptureFailureException(
                "invalid_output_path",
                "The gallery output path is empty.");
        }

        try
        {
            if (!Path.IsPathFullyQualified(_capturePath))
            {
                throw new CaptureFailureException(
                    "invalid_output_path",
                    "The gallery output path must be absolute.");
            }

            var fullPath = Path.GetFullPath(_capturePath);
            if (!string.Equals(
                    Path.GetExtension(fullPath),
                    ".png",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new CaptureFailureException(
                    "invalid_output_path",
                    "The gallery output path must use the .png extension.");
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory)
                || !Directory.Exists(directory))
            {
                throw new CaptureFailureException(
                    "invalid_output_path",
                    $"The gallery output directory does not exist: '{directory}'.");
            }

            _capturePath = fullPath;
        }
        catch (CaptureFailureException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or IOException
            or NotSupportedException)
        {
            throw new CaptureFailureException(
                "invalid_output_path",
                $"The gallery output path is invalid: {exception.Message}");
        }
    }

    private void ConfigureVanguardAnimation()
    {
        var vanguard = GetNode(
            "Slots/Slot01_Vanguard/ProductionPresentation/Vanguard");
        var player = vanguard.FindChild(
            "AnimationPlayer",
            recursive: true,
            owned: false) as AnimationPlayer;
        if (player is null)
        {
            throw new CaptureFailureException(
                "animation_player_unavailable",
                "The published Vanguard scene has no AnimationPlayer.");
        }

        var importedAnimation = _animationContract.Replace('.', '_');
        var animationName = new StringName(importedAnimation);
        if (!player.HasAnimation(animationName))
        {
            var available = string.Join(
                ", ",
                player.GetAnimationList().Select(name => name.ToString()));
            throw new CaptureFailureException(
                "animation_unavailable",
                $"The published Vanguard scene is missing '{importedAnimation}' "
                + $"for contract '{_animationContract}'. "
                + $"Available animations: {available}.");
        }

        var animation = player.GetAnimation(animationName);
        if (animation.Length <= 1.0)
        {
            throw new CaptureFailureException(
                "animation_not_multiframe",
                $"The Vanguard retarget proof is not multi-frame: "
                + $"{animation.Length:F3} seconds.");
        }
        if (_requestedAnimationPositionSeconds < 0.0
            || _requestedAnimationPositionSeconds > animation.Length)
        {
            throw new CaptureFailureException(
                "animation_position_out_of_range",
                $"Requested {FormatInvariant(_requestedAnimationPositionSeconds, "F6")}s for "
                + $"'{_animationContract}', whose duration is "
                + $"{FormatInvariant(animation.Length, "F6")}s.");
        }

        player.Play(animationName);
        player.Seek(_requestedAnimationPositionSeconds, update: true);
        player.Pause();
        _animationPlayer = player;

        var actualPosition = player.CurrentAnimationPosition;
        GD.Print(
            $"ASSET_GALLERY_ANIMATION={_animationContract}|"
            + $"imported={importedAnimation}|"
            + $"duration={FormatInvariant(animation.Length, "F6")}s|"
            + $"requested={FormatInvariant(_requestedAnimationPositionSeconds, "F6")}s|"
            + $"actual_after_freeze={FormatInvariant(actualPosition, "F6")}s|"
            + $"tolerance={FormatInvariant(AnimationPositionToleranceSeconds, "F6")}s|"
            + "frozen=true");
    }

    public override void _Process(double delta)
    {
        _ = delta;
        if (_captureTerminating || _capturePath is null)
        {
            return;
        }

        _settledFrames++;
        if (_settledFrames < RenderSettlingFrames)
        {
            return;
        }

        try
        {
            CaptureSettledFrame();
        }
        catch (CaptureFailureException exception)
        {
            FailCapture(exception.ErrorCode, exception.Message);
        }
        catch (Exception exception)
        {
            FailCapture("capture_failed", exception.Message);
        }
    }

    private void CaptureSettledFrame()
    {
        var capturePath = _capturePath
            ?? throw new CaptureFailureException(
                "invalid_output_path",
                "The gallery output path became unavailable before capture.");
        var player = _animationPlayer
            ?? throw new CaptureFailureException(
                "animation_player_unavailable",
                "The animation player became unavailable before capture.");
        var actualPosition = player.CurrentAnimationPosition;
        var drift = Math.Abs(
            actualPosition - _requestedAnimationPositionSeconds);

        GD.Print(
            $"ASSET_GALLERY_CAPTURE_POSITION={_animationContract}|"
            + $"requested={FormatInvariant(_requestedAnimationPositionSeconds, "F6")}s|"
            + $"actual={FormatInvariant(actualPosition, "F6")}s|"
            + $"drift={FormatInvariant(drift, "F6")}s|"
            + $"tolerance={FormatInvariant(AnimationPositionToleranceSeconds, "F6")}s");

        if (drift > AnimationPositionToleranceSeconds)
        {
            throw new CaptureFailureException(
                "animation_position_drift",
                $"Animation '{_animationContract}' drifted from requested "
                + $"{FormatInvariant(_requestedAnimationPositionSeconds, "F6")}s to "
                + $"{FormatInvariant(actualPosition, "F6")}s while render frames settled; "
                + $"tolerance is {FormatInvariant(AnimationPositionToleranceSeconds, "F6")}s.");
        }

        var texture = GetViewport().GetTexture();
        if (texture is null)
        {
            throw new CaptureFailureException(
                "rendering_unavailable",
                "The gallery viewport has no render texture.");
        }

        var image = texture.GetImage();
        if (image is null || image.IsEmpty()
            || image.GetWidth() <= 0 || image.GetHeight() <= 0)
        {
            throw new CaptureFailureException(
                "rendering_unavailable",
                "The gallery viewport render texture produced no image.");
        }

        var result = image.SavePng(capturePath);
        if (result != Error.Ok)
        {
            throw new CaptureFailureException(
                "image_write_failed",
                $"Godot failed to save the gallery PNG: {result}.");
        }

        var output = new FileInfo(capturePath);
        if (!output.Exists || output.Length <= 0)
        {
            throw new CaptureFailureException(
                "image_write_failed",
                "The gallery PNG was not written or is empty.");
        }

        CompleteCapture(
            image.GetWidth(),
            image.GetHeight(),
            actualPosition,
            output.Length);
    }

    private void CompleteCapture(
        int width,
        int height,
        double actualPosition,
        long bytes)
    {
        if (_captureTerminating)
        {
            return;
        }

        _captureTerminating = true;
        GD.Print(
            $"ASSET_GALLERY_CAPTURE={_capturePath}|"
            + $"{width}x{height}|bytes={bytes}|"
            + $"contract={_animationContract}|"
            + $"requested={FormatInvariant(_requestedAnimationPositionSeconds, "F6")}s|"
            + $"actual={FormatInvariant(actualPosition, "F6")}s|result=Ok");
        GetTree().Quit(0);
    }

    private void HandleInitializationFailure(string errorCode, string message)
    {
        if (_capturePath is not null)
        {
            FailCapture(errorCode, message);
            return;
        }

        GD.PushError($"Asset-gallery review initialization failed: {message}");
        SetProcess(false);
    }

    private void FailCapture(string errorCode, string message)
    {
        if (_captureTerminating)
        {
            return;
        }

        _captureTerminating = true;
        var safeMessage = message
            .Replace('|', '/')
            .Replace('\r', ' ')
            .Replace('\n', ' ');
        GD.PushError($"Asset-gallery capture failed: {message}");
        GD.Print(
            $"ASSET_GALLERY_CAPTURE_FAILURE|code={errorCode}|"
            + $"contract={_animationContract}|"
            + $"requested={FormatInvariant(_requestedAnimationPositionSeconds, "F6")}s|"
            + $"output={_capturePath}|message={safeMessage}");
        GetTree().Quit(CaptureFailureExitCode);
    }

    private static string FormatInvariant(double value, string format)
    {
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    private sealed class CaptureFailureException(
        string errorCode,
        string message) : Exception(message)
    {
        public string ErrorCode { get; } = errorCode;
    }
}
