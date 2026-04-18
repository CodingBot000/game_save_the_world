using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class VehicleRotorSpinner : MonoBehaviour
{
    [SerializeField] private bool spinOnlyWhilePlaying = true;
    [SerializeField] private List<RotorBinding> rotors = new List<RotorBinding>
    {
        RotorBinding.CreateMainRotor(),
        RotorBinding.CreateTailRotor(),
    };

    private readonly List<ResolvedRotor> resolvedRotors = new List<ResolvedRotor>();
    private readonly HashSet<Transform> claimedTargets = new HashSet<Transform>();
    private bool bindingsResolved;

    private void Awake()
    {
        ResolveBindings();
    }

    private void OnEnable()
    {
        ResolveBindings();
    }

    private void Update()
    {
        if (spinOnlyWhilePlaying && !Application.isPlaying)
        {
            return;
        }

        if (!bindingsResolved)
        {
            ResolveBindings();
        }

        float deltaTime = Time.deltaTime;
        for (int i = 0; i < resolvedRotors.Count; i++)
        {
            ResolvedRotor rotor = resolvedRotors[i];
            if (rotor.Target == null || rotor.Axis.sqrMagnitude < 0.0001f || Mathf.Approximately(rotor.DegreesPerSecond, 0f))
            {
                continue;
            }

            rotor.Target.Rotate(rotor.Axis.normalized, rotor.DegreesPerSecond * deltaTime, Space.Self);
        }
    }

    [ContextMenu("Refresh Rotor Bindings")]
    public void RefreshBindings()
    {
        bindingsResolved = false;
        ResolveBindings();
    }

    private void ResolveBindings()
    {
        EnsureDefaultBindings();

        bindingsResolved = true;
        resolvedRotors.Clear();
        claimedTargets.Clear();

        if (rotors == null || rotors.Count == 0)
        {
            return;
        }

        Transform[] candidates = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < rotors.Count; i++)
        {
            RotorBinding binding = rotors[i];
            if (binding == null)
            {
                continue;
            }

            Transform target = binding.Resolve(transform, candidates, claimedTargets);
            if (target == null)
            {
                continue;
            }

            claimedTargets.Add(target);
            resolvedRotors.Add(new ResolvedRotor(target, binding.LocalAxis, binding.DegreesPerSecond));
        }
    }

    private void EnsureDefaultBindings()
    {
        if (rotors == null)
        {
            rotors = new List<RotorBinding>();
        }

        if (rotors.Count > 0)
        {
            return;
        }

        rotors.Add(RotorBinding.CreateMainRotor());
        rotors.Add(RotorBinding.CreateTailRotor());
    }

    private readonly struct ResolvedRotor
    {
        public ResolvedRotor(Transform target, Vector3 axis, float degreesPerSecond)
        {
            Target = target;
            Axis = axis;
            DegreesPerSecond = degreesPerSecond;
        }

        public Transform Target { get; }
        public Vector3 Axis { get; }
        public float DegreesPerSecond { get; }
    }

    [Serializable]
    private sealed class RotorBinding
    {
        [SerializeField] private string label;
        [SerializeField] private string transformPath;
        [SerializeField] private string[] searchNames;
        [SerializeField] private Vector3 localAxis = Vector3.forward;
        [SerializeField] private float degreesPerSecond = 1440f;

        public Vector3 LocalAxis => localAxis;
        public float DegreesPerSecond => degreesPerSecond;

        public static RotorBinding CreateMainRotor()
        {
            return new RotorBinding(
                "Main Rotor",
                string.Empty,
                Vector3.forward,
                1800f,
                "AH_MainRotor",
                "MainRotor",
                "Main_Rotor",
                "Main Rotor",
                "UpperRotor",
                "TopRotor",
                "Propeller",
                "Rotor");
        }

        public static RotorBinding CreateTailRotor()
        {
            return new RotorBinding(
                "Tail Rotor",
                string.Empty,
                Vector3.right,
                2400f,
                "AH_TailRotor",
                "TailRotor",
                "Tail_Rotor",
                "Tail Rotor",
                "BackRotor",
                "RearRotor",
                "TailPropeller",
                "Tail_Propeller",
                "Tail Propeller");
        }

        private RotorBinding()
        {
            searchNames = Array.Empty<string>();
        }

        private RotorBinding(string newLabel, string newTransformPath, Vector3 newLocalAxis, float newDegreesPerSecond, params string[] newSearchNames)
        {
            label = newLabel;
            transformPath = newTransformPath;
            localAxis = newLocalAxis;
            degreesPerSecond = newDegreesPerSecond;
            searchNames = newSearchNames ?? Array.Empty<string>();
        }

        public Transform Resolve(Transform root, Transform[] candidates, HashSet<Transform> claimedTargets)
        {
            if (!string.IsNullOrWhiteSpace(transformPath))
            {
                Transform pathTarget = FindByPath(root, transformPath);
                if (pathTarget != null && !claimedTargets.Contains(pathTarget))
                {
                    return pathTarget;
                }
            }

            return FindByNames(candidates, claimedTargets);
        }

        private Transform FindByNames(Transform[] candidates, HashSet<Transform> claimedTargets)
        {
            if (candidates == null || candidates.Length == 0)
            {
                return null;
            }

            Transform exactMatch = FindMatch(candidates, claimedTargets, useContainsMatch: false);
            if (exactMatch != null)
            {
                return exactMatch;
            }

            return FindMatch(candidates, claimedTargets, useContainsMatch: true);
        }

        private Transform FindMatch(Transform[] candidates, HashSet<Transform> claimedTargets, bool useContainsMatch)
        {
            Transform bestMatch = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < candidates.Length; i++)
            {
                Transform candidate = candidates[i];
                if (candidate == null || claimedTargets.Contains(candidate))
                {
                    continue;
                }

                int score = ScoreCandidate(candidate.name, useContainsMatch);
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestMatch = candidate;
            }

            return bestMatch;
        }

        private int ScoreCandidate(string candidateName, bool useContainsMatch)
        {
            if (string.IsNullOrWhiteSpace(candidateName) || searchNames == null || searchNames.Length == 0)
            {
                return int.MinValue;
            }

            string normalizedCandidate = Normalize(candidateName);
            if (normalizedCandidate.Length == 0)
            {
                return int.MinValue;
            }

            int bestScore = int.MinValue;
            for (int i = 0; i < searchNames.Length; i++)
            {
                string searchName = searchNames[i];
                if (string.IsNullOrWhiteSpace(searchName))
                {
                    continue;
                }

                string normalizedSearchName = Normalize(searchName);
                if (normalizedSearchName.Length == 0)
                {
                    continue;
                }

                bool matched = useContainsMatch
                    ? normalizedCandidate.IndexOf(normalizedSearchName, StringComparison.OrdinalIgnoreCase) >= 0
                    : normalizedCandidate.Equals(normalizedSearchName, StringComparison.OrdinalIgnoreCase);

                if (!matched)
                {
                    continue;
                }

                int score = normalizedSearchName.Length;
                if (score > bestScore)
                {
                    bestScore = score;
                }
            }

            return bestScore;
        }

        private static Transform FindByPath(Transform root, string candidatePath)
        {
            if (root == null || string.IsNullOrWhiteSpace(candidatePath))
            {
                return null;
            }

            string trimmedPath = candidatePath.Trim();
            if (trimmedPath.StartsWith(root.name + "/", StringComparison.OrdinalIgnoreCase))
            {
                trimmedPath = trimmedPath.Substring(root.name.Length + 1);
            }

            Transform directMatch = root.Find(trimmedPath);
            if (directMatch != null)
            {
                return directMatch;
            }

            Transform[] candidates = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                Transform candidate = candidates[i];
                if (candidate == null)
                {
                    continue;
                }

                string relativePath = GetRelativePath(root, candidate);
                if (relativePath.Equals(trimmedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (root == null || target == null)
            {
                return string.Empty;
            }

            if (target == root)
            {
                return root.name;
            }

            Stack<string> pathStack = new Stack<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                pathStack.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", pathStack);
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (!char.IsLetterOrDigit(character))
                {
                    continue;
                }

                builder.Append(char.ToLowerInvariant(character));
            }

            return builder.ToString();
        }
    }
}
