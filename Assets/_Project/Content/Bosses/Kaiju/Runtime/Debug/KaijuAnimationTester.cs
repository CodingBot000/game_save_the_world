using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace TitanDestroyer.Debugging
{
    /// <summary>Isolated clip preview. Never invokes boss AI, damage, or animation events.</summary>
    public sealed class KaijuAnimationTester : MonoBehaviour
    {
        [SerializeField] private Animator target;
        [SerializeField] private AnimationClip[] clips;
        [SerializeField] private Text status;
        [SerializeField] private Text pauseLabel;
        [SerializeField] private Text speedLabel;
        [SerializeField] private Slider timeline;
        [SerializeField] private Button[] clipButtons;

        private PlayableGraph graph;
        private AnimationClipPlayable playable;
        private Transform[] bones;
        private Vector3[] positions;
        private Quaternion[] rotations;
        private Vector3[] scales;
        private float time;
        private float speed = 1f;
        private bool paused;
        private int selected = -1;
        private readonly float[] speeds = { 0.25f, 0.5f, 1f, 2f };

        public int SelectedIndex => selected;
        public float PlaybackTime => time;
        public bool IsPaused => paused;
        public float PlaybackSpeed => speed;
        public Animator Target => target;
        public AnimationClip[] Clips => clips;

        public void Configure(Animator animator, AnimationClip[] animations, Text statusText,
            Text pauseText, Text speedText, Slider timeSlider, Button[] buttons)
        {
            target = animator;
            clips = animations;
            status = statusText;
            pauseLabel = pauseText;
            speedLabel = speedText;
            timeline = timeSlider;
            clipButtons = buttons;
        }

        private void Awake()
        {
            if (target == null || clips == null || clips.Length == 0)
            {
                Debug.LogError("Kaiju animation tester is missing its target or clips.", this);
                enabled = false;
                return;
            }
            target.applyRootMotion = false;
            target.fireEvents = false;
            target.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            bones = target.GetComponentsInChildren<Transform>(true);
            positions = new Vector3[bones.Length];
            rotations = new Quaternion[bones.Length];
            scales = new Vector3[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                positions[i] = bones[i].localPosition;
                rotations[i] = bones[i].localRotation;
                scales[i] = bones[i].localScale;
            }
        }

        private void OnEnable()
        {
            if (bones == null) return;
            if (timeline != null) timeline.onValueChanged.AddListener(SeekNormalized);
            PlayClip(selected < 0 ? 0 : selected);
        }

        private void OnDisable()
        {
            if (timeline != null) timeline.onValueChanged.RemoveListener(SeekNormalized);
            DestroyGraph();
        }

        private void OnDestroy() => DestroyGraph();

        public void PlayClip(int index)
        {
            if (!Application.isPlaying || bones == null || index < 0 || index >= clips.Length || clips[index] == null)
                return;
            DestroyGraph();
            // Restore unkeyed transforms as well, so Death -> Idle cannot retain a stale pose.
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                bones[i].localPosition = positions[i];
                bones[i].localRotation = rotations[i];
                bones[i].localScale = scales[i];
            }
            selected = index;
            time = 0f;
            paused = false;
            graph = PlayableGraph.Create("Kaiju isolated animation test");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable = AnimationClipPlayable.Create(graph, clips[index]);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            var output = AnimationPlayableOutput.Create(graph, "Kaiju", target);
            output.SetSourcePlayable(playable);
            graph.Play();
            EvaluatePose();
        }

        private void Update()
        {
            if (!graph.IsValid() || selected < 0) return;
            Advance(Time.unscaledDeltaTime);
        }

        // Also used by the editor verification harness for deterministic runtime checks.
        public void Advance(float deltaSeconds)
        {
            if (!graph.IsValid() || selected < 0) return;
            var clip = clips[selected];
            if (!paused)
            {
                time += Mathf.Max(0f, deltaSeconds) * speed;
                if (clip.isLooping && clip.length > 0f) time = Mathf.Repeat(time, clip.length);
                else if (time >= clip.length)
                {
                    time = clip.length;
                    paused = true;
                }
            }
            EvaluatePose();
        }

        private void EvaluatePose()
        {
            if (!graph.IsValid()) return;
            playable.SetTime(time);
            graph.Evaluate(0f);
            RefreshUI();
        }

        public void Replay() => PlayClip(selected);

        public void TogglePause()
        {
            if (selected < 0) return;
            if (paused && time >= clips[selected].length) time = 0f;
            paused = !paused;
            RefreshUI();
        }

        public void CycleSpeed()
        {
            int next = (System.Array.IndexOf(speeds, speed) + 1) % speeds.Length;
            speed = speeds[next];
            RefreshUI();
        }

        public void SeekNormalized(float value)
        {
            if (selected < 0 || !graph.IsValid()) return;
            time = Mathf.Clamp01(value) * clips[selected].length;
            paused = true;
            EvaluatePose();
        }

        public void StepFrame()
        {
            if (selected < 0) return;
            paused = true;
            var clip = clips[selected];
            time = Mathf.Min(time + 1f / Mathf.Max(1f, clip.frameRate), clip.length);
            EvaluatePose();
        }

        private void RefreshUI()
        {
            if (selected < 0) return;
            var clip = clips[selected];
            if (status != null)
                status.text = $"{clip.name}\n{time:0.00} / {clip.length:0.00}s   |   {clip.frameRate:0} fps   |   {(clip.isLooping ? "LOOP" : "ONCE / HOLD")}";
            if (pauseLabel != null) pauseLabel.text = paused ? "Resume" : "Pause";
            if (speedLabel != null) speedLabel.text = $"Speed {speed:0.##}x";
            if (timeline != null) timeline.SetValueWithoutNotify(clip.length > 0f ? time / clip.length : 0f);
            if (clipButtons == null) return;
            for (int i = 0; i < clipButtons.Length; i++)
            {
                if (clipButtons[i] == null) continue;
                var colors = clipButtons[i].colors;
                colors.normalColor = i == selected ? new Color(0.15f, 0.65f, 0.64f) : new Color(0.20f, 0.26f, 0.34f);
                colors.selectedColor = colors.normalColor;
                clipButtons[i].colors = colors;
            }
        }

        private void DestroyGraph()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
