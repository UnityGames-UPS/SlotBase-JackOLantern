using Spine.Unity;
using UnityEngine;

public class SpineAnimController : MonoBehaviour
{
    private SkeletonGraphic skeletonGraphic;
    [SerializeField] private string animName;
    private bool isPlaying;
    private Spine.TrackEntry synchronizedTrack;

    public SkeletonGraphic SkeletonGraphic
    {
        get
        {
            if (skeletonGraphic == null)
            {
                skeletonGraphic = GetComponent<SkeletonGraphic>();
            }

            return skeletonGraphic;
        }
    }

    private void Awake()
    {
        skeletonGraphic = GetComponent<SkeletonGraphic>();
    }

    internal void Play(bool loop)
    {
        // Leave shared-clock mode before allowing Spine to advance itself.
        if (synchronizedTrack != null) Stop();
        if (skeletonGraphic == null)
        {
            skeletonGraphic = GetComponent<SkeletonGraphic>();
        }

        if (skeletonGraphic != null && !string.IsNullOrEmpty(animName))
        {
            skeletonGraphic.Initialize(false);
            if (skeletonGraphic.AnimationState == null) return;
            if (!isPlaying)
            {
                var track = skeletonGraphic.AnimationState.SetAnimation(0, animName, loop);
                if (track != null)
                {
                    track.TimeScale = 0.5f;
                }

                isPlaying = true;
            }

            skeletonGraphic.freeze = false;
        }
    }

    internal void Pause()
    {
        if (skeletonGraphic == null)
        {
            skeletonGraphic = GetComponent<SkeletonGraphic>();
        }

        if (skeletonGraphic != null)
        {
            if (skeletonGraphic.SkeletonData == null)
            {
                skeletonGraphic.Initialize(false);
            }

            skeletonGraphic.freeze = true;
        }
    }

    internal void Resume()
    {
        if (skeletonGraphic == null)
        {
            skeletonGraphic = GetComponent<SkeletonGraphic>();
        }

        if (skeletonGraphic != null)
        {
            if (skeletonGraphic.SkeletonData == null)
            {
                skeletonGraphic.Initialize(false);
            }

            if (!isPlaying && !string.IsNullOrEmpty(animName))
            {
                Play(true);
            }

            skeletonGraphic.freeze = synchronizedTrack != null;
        }
    }

    internal void Stop()
    {
        SkeletonGraphic graphic = SkeletonGraphic;
        synchronizedTrack = null;
        isPlaying = false;

        if (graphic == null) return;

        graphic.freeze = true;
        graphic.AnimationState?.ClearTracks();
    }

    internal bool BeginSynchronizedPlayback(SkeletonDataAsset asset)
    {
        if (asset == null) return false;

        Stop();
        SetSkeletonData(asset, "animation");

        SkeletonGraphic graphic = SkeletonGraphic;
        if (graphic == null) return false;

        graphic.Initialize(false);
        Spine.Animation animation = graphic.SkeletonData?.FindAnimation("animation");
        if (graphic.AnimationState == null || animation == null || animation.Duration <= 0f)
            return false;

        graphic.freeze = true;
        graphic.AnimationState.ClearTracks();
        graphic.Skeleton.SetToSetupPose();

        synchronizedTrack = graphic.AnimationState.SetAnimation(0, "animation", true);
        synchronizedTrack.MixDuration = 0f;
        synchronizedTrack.TimeScale = 0f;
        isPlaying = true;
        SetSynchronizedTime(0f, 1f);
        return true;
    }

    internal void SetSynchronizedTime(float elapsedTime, float loopDuration)
    {
        SkeletonGraphic graphic = SkeletonGraphic;
        if (graphic == null || synchronizedTrack == null) return;

        float normalizedTime = Mathf.Max(0f, elapsedTime) / Mathf.Max(0.01f, loopDuration);
        synchronizedTrack.TrackTime = normalizedTime * synchronizedTrack.Animation.Duration;

        graphic.Update(0f);
        graphic.UpdateMesh();
    }

    internal float GetAnimationDuration()
    {
        if (skeletonGraphic == null)
        {
            skeletonGraphic = GetComponent<SkeletonGraphic>();
        }

        if (skeletonGraphic != null)
        {
            if (skeletonGraphic.SkeletonData == null)
            {
                skeletonGraphic.Initialize(false);
            }

            if (skeletonGraphic.SkeletonData != null)
            {
                var anim = skeletonGraphic.SkeletonData.FindAnimation(animName);
                if (anim != null)
                {
                    return anim.Duration / 0.5f;
                }
            }
        }

        return 0f;
    }

    internal void SetSkeletonData(
        SkeletonDataAsset skeletonDataAsset,
        string overrideAnimName = null)
    {
        if (skeletonGraphic == null)
        {
            skeletonGraphic = GetComponent<SkeletonGraphic>();
        }

        if (skeletonGraphic == null) return;

        string oldAnimName = animName;
        if (overrideAnimName != null)
        {
            animName = overrideAnimName;
        }

        if (skeletonGraphic.skeletonDataAsset != skeletonDataAsset)
        {
            synchronizedTrack = null;
            skeletonGraphic.skeletonDataAsset = skeletonDataAsset;
            skeletonGraphic.Initialize(true);
            isPlaying = false;
        }

        if (skeletonGraphic.SkeletonData != null &&
            (string.IsNullOrEmpty(animName) ||
             skeletonGraphic.SkeletonData.FindAnimation(animName) == null))
        {
            var animations = skeletonGraphic.SkeletonData.Animations;
            animName = animations.Count > 0 ? animations.Items[0].Name : string.Empty;
        }

        if (animName != oldAnimName)
        {
            isPlaying = false;
        }
    }
}
